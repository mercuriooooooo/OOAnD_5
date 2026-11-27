using System.Collections.Concurrent;

namespace SpaceBattle.Lib;

public static class ScopeContainer
{
    public const string ParentDependency = "IoC.Scope.Parent";

    public static IDictionary<string, Func<object[], object>> Create(object parent, bool threadSafe = false)
    {
        IDictionary<string, Func<object[], object>> scope = threadSafe
            ? new ConcurrentDictionary<string, Func<object[], object>>()
            : new Dictionary<string, Func<object[], object>>();

        scope[ParentDependency] = _ => parent;
        return scope;
    }

    public static void Register(object scope, string dependency, Func<object[], object> strategy)
    {
        var storage = (IDictionary<string, Func<object[], object>>)scope;
        storage[dependency] = strategy;
    }
}
