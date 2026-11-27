using System.Text.Json;
using SpaceBattle.Lib;
using Xunit;

public class GatewayTests
{
    private static (EndPointProxy Proxy, Dictionary<string, IServerQueue> Servers) MakeProxy(
        int capacity = 3, int refillPerSecond = 1000)
    {
        var router = new ConsistentHashRouter();
        var servers = new Dictionary<string, IServerQueue>
        {
            ["server-A"] = new ServerQueue("server-A"),
            ["server-B"] = new ServerQueue("server-B")
        };
        foreach (var id in servers.Keys)
            router.AddServer(id);
        return (new EndPointProxy(new RateLimiter(capacity, refillPerSecond), new IpBanList(), router, servers), servers);
    }

    private static string Msg(string gameId, string ip) =>
        JsonSerializer.Serialize(new IncomingMessage { GameId = gameId, Ip = ip, Type = "move" });

    [Fact]
    public void Route_AllMessagesOfSameGame_GoToSameServer()
    {
        var (proxy, servers) = MakeProxy();
        for (var i = 0; i < 10; i++)
            proxy.Handle(Msg("game-X", $"10.0.0.{i}"));

        var total = servers.Values.Sum(s => s.Count);
        Assert.Equal(10, total);
        Assert.Single(servers.Values.Where(s => s.Count == 10)); // все 10 — на одном сервере
    }

    [Fact]
    public void Route_BadJson_Rejected_WithoutRouting()
    {
        var (proxy, servers) = MakeProxy();
        var result = proxy.Handle("не json {{{");

        Assert.StartsWith("REJECTED", result);
        Assert.Equal(0, servers.Values.Sum(s => s.Count));
    }

    [Fact]
    public void RateLimit_Exceeded_TriggersBan()
    {
        var (proxy, servers) = MakeProxy(capacity: 3, refillPerSecond: 1);
        for (var i = 0; i < 3; i++)
            Assert.StartsWith("OK", proxy.Handle(Msg("g", "10.0.0.9")));

        var result = proxy.Handle(Msg("g", "10.0.0.9")); // 4-й запрос подряд
        Assert.StartsWith("REJECTED", result); // лимит превышен + бан

        var afterBan = proxy.Handle(Msg("g", "10.0.0.9"));
        Assert.Contains("заблокирован", afterBan); // забанен
        Assert.Equal(3, servers.Values.Sum(s => s.Count)); // лишние не прошли
    }

    [Fact]
    public void RateLimit_RefillsOverTime()
    {
        var limiter = new RateLimiter(capacity: 2, refillPerSecond: 500);
        Assert.True(limiter.TryAcquire("ip"));
        Assert.True(limiter.TryAcquire("ip"));
        Assert.False(limiter.TryAcquire("ip"));
        Thread.Sleep(30); // 30 мс * 500/с ≈ 15 токенов
        Assert.True(limiter.TryAcquire("ip"));
    }

    [Fact]
    public void IpBan_Expires()
    {
        var bans = new IpBanList();
        bans.Ban("1.2.3.4", TimeSpan.FromMilliseconds(50));
        Assert.True(bans.IsBanned("1.2.3.4"));
        Thread.Sleep(60);
        Assert.False(bans.IsBanned("1.2.3.4"));
    }

    [Fact]
    public void ConsistentHash_Stable_And_MostlyPreservedAfterRemovingServer()
    {
        var router = new ConsistentHashRouter();
        router.AddServer("A");
        router.AddServer("B");
        var keys = Enumerable.Range(0, 200).Select(i => $"game-{i}").ToList();
        var before = keys.ToDictionary(k => k, k => router.Route(k));

        foreach (var k in keys)
            Assert.Equal(before[k], router.Route(k)); // стабильность

        router.RemoveServer("B");
        var unchanged = keys.Count(k => router.Route(k) == before[k]);
        // консистентное хэширование: уцелевшая доля ~ 1/N серверов перемещается
        Assert.True(unchanged > keys.Count / 4, $"слишком много перемещений: {unchanged}/{keys.Count}");
    }

    [Fact]
    public void ProblemSolver_ReceivesReports_Asynchronously()
    {
        using var solver = new ProblemSolverThread();
        var signal = new ManualResetEventSlim();
        ProblemReport? seen = null;
        solver.ProblemLogged += r => { seen = r; signal.Set(); };

        solver.Report(new InvalidOperationException("сбой"), "контекст");
        Assert.True(signal.Wait(2000), "поток проблем не получил отчёт");
        Assert.Equal("контекст", seen!.Context);
    }
}
