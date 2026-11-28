using App;

namespace SpaceBattle.Lib;

public static class AdapterStrategies
{
    public static Func<object[], object> Get<T>(string dependencyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyName);
        return args => Ioc.Resolve<T>(dependencyName, (IGameItem)args[0])!;
    }

    public static Action<object[]> Set(string dependencyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dependencyName);
        return args => Ioc.Resolve<ICommand>(dependencyName, (IGameItem)args[0], args[1]).Execute();
    }
}
