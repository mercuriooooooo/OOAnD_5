using System.Reflection;
using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Lib.Tests;

public class AdapterCompilerTests
{
    [Fact]
    public void Compile_LoadsGeneratedAssemblyAndIgnoresDynamicReferences()
    {
        var first = AdapterCompiler.Compile("public class Marker {}");
        var second = AdapterCompiler.Compile("public class Marker2 {}", new[] { first });

        Assert.NotNull(first.GetType("Marker"));
        Assert.NotNull(second.GetType("Marker2"));
    }

    [Fact]
    public void Compile_RejectsInvalidSource()
    {
        var error = Assert.Throws<InvalidOperationException>(() => AdapterCompiler.Compile("class {"));
        Assert.Contains("Adapter compilation failed", error.Message);
        Assert.ThrowsAny<ArgumentException>(() => AdapterCompiler.Compile(" "));
        Assert.ThrowsAny<ArgumentException>(() => AdapterCompiler.Compile(null!));
    }
}

public class AdapterFactoryTests : IDisposable
{
    public AdapterFactoryTests()
    {
        new InitCommand().Execute();
        var scope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();
    }

    [Fact]
    public void Create_BuildsMovingAdapterUsedByMoveCommand()
    {
        new RegisterIoCDependencyAdapterProperties(typeof(IMoving)).Execute();
        var item = new GameItem();
        item.SetProperty("Position", new Vectors(new[] { 1, 2 }));
        item.SetProperty("Velocity", new Vectors(new[] { 3, 4 }));

        var factory = new AdapterFactory<IMoving>();
        var adapter = (IMoving)factory.Create(item);
        new MoveCommand(adapter).Execute();

        Assert.Equal(new Vectors(new[] { 4, 6 }), adapter.Position);
        Assert.Equal(new Vectors(new[] { 4, 6 }), item.GetProperty("Position"));
    }

    [Fact]
    public void Create_ReplacedStrategyIsUsedInsteadOfIoC()
    {
        var factory = new AdapterFactory<IMoving>();
        var adapter = (IMoving)factory.Create(new GameItem());
        Assert.ThrowsAny<Exception>(() => _ = adapter.Position);

        var field = adapter.GetType().GetField("PositionGet", BindingFlags.Public | BindingFlags.Static);
        Assert.NotNull(field);
        field!.SetValue(null, new Func<object[], object>(_ => new Vectors(new[] { 9, 8 })));

        Assert.Equal(new Vectors(new[] { 9, 8 }), adapter.Position);
    }

    [Fact]
    public void Create_CustomVelocityStrategyDoesNotReadVelocityProperty()
    {
        new RegisterIoCDependencyAdapterProperties(typeof(IMoving)).Execute();
        var attribute = new AdapterStrategyAttribute(typeof(IMoving), "Velocity", typeof(ThrustVelocityStrategy));
        var assembly = typeof(ThrustVelocityStrategy).Assembly;
        var factory = new AdapterFactory<IMoving>(new[] { assembly, assembly }, new[] { attribute });
        var item = new GameItem();
        item.SetProperty("Angle", new Angle(2));
        item.SetProperty("Speed", 4);

        var adapter = (IMoving)factory.Create(item);

        Assert.Equal(new Vectors(new[] { 0, 4 }), adapter.Velocity);
        Assert.Throws<KeyNotFoundException>(() => item.GetProperty("Velocity"));
    }

    [Fact]
    public void Create_CustomSetterStrategyWritesExtraProperty()
    {
        new RegisterIoCDependencyAdapterProperties(typeof(IWithLocation)).Execute();
        var factory = new AdapterFactory<IWithLocation>();
        var item = new GameItem();
        var location = new Vectors(new[] { 2, 3 });

        var adapter = (IWithLocation)factory.Create(item);
        adapter.Location = location;

        Assert.Equal(location, adapter.Location);
        Assert.Equal(location, item.GetProperty("LocationMirror"));
    }

    [Fact]
    public void Create_InheritedGetterStrategyAndOwnProperty()
    {
        new RegisterIoCDependencyAdapterProperties(typeof(IShip)).Execute();
        var factory = new AdapterFactory<IShip>();
        var item = new GameItem();
        item.SetProperty("Name", "Alpha");

        var ship = (IShip)factory.Create(item);
        Assert.Equal(7, ship.Id);
        Assert.Equal("Alpha", ship.Name);

        ship.Name = "Beta";
        Assert.Equal("Beta", item.GetProperty("Name"));
    }

    [Fact]
    public void Create_ReturnsNewAdapterOnEveryCall()
    {
        var factory = new AdapterFactory(typeof(IEmptyShip), new Assembly[] { null! });
        var first = factory.Create(new GameItem());
        var second = factory.Create(new GameItem());

        Assert.IsAssignableFrom<IEmptyShip>(first);
        Assert.Equal(first.GetType(), second.GetType());
        Assert.NotSame(first, second);
        Assert.Throws<ArgumentNullException>(() => factory.Create(null!));
        Assert.Throws<ArgumentNullException>(() => new AdapterFactory(null!));
    }

    public void Dispose()
    {
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Clear").Execute();
    }
}
