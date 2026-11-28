using System.Text.Json;

namespace SpaceBattle.Lib;

public class EndPointProxy
{
    private readonly RateLimiter _rateLimiter;
    private readonly IpBanList _banList;
    private readonly ConsistentHashRouter _router;
    private readonly IReadOnlyDictionary<string, IServerQueue> _servers;

    public EndPointProxy(RateLimiter rateLimiter, IpBanList banList,
        ConsistentHashRouter router, IReadOnlyDictionary<string, IServerQueue> servers)
    {
        _rateLimiter = rateLimiter;
        _banList = banList;
        _router = router;
        _servers = servers;
    }

    public string Handle(string json)
    {
        IncomingMessage message;
        try
        {
            message = JsonSerializer.Deserialize<IncomingMessage>(json)
                      ?? throw new JsonException("пустое сообщение");
        }
        catch (JsonException e)
        {
            return $"REJECTED: некорректный JSON ({e.Message})";
        }

        if (string.IsNullOrEmpty(message.GameId))
            return "REJECTED: сообщение без GameId";

        if (_banList.IsBanned(message.Ip))
            return $"REJECTED: IP '{message.Ip}' заблокирован";

        if (!_rateLimiter.TryAcquire(message.Ip))
        {
            _banList.Ban(message.Ip, TimeSpan.FromSeconds(30));
            return $"REJECTED: аномальная активность с '{message.Ip}' — лимит частоты превышен, IP заблокирован";
        }

        var serverId = _router.Route(message.GameId);
        _servers[serverId].Enqueue(new ReceiveMessageCommand(message));
        return $"OK: игра '{message.GameId}' -> сервер '{serverId}'";
    }
}