using SpaceBattle.Lib;
using Xunit;

public class LobbyTests
{
    private readonly AuthService _auth = new();
    private readonly InviteService _invites;

    public LobbyTests() => _invites = new InviteService(_auth);

    private Guid Player(string login)
    {
        _auth.Register(login, "p");
        return _auth.Login(login, "p");
    }

    [Fact]
    public void Invite_SendAccept_Works()
    {
        var a = Player("ИгрокА");
        Player("ИгрокБ");
        var invite = _invites.SendInvite(a, "ИгрокБ", "g1");
        var accepted = _invites.Accept(_auth.Login("ИгрокБ", "p"), "ИгрокА");

        Assert.Equal(InviteStatus.Accepted, accepted.Status);
        Assert.Equal("g1", accepted.GameId);
        Assert.Equal(invite.Key, accepted.Key);
    }

    [Fact]
    public void Invite_SendToOffline_Throws()
    {
        var a = Player("ИгрокА");
        Player("ИгрокБ");
        _auth.Logout(_auth.Login("ИгрокБ", "p"));
        Assert.Throws<LobbyException>(() => _invites.SendInvite(a, "ИгрокБ", "g1"));
    }

    [Fact]
    public void Invite_Reject_FiresEvent_ForRatingPenalty()
    {
        var a = Player("ИгрокА");
        Player("ИгрокБ");
        _invites.SendInvite(a, "ИгрокБ", "g1");

        Invite? rejected = null;
        _invites.InviteRejected += i => rejected = i;
        _invites.Reject(_auth.Login("ИгрокБ", "p"), "ИгрокА");

        Assert.NotNull(rejected);
        Assert.Equal(InviteStatus.Rejected, rejected!.Status);
    }

    [Fact]
    public void Invite_DoubleProcessing_Throws()
    {
        var a = Player("ИгрокА");
        Player("ИгрокБ");
        _invites.SendInvite(a, "ИгрокБ", "g1");
        _invites.Accept(_auth.Login("ИгрокБ", "p"), "ИгрокА");
        Assert.Throws<LobbyException>(() => _invites.Accept(_auth.Login("ИгрокБ", "p"), "ИгрокА"));
    }

    [Fact]
    public void MatchConfig_NonHostCannotCreate()
    {
        var plain = Player("ИгрокБ");
        var configurator = new MatchConfigurator(_auth);
        Assert.Throws<AuthException>(() => configurator.CreateMatch(plain, "g1"));
    }

    [Fact]
    public void MatchConfig_HostCreates_AddsRules_ArrangesShips()
    {
        var host = Player("ИгрокА");
        _auth.GrantHost(host);
        var configurator = new MatchConfigurator(_auth);

        var config = configurator.CreateMatch(host, "g1");
        configurator.AddCustomRule(config, "метеориты");
        configurator.SetFleetSize(config, 2);
        configurator.ArrangeShips(config, new[] { new Vectors(new[]{0, 0}), new Vectors(new[]{1, 1}), new Vectors(new[]{5, 5}), new Vectors(new[]{6, 6}) });

        Assert.Equal("g1", config.GameId);
        Assert.Contains("метеориты", config.Rules);
        Assert.Equal(4, config.ShipPositions.Count);
        Assert.Contains("\"GameId\": \"g1\"", config.ToJson()); // сериализация матча
    }

    [Fact]
    public void MatchConfig_DuplicateRule_And_BadFleetSize_Throw()
    {
        var host = Player("ИгрокА");
        _auth.GrantHost(host);
        var configurator = new MatchConfigurator(_auth);
        var config = configurator.CreateMatch(host, "g1");

        configurator.AddCustomRule(config, "метеориты");
        Assert.Throws<ConfigException>(() => configurator.AddCustomRule(config, "метеориты"));
        Assert.Throws<ConfigException>(() => configurator.SetFleetSize(config, 11));
    }

    [Fact]
    public void LibraryValidation_MissingFile_Throws()
    {
        var configurator = new MatchConfigurator(_auth);
        Assert.Throws<LibraryValidationException>(() => configurator.ValidateLibrary("нет-такого-файла.dll"));
    }

    [Fact]
    public void LibraryValidation_ValidAssembly_FindsPlugins()
    {
        var configurator = new MatchConfigurator(_auth);
        var libPath = typeof(ICommand).Assembly.Location; // SpaceBattle.Lib.dll
        var plugins = configurator.ValidateLibrary(libPath);

        Assert.NotNull(plugins); // сборка валидна, не бросила исключение
    }
}
