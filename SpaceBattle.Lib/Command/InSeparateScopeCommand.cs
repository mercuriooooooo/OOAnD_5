using App;

namespace SpaceBattle.Lib;

public class InSeparateScopeCommand : ICommand
{
    private readonly object _scope;
    private readonly ICommand _cmd;

    public InSeparateScopeCommand(ICommand cmd, object scope)
    {
        _scope = scope;
        _cmd = cmd;
    }

    public void Execute()
    {
        object oldScope = Ioc.Resolve<object>("Scopes.Current");
        Ioc.Resolve<App.ICommand>("Scopes.Current.Set", _scope).Execute();

        try
        {
            _cmd.Execute();
        }
        finally
        {
            Ioc.Resolve<App.ICommand>("Scopes.Current.Set", oldScope).Execute();
        }
    }
}
