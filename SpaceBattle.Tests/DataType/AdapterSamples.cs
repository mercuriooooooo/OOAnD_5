using SpaceBattle.Lib;

[assembly: AdapterStrategy(typeof(SpaceBattle.Lib.Tests.IWithLocation), "Location", typeof(SpaceBattle.Lib.Tests.MirrorLocationStrategy), true)]
[assembly: AdapterStrategy(typeof(SpaceBattle.Lib.Tests.IIdentified), "Id", typeof(SpaceBattle.Lib.Tests.ConstantIdStrategy))]

namespace SpaceBattle.Lib.Tests;

public interface IWithLocation
{
    Vectors Location { get; set; }
}

public interface IIdentified
{
    int Id { get; }
}

public interface IShip : IIdentified
{
    string Name { get; set; }
}

public interface IEmptyShip
{
}

public interface ISetOnly
{
    int Value { set; }
}

public interface ITagged
{
    List<string> Tags { get; }
    int[] Zones { get; }
}

public interface IWithMethod
{
    void Run();
}

public interface IWithIndexer
{
    int this[int index] { get; }
}

public interface IWithEvent
{
    event Action Happened;
}

public interface IGenericShip<T>
{
    T Value { get; }
}

public interface IPositioned
{
    Vectors Position { get; set; }
}

public static class MirrorLocationStrategy
{
    public static object Invoke(object[] args)
    {
        var item = (IGameItem)args[0];
        item.SetProperty("Location", args[1]);
        item.SetProperty("LocationMirror", args[1]);
        return args[1];
    }
}

public static class ConstantIdStrategy
{
    public static object Invoke(object[] args) => 7;
}

public static class OverrideIdStrategy
{
    public static object Invoke(object[] args) => 11;
}

public static class BadReturnStrategy
{
    public static int Invoke(object[] args) => 1;
}

public static class NoInvokeStrategy
{
}

public static class ThrustVelocityStrategy
{
    public static object Invoke(object[] args)
    {
        var item = (IGameItem)args[0];
        var angle = (Angle)item.GetProperty("Angle");
        var speed = (int)item.GetProperty("Speed");
        var radians = (double)angle;
        return new Vectors(new[]
        {
            (int)Math.Round(Math.Cos(radians) * speed),
            (int)Math.Round(Math.Sin(radians) * speed)
        });
    }
}
