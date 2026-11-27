using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyScopes : ICommand
{
    public void Execute()
    {
        object current = Ioc.Resolve<object>("IoC.Scope.Current");
        Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Clear").Execute();

        try
        {
            var root = (IDictionary<string, Func<object[], object>>)Ioc.Resolve<object>("IoC.Scope.Current");
            root["Scopes.Current"] = _ => Ioc.Resolve<object>("IoC.Scope.Current");
            root["Scopes.Current.Set"] = args => Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", args[0]);
            root["Scopes.Create"] = args => Create(args, threadSafe: false);
            root["Scopes.Create.ThreadSafe"] = args => Create(args, threadSafe: true);
        }
        finally
        {
            Ioc.Resolve<App.ICommand>("IoC.Scope.Current.Set", current).Execute();
        }
    }

    private static object Create(object[] args, bool threadSafe)
    {
        object parent = args.Length > 0
            ? args[0]
            : Ioc.Resolve<object>("IoC.Scope.Current");

        return ScopeContainer.Create(parent, threadSafe);
    }
}
