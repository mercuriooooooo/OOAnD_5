using System.Text.Json;

namespace SpaceBattle.Lib;

public class ConfigException : CommandException
{
    public ConfigException(string message) : base(message) { }
}

public class LibraryValidationException : ConfigException
{
    public LibraryValidationException(string message, Exception? inner = null) : base(message) { }
}


public interface IMatchRulePlugin
{
    string RuleName { get; }
}


public class MatchConfig
{
    public string GameId { get; }
    public string HostLogin { get; }
    public int FleetSize { get; set; } = 3;
    public List<string> Rules { get; } = new();
    public List<string> Plugins { get; } = new();
    public List<Vectors> ShipPositions { get; } = new();

    public MatchConfig(string gameId, string hostLogin)
    {
        GameId = gameId;
        HostLogin = hostLogin;
    }


    public string ToJson() => JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true });
}


public class MatchConfigurator
{
    private readonly AuthService _auth;

    public MatchConfigurator(AuthService auth) => _auth = auth;

    public MatchConfig CreateMatch(Guid hostToken, string gameId)
    {
        _auth.RequireHost(hostToken);
        if (string.IsNullOrWhiteSpace(gameId))
            throw new ConfigException("Идентификатор матча не может быть пустым");
        return new MatchConfig(gameId, _auth.ValidateToken(hostToken));
    }

    public void AddCustomRule(MatchConfig config, string rule)
    {
        if (string.IsNullOrWhiteSpace(rule))
            throw new ConfigException("Правило не может быть пустым");
        if (config.Rules.Contains(rule))
            throw new ConfigException($"Правило '{rule}' уже добавлено в матч '{config.GameId}'");
        config.Rules.Add(rule);
    }

    public void SetFleetSize(MatchConfig config, int size)
    {
        if (size < 1 || size > 10)
            throw new ConfigException("Размер флотилии должен быть от 1 до 10");
        config.FleetSize = size;
    }


    public void ArrangeShips(MatchConfig config, IEnumerable<Vectors> positions)
    {
        var list = positions.ToList();
        if (list.Count != config.FleetSize * 2)
            throw new ConfigException($"Ожидалось {config.FleetSize * 2} позиций (две флотилии), получено {list.Count}");
        config.ShipPositions.Clear();
        config.ShipPositions.AddRange(list);
    }


    public List<string> ValidateLibrary(string path)
    {
        if (!File.Exists(path))
            throw new LibraryValidationException($"Библиотека '{path}' не найдена");

        System.Reflection.Assembly assembly;
        try
        {
            assembly = System.Reflection.Assembly.Load(File.ReadAllBytes(path));
        }
        catch (Exception e)
        {
            throw new LibraryValidationException($"Файл '{path}' не является корректной .NET-сборкой", e);
        }

        try
        {


            var pluginInterface = assembly.GetTypes()
                .FirstOrDefault(t => t.IsInterface && t.FullName == typeof(IMatchRulePlugin).FullName)
                ?? typeof(IMatchRulePlugin);

            var ruleNameProperty = pluginInterface.GetProperty("RuleName");
            return assembly.GetTypes()
                .Where(t => pluginInterface.IsAssignableFrom(t) && !t.IsAbstract && t.IsClass)
                .Select(t => (string?)ruleNameProperty?.GetValue(Activator.CreateInstance(t)) ?? t.Name)
                .ToList();
        }
        catch (Exception e)
        {
            throw new LibraryValidationException($"Ошибка чтения типов из '{path}'", e);
        }
    }

    public void AttachPlugin(MatchConfig config, string pluginName)
    {
        if (string.IsNullOrWhiteSpace(pluginName))
            throw new ConfigException("Имя плагина не может быть пустым");
        config.Plugins.Add(pluginName);
    }
}
