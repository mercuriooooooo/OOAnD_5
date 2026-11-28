using System.Reflection;

namespace SpaceBattle.Lib;

public class GameRuleGenerator
{
    public string Generate(IEnumerable<Type> commandTypes)
    {
        ArgumentNullException.ThrowIfNull(commandTypes);
        var rules = string.Join('\n', commandTypes.Select(RenderRule));
        return TemplateEngine.Render(TemplateEngine.Load("game-rules.txt"), new Dictionary<string, object?>
        {
            ["Rules"] = rules
        });
    }

    public void GenerateToFile(IEnumerable<Type> commandTypes, string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, "GameRules.cs"), Generate(commandTypes));
    }

    public static IReadOnlyList<Type> FindGameCommands(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        return assembly.GetTypes()
            .Where(IsGameCommand)
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .ToList();
    }

    public static bool IsGameCommand(Type type)
    {
        ArgumentNullException.ThrowIfNull(type);
        return type is { IsClass: true, IsAbstract: false, IsPublic: true }
            && type.Name.EndsWith("Command", StringComparison.Ordinal)
            && !type.Name.StartsWith("Register", StringComparison.Ordinal)
            && typeof(ICommand).IsAssignableFrom(type);
    }

    private static string RenderRule(Type commandType)
    {
        if (!IsGameCommand(commandType))
        {
            throw new ArgumentException($"'{commandType.FullName}' is not a game command.", nameof(commandType));
        }

        var commandName = commandType.Name[..^"Command".Length];
        var constructor = commandType.GetConstructors()
            .OrderByDescending(item => item.GetParameters().Length)
            .FirstOrDefault();
        var parameters = constructor == null
            ? string.Empty
            : string.Join(", ", constructor.GetParameters().Select(parameter =>
                (parameter.ParameterType.FullName ?? parameter.ParameterType.Name).Replace('+', '.')));

        return TemplateEngine.Apply(TemplateEngine.Load("game-rule.txt"), new Dictionary<string, object?>
        {
            ["RuleName"] = commandName + "GameRule",
            ["CommandName"] = commandName,
            ["CommandType"] = (commandType.FullName ?? commandType.Name).Replace('+', '.'),
            ["DependencyKey"] = "Commands." + commandName,
            ["Parameters"] = parameters,
            ["Description"] = commandName + " выполняется командой " + (commandType.FullName ?? commandType.Name) + "."
        }).TrimEnd();
    }
}
