using App;
using App.Scopes;
using SpaceBattle.Lib;

namespace SpaceBattle.Lib.Tests;

public class GameItemTests
{
    [Fact]
    public void StoresAndReplacesProperties()
    {
        var item = new GameItem();
        item.SetProperty("Position", new Vectors(new[] { 1, 1 }));
        Assert.Equal(new Vectors(new[] { 1, 1 }), item.GetProperty("Position"));

        item.SetProperty("Position", new Vectors(new[] { 2, 2 }));
        Assert.Equal(new Vectors(new[] { 2, 2 }), item.GetProperty("Position"));
    }

    [Fact]
    public void GetProperty_ThrowsWhenKeyIsMissing()
    {
        var item = new GameItem();
        var error = Assert.Throws<KeyNotFoundException>(() => item.GetProperty("Missing"));
        Assert.Contains("Missing", error.Message);
        Assert.Throws<ArgumentNullException>(() => item.SetProperty(null!, new object()));
    }
}

public class AdapterStrategyAttributeTests
{
    [Fact]
    public void StoresStrategyDescription()
    {
        var attribute = new AdapterStrategyAttribute(typeof(IMoving), "Velocity", typeof(ThrustVelocityStrategy), true);

        Assert.Equal(typeof(IMoving), attribute.InterfaceType);
        Assert.Equal("Velocity", attribute.PropertyName);
        Assert.Equal(typeof(ThrustVelocityStrategy), attribute.StrategyType);
        Assert.True(attribute.IsSetter);
    }

    [Fact]
    public void RejectsEmptyDescription()
    {
        Assert.Throws<ArgumentNullException>(() => new AdapterStrategyAttribute(null!, "Velocity", typeof(ThrustVelocityStrategy)));
        Assert.ThrowsAny<ArgumentException>(() => new AdapterStrategyAttribute(typeof(IMoving), " ", typeof(ThrustVelocityStrategy)));
        Assert.Throws<ArgumentNullException>(() => new AdapterStrategyAttribute(typeof(IMoving), "Velocity", null!));
    }
}

public class RegisterIoCDependencyAdapterPropertiesTests : IDisposable
{
    public RegisterIoCDependencyAdapterPropertiesTests()
    {
        new InitCommand().Execute();
        var scope = Ioc.Resolve<object>("IoC.Scope.Create");
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", scope).Execute();
    }

    [Fact]
    public void Execute_RegistersGetAndSetForInterfaceProperties()
    {
        new RegisterIoCDependencyAdapterProperties(typeof(IMoving)).Execute();
        var item = new GameItem();
        var position = new Vectors(new[] { 4, 5 });
        var velocity = new Vectors(new[] { 1, 0 });
        item.SetProperty("Position", position);
        item.SetProperty("Velocity", velocity);

        var positionKey = AdapterGenerator.DependencyKey(typeof(IMoving), "Position", false);
        var positionSetKey = AdapterGenerator.DependencyKey(typeof(IMoving), "Position", true);
        var velocityKey = AdapterGenerator.DependencyKey(typeof(IMoving), "Velocity", false);

        Assert.Equal(position, Ioc.Resolve<Vectors>(positionKey, item));
        Assert.Equal(velocity, AdapterStrategies.Get<Vectors>(velocityKey)(new object[] { item }));

        var updated = new Vectors(new[] { 8, 9 });
        Ioc.Resolve<ICommand>(positionSetKey, item, updated).Execute();
        Assert.Equal(updated, item.GetProperty("Position"));
        AdapterStrategies.Set(positionSetKey)(new object[] { item, position });
        Assert.Equal(position, item.GetProperty("Position"));
    }

    [Fact]
    public void Execute_DoesNotRegisterSetterForReadOnlyProperty()
    {
        new RegisterIoCDependencyAdapterProperties(typeof(IRotating)).Execute();
        var setKey = AdapterGenerator.DependencyKey(typeof(IRotating), "AngleSpeed", true);
        Assert.ThrowsAny<Exception>(() => Ioc.Resolve<object>(setKey));
    }

    [Fact]
    public void Execute_SkipsIndexers()
    {
        new RegisterIoCDependencyAdapterProperties(typeof(IWithIndexer)).Execute();
        Assert.ThrowsAny<Exception>(() => Ioc.Resolve<object>("SpaceBattle.Lib.Tests.IWithIndexer.Item"));
    }

    [Fact]
    public void Constructor_RejectsNonInterface()
    {
        Assert.Throws<ArgumentNullException>(() => new RegisterIoCDependencyAdapterProperties(null!));
        Assert.Throws<ArgumentException>(() => new RegisterIoCDependencyAdapterProperties(typeof(GameItem)));
        Assert.ThrowsAny<ArgumentException>(() => AdapterStrategies.Get<int>(" "));
        Assert.ThrowsAny<ArgumentException>(() => AdapterStrategies.Set(null!));
    }

    public void Dispose()
    {
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Clear").Execute();
    }
}
