using SpaceBattle.Lib;
using Xunit;

public class MatchConfiguratorCoverageTests
{
    private readonly AuthService _auth = new();
    private readonly MatchConfigurator _configurator;

    public MatchConfiguratorCoverageTests()
    {
        _auth.Register("ХостТест", "p");
        var token = _auth.Login("ХостТест", "p");
        _auth.GrantHost(token);
        _configurator = new MatchConfigurator(_auth);
    }

    private Guid HostToken() => _auth.Login("ХостТест", "p");

    [Fact]
    public void CreateMatch_EmptyGameId_Throws()
    {
        Assert.Throws<ConfigException>(() => _configurator.CreateMatch(HostToken(), ""));
        Assert.Throws<ConfigException>(() => _configurator.CreateMatch(HostToken(), "   "));
    }

    [Fact]
    public void AddCustomRule_EmptyRule_Throws()
    {
        var config = _configurator.CreateMatch(HostToken(), "g");
        Assert.Throws<ConfigException>(() => _configurator.AddCustomRule(config, ""));
        Assert.Throws<ConfigException>(() => _configurator.AddCustomRule(config, "   "));
    }

    [Fact]
    public void AttachPlugin_EmptyName_Throws()
    {
        var config = _configurator.CreateMatch(HostToken(), "g");
        Assert.Throws<ConfigException>(() => _configurator.AttachPlugin(config, ""));
    }

    [Fact]
    public void AttachPlugin_Valid_AddsToList()
    {
        var config = _configurator.CreateMatch(HostToken(), "g");
        _configurator.AttachPlugin(config, "метеориты");
        Assert.Contains("метеориты", config.Plugins);
    }

    [Fact]
    public void ValidateLibrary_NotAssembly_Throws()
    {
        var badFile = Path.GetTempFileName();
        try
        {
            File.WriteAllText(badFile, "это не сборка");
            Assert.Throws<LibraryValidationException>(() => _configurator.ValidateLibrary(badFile));
        }
        finally
        {
            File.Delete(badFile);
        }
    }

    [Fact]
    public void ValidateLibrary_ValidAssembly_FindsPlugins()
    {
        var libPath = typeof(MatchConfigurator).Assembly.Location;
        var plugins = _configurator.ValidateLibrary(libPath);
        Assert.NotNull(plugins);
    }

    [Fact]
    public void CreateMatch_ValidHostAndGameId_CreatesConfig()
    {
        var config = _configurator.CreateMatch(HostToken(), "game-42");
        Assert.Equal("game-42", config.GameId);
        Assert.Equal("ХостТест", config.HostLogin);
        Assert.Contains("\"GameId\": \"game-42\"", config.ToJson());
    }
}
