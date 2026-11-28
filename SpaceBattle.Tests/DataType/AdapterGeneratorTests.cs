using System.Reflection;
using SpaceBattle.Lib;

namespace SpaceBattle.Lib.Tests;

public class AdapterGeneratorTests
{
    private readonly AdapterGenerator _generator = new();

    [Fact]
    public void Generate_IncludesAdapterAndFactoryWithFullDependencyNames()
    {
        var source = _generator.Generate(typeof(IMoving));

        Assert.Contains("class MovingAdapter :", source);
        Assert.Contains("class MovingAdapterFactory :", source);
        Assert.True(source.IndexOf("class MovingAdapter :", StringComparison.Ordinal) <
                    source.IndexOf("class MovingAdapterFactory :", StringComparison.Ordinal));
        Assert.Contains("SpaceBattle.Lib.IMoving.Position", source);
        Assert.Contains("SpaceBattle.Lib.IMoving.Position.Set", source);
        Assert.Contains("SpaceBattle.Lib.IMoving.Velocity", source);
        Assert.DoesNotContain("VelocitySet", source);
        Assert.Contains("AdapterStrategies.Get<global::SpaceBattle.Lib.Vectors>", source);
        Assert.Contains("PositionGet(new object[] { _item })", source);
        Assert.Contains("return new MovingAdapter(item);", source);
    }

    [Fact]
    public void Generate_ReadOnlyPropertyHasNoSetter()
    {
        var source = _generator.Generate(typeof(IRotating));

        Assert.Contains("CurrentAngleSet", source);
        Assert.Contains("AngleSpeedGet", source);
        Assert.DoesNotContain("AngleSpeedSet", source);
        Assert.Contains("SpaceBattle.Lib.IRotating.CurrentAngle", source);
        Assert.NotEqual(
            AdapterGenerator.DependencyKey(typeof(IMoving), "Position", false),
            AdapterGenerator.DependencyKey(typeof(IPositioned), "Position", false));
    }

    [Fact]
    public void Generate_UsesAssemblyStrategyAndKeepsDefaultGetter()
    {
        var source = _generator.Generate(typeof(IWithLocation));

        Assert.Contains("MirrorLocationStrategy.Invoke(args)", source);
        Assert.Contains("AdapterStrategies.Get<global::SpaceBattle.Lib.Vectors>", source);
        Assert.DoesNotContain("AdapterStrategies.Set", source);
    }

    [Fact]
    public void Generate_InheritedStrategyAppliesToDerivedInterface()
    {
        var source = _generator.Generate(typeof(IShip));

        Assert.Contains("ConstantIdStrategy.Invoke(args)", source);
        Assert.Contains("SpaceBattle.Lib.Tests.IShip.Name", source);
        Assert.DoesNotContain("IIdentified.Name", source);
        Assert.DoesNotContain("IdSet", source);
    }

    [Fact]
    public void Generate_ExplicitStrategyOverridesAssemblyStrategy()
    {
        var source = _generator.Generate(typeof(IShip), null, new[]
        {
            new AdapterStrategyAttribute(typeof(IShip), "Id", typeof(OverrideIdStrategy))
        });

        Assert.Contains("OverrideIdStrategy.Invoke(args)", source);
        Assert.DoesNotContain("ConstantIdStrategy", source);
    }

    [Fact]
    public void Generate_IgnoresStrategiesForOtherInterfacesAndSettersOfReadOnlyProperties()
    {
        var testAssembly = typeof(IShip).Assembly;
        var moving = _generator.Generate(typeof(IMoving), new[] { testAssembly, testAssembly });
        Assert.DoesNotContain("ConstantIdStrategy", moving);
        Assert.DoesNotContain("MirrorLocationStrategy", moving);

        var identified = _generator.Generate(typeof(IIdentified), null, new[]
        {
            new AdapterStrategyAttribute(typeof(IIdentified), "Id", typeof(MirrorLocationStrategy), true)
        });
        Assert.Contains("ConstantIdStrategy", identified);
        Assert.DoesNotContain("MirrorLocationStrategy", identified);
        Assert.DoesNotContain("IdSet", identified);
    }

    [Fact]
    public void Generate_RendersGenericAndArrayPropertyTypes()
    {
        var source = _generator.Generate(typeof(ITagged));

        Assert.Contains("global::System.Collections.Generic.List<string>", source);
        Assert.Contains("int[]", source);
    }

