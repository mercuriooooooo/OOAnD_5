using SpaceBattle.Lib;

namespace SpaceBattle.Lib.Tests;

public class GameRuleGeneratorTests
{
    private readonly GameRuleGenerator _generator = new();

    [Fact]
    public void Templates_AreTextFiles()
    {
        Assert.Contains("{{AdapterName}}", TemplateEngine.Load("adapter.txt"));
        Assert.Contains("{{FactoryName}}", TemplateEngine.Load("factory.txt"));
        Assert.Contains("{{Name}}Get", TemplateEngine.Load("property.txt"));
        Assert.Contains("{{RuleName}}", TemplateEngine.Load("game-rule.txt"));
        Assert.Contains("{{Rules}}", TemplateEngine.Load("game-rules.txt"));
    }

    [Fact]
    public void Generate_BuildsRuleFromCommand()
    {
        var source = _generator.Generate(new[] { typeof(MoveCommand), typeof(RotateCommand) });

        Assert.Contains("class MoveGameRule :", source);
        Assert.Contains("class RotateGameRule :", source);
        Assert.Contains("public string Name => \"Move\";", source);
        Assert.Contains("public string DependencyKey => \"Commands.Move\";", source);
        Assert.Contains("SpaceBattle.Lib.IMoving", source);
        Assert.Contains("SpaceBattle.Lib.IRotating", source);
        Assert.Contains("Move выполняется командой SpaceBattle.Lib.MoveCommand.", source);
    }

    [Fact]
    public void Generate_CompilesRulesThatDescribeCommands()
    {
        var commands = GameRuleGenerator.FindGameCommands(typeof(MoveCommand).Assembly);
        Assert.Contains(typeof(MoveCommand), commands);
        Assert.Contains(typeof(FireCommand), commands);
        Assert.Contains(typeof(NullCommand), commands);
        Assert.DoesNotContain(typeof(Game), commands);
        Assert.DoesNotContain(typeof(RegisterIoCDependencyMoveCommand), commands);

        var assembly = AdapterCompiler.Compile(
            _generator.Generate(commands),
            new[] { typeof(IGameRule).Assembly });
        var rule = (IGameRule)Activator.CreateInstance(assembly.GetType("SpaceBattle.Lib.Generated.FireGameRule")!)!;

        Assert.Equal("Fire", rule.Name);
        Assert.Equal("Commands.Fire", rule.DependencyKey);
        Assert.Contains("SpaceBattle.Lib.Vectors", rule.Parameters);
        Assert.Contains("FireCommand", rule.Describe());
    }

    [Fact]
    public void Generate_RejectsTypeThatIsNotAGameCommand()
    {
        Assert.Throws<ArgumentNullException>(() => _generator.Generate(null!));
        Assert.Throws<ArgumentException>(() => _generator.Generate(new[] { typeof(Game) }));
        Assert.Throws<ArgumentNullException>(() => GameRuleGenerator.FindGameCommands(null!));
        Assert.Throws<ArgumentNullException>(() => GameRuleGenerator.IsGameCommand(null!));
        Assert.Throws<FileNotFoundException>(() => TemplateEngine.Load("missing.txt"));
    }

    [Fact]
    public void GenerateToFile_WritesRulesFromCommands()
    {
        var directory = Path.Combine(Path.GetTempPath(), "spacebattle-rules-" + Guid.NewGuid().ToString("N"));
        try
        {
            _generator.GenerateToFile(new[] { typeof(NullCommand) }, directory);
            var source = File.ReadAllText(Path.Combine(directory, "GameRules.cs"));
            Assert.Contains("class NullGameRule :", source);
            Assert.Contains("public string Parameters => \"\";", source);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }
}
