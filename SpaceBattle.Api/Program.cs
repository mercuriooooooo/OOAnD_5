using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using SpaceBattle.Lib;

var builder = WebApplication.CreateBuilder(args);
var services = builder.Services;

var auth = new AuthService();
var invites = new InviteService(auth);
var rating = new RatingService();
var matchmaker = new FairMatchmaker(rating);
invites.InviteRejected += invite => rating.Penalize(invite.To);

var router = new ConsistentHashRouter();
var serverA = new ServerQueue("server-A");
var serverB = new ServerQueue("server-B");
router.AddServer(serverA.ServerId);
router.AddServer(serverB.ServerId);
var servers = new Dictionary<string, IServerQueue>
{
    [serverA.ServerId] = serverA,
    [serverB.ServerId] = serverB
};
var gateway = new EndPointProxy(new RateLimiter(20, 5), new IpBanList(), router, servers);

var app = builder.Build();

app.MapPost("/api/register", (AuthServiceRegisterRequest r) =>
{
    try
    {
        auth.Register(r.Login, r.Password);
        return Results.Ok(new { ok = true });
    }
    catch (AuthException e)
    {
        return Results.BadRequest(new { error = e.Message });
    }
});

app.MapPost("/api/login", (AuthServiceRegisterRequest r) =>
{
    try
    {
        var token = auth.Login(r.Login, r.Password);
        return Results.Ok(new { token });
    }
    catch (AuthException e)
    {
        return Results.BadRequest(new { error = e.Message });
    }
});

app.MapGet("/api/leaderboard", () => Results.Json(rating.Leaderboard()));

app.MapPost("/api/invite", (InviteRequest r) =>
{
    try
    {
        var invite = invites.SendInvite(r.FromToken, r.ToLogin, r.GameId);
        return Results.Ok(new { invite.From, invite.To, invite.GameId, invite.Status });
    }
    catch (LobbyException e)
    {
        return Results.BadRequest(new { error = e.Message });
    }
});

app.MapPost("/api/invite/accept", (InviteDecisionRequest r) =>
{
    try
    {
        var invite = invites.Accept(r.Token, r.FromLogin);
        return Results.Ok(new { invite.From, invite.To, invite.Status });
    }
    catch (LobbyException e)
    {
        return Results.BadRequest(new { error = e.Message });
    }
});

app.MapPost("/api/invite/reject", (InviteDecisionRequest r) =>
{
    try
    {
        var invite = invites.Reject(r.Token, r.FromLogin);
        return Results.Ok(new { invite.From, invite.To, invite.Status });
    }
    catch (LobbyException e)
    {
        return Results.BadRequest(new { error = e.Message });
    }
});

app.MapPost("/api/matchmaking", (MatchmakingRequest r) =>
{
    matchmaker.Enqueue(r.Login);
    var pair = matchmaker.TryPair();
    return Results.Ok(new { pair });
});

app.MapPost("/api/gateway", (HttpRequest request) =>
{
    using var reader = new StreamReader(request.Body);
    var body = reader.ReadToEndAsync().Result;
    var ip = request.HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown";
    var result = gateway.Handle(body.Replace("\"Ip\":", $"\"Ip\": \"{ip}\", \"_\" : ", StringComparison.Ordinal) is var _ && body.Contains("\"Ip\"") ? body : body.Replace("{", $"{{\"Ip\":\"{ip}\",", StringComparison.Ordinal));
    return Results.Ok(new { result });
});

app.MapGet("/api/queue/{serverId}", (string serverId) =>
{
    if (!servers.TryGetValue(serverId, out var queue))
        return Results.NotFound(new { error = $"сервер '{serverId}' не найден" });
    return Results.Ok(new { serverId, messages = queue.Count });
});

app.Run();

public record AuthServiceRegisterRequest(string Login, string Password);
public record InviteRequest(Guid FromToken, string ToLogin, string GameId);
public record InviteDecisionRequest(Guid Token, string FromLogin);
public record MatchmakingRequest(string Login);