    [Fact]
    public void Generate_SetOnlyPropertyHasNoGetter()
    {
        var source = _generator.Generate(typeof(ISetOnly));

        Assert.Contains("ValueSet", source);
        Assert.DoesNotContain("ValueGet", source);
    }

    [Fact]
    public void Generate_SkipsNullAndDuplicateStrategyAssemblies()
    {
        var source = _generator.Generate(typeof(IMoving), new Assembly[] { null!, typeof(IShip).Assembly, null! });

        Assert.Contains("class MovingAdapter :", source);
    }

    [Fact]
    public void Generate_RejectsUnsupportedInterfaces()
    {
        Assert.Throws<ArgumentNullException>(() => _generator.Generate(null!));
        Assert.Throws<ArgumentException>(() => _generator.Generate(typeof(GameItem)));
        Assert.Throws<NotSupportedException>(() => _generator.Generate(typeof(IGenericShip<int>)));
        Assert.Throws<NotSupportedException>(() => _generator.Generate(typeof(IWithMethod)));
        Assert.Throws<NotSupportedException>(() => _generator.Generate(typeof(IWithEvent)));
        var indexer = Assert.Throws<NotSupportedException>(() => _generator.Generate(typeof(IWithIndexer)));
        Assert.Contains("Indexer", indexer.Message);
        var method = Assert.Throws<NotSupportedException>(() => _generator.Generate(typeof(IWithMethod)));
        Assert.Contains("Run", method.Message);
    }

    [Fact]
    public void Generate_RejectsStrategyWithoutObjectInvoke()
    {
        Assert.Throws<InvalidOperationException>(() => _generator.Generate(typeof(IMoving), null, new[]
        {
            new AdapterStrategyAttribute(typeof(IMoving), "Velocity", typeof(BadReturnStrategy))
        }));
        Assert.Throws<InvalidOperationException>(() => _generator.Generate(typeof(IMoving), null, new[]
        {
            new AdapterStrategyAttribute(typeof(IMoving), "Velocity", typeof(NoInvokeStrategy))
        }));
    }

    [Fact]
    public void GenerateToFile_WritesAdapterAndFactoryIntoOneFile()
    {
        var directory = Path.Combine(Path.GetTempPath(), "spacebattle-adapters-" + Guid.NewGuid().ToString("N"));
        try
        {
            _generator.GenerateToFile(typeof(IMoving), directory);
            var source = File.ReadAllText(Path.Combine(directory, "MovingAdapter.cs"));
            Assert.Contains("class MovingAdapter :", source);
            Assert.Contains("class MovingAdapterFactory :", source);
        }
        finally
        {
            if (Directory.Exists(directory))
            {
                Directory.Delete(directory, true);
            }
        }
    }

    [Fact]
    public void GenerateToFile_RejectsEmptyDirectory()
    {
        Assert.ThrowsAny<ArgumentException>(() => _generator.GenerateToFile(typeof(IMoving), " "));
    }

    [Fact]
    public void NamesAndDependencyKeys_UseInterfaceFullName()
    {
        Assert.Equal("MovingAdapter", _generator.GetAdapterName(typeof(IMoving)));
        Assert.Equal("MovingAdapterFactory", _generator.GetFactoryName(typeof(IMoving)));
        Assert.Equal("SpaceBattle.Lib.Generated.MovingAdapterFactory", _generator.GetGeneratedFactoryFullName(typeof(IMoving)));
        Assert.Equal("GameItemAdapter", _generator.GetAdapterName(typeof(GameItem)));
        Assert.Equal("SpaceBattle.Lib.IMoving.Position", AdapterGenerator.DependencyKey(typeof(IMoving), "Position", false));
        Assert.Equal("SpaceBattle.Lib.IMoving.Position.Set", AdapterGenerator.DependencyKey(typeof(IMoving), "Position", true));
        Assert.Throws<ArgumentNullException>(() => _generator.GetAdapterName(null!));
        Assert.Throws<ArgumentNullException>(() => AdapterGenerator.DependencyKey(null!, "Position", false));
        Assert.ThrowsAny<ArgumentException>(() => AdapterGenerator.DependencyKey(typeof(IMoving), " ", false));
    }
}
