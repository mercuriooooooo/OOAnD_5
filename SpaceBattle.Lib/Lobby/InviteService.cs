using System.Collections.Concurrent;

namespace SpaceBattle.Lib;

public class LobbyException : CommandException
{
    public LobbyException(string message) : base(message) { }
}

public enum InviteStatus { Pending, Accepted, Rejected }

public class Invite
{
    public string From { get; init; } = "";
    public string To { get; init; } = "";
    public InviteStatus Status { get; set; } = InviteStatus.Pending;
    public string GameId { get; init; } = "";
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    public string Key => $"{From}->{To}";
}


public class InviteService
{
    private readonly AuthService _auth;
    private readonly ConcurrentDictionary<string, Invite> _invites = new();

    public event Action<Invite>? InviteAccepted;
    public event Action<Invite>? InviteRejected;

    public InviteService(AuthService auth) => _auth = auth;

    public Invite SendInvite(Guid fromToken, string toLogin, string gameId)
    {
        var from = _auth.ValidateToken(fromToken);
        if (from.Equals(toLogin, StringComparison.OrdinalIgnoreCase))
            throw new LobbyException("Нельзя пригласить самого себя");
        if (!_auth.IsOnline(toLogin))
            throw new LobbyException($"Игрок '{toLogin}' офлайн — инвайт отправить нельзя");

        var invite = new Invite { From = from, To = toLogin, GameId = gameId };
        _invites[invite.Key] = invite;
        return invite;
    }

    public Invite Accept(Guid toToken, string fromLogin)
    {
        var invite = Take(toToken, fromLogin);
        invite.Status = InviteStatus.Accepted;
        InviteAccepted?.Invoke(invite);
        return invite;
    }

    public Invite Reject(Guid toToken, string fromLogin)
    {
        var invite = Take(toToken, fromLogin);
        invite.Status = InviteStatus.Rejected;
        InviteRejected?.Invoke(invite);
        return invite;
    }

    public Invite? Find(string fromLogin, string toLogin) =>
        _invites.TryGetValue($"{fromLogin}->{toLogin}", out var i) ? i : null;

    private Invite Take(Guid toToken, string fromLogin)
    {
        var me = _auth.ValidateToken(toToken);
        var invite = Find(fromLogin, me)
            ?? throw new LobbyException($"Инвайт от '{fromLogin}' не найден");
        if (invite.Status != InviteStatus.Pending)
            throw new LobbyException($"Инвайт от '{fromLogin}' уже обработан ({invite.Status})");
        return invite;
    }
}
