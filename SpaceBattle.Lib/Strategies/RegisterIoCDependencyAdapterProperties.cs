using App;

namespace SpaceBattle.Lib;

public class RegisterIoCDependencyAdapterProperties : ICommand
{
    private readonly Type _interfaceType;

    public RegisterIoCDependencyAdapterProperties(Type interfaceType)
    {
        ArgumentNullException.ThrowIfNull(interfaceType);
        if (!interfaceType.IsInterface)
        {
            throw new ArgumentException("Adapter properties can be registered only for an interface.", nameof(interfaceType));
        }

        _interfaceType = interfaceType;
    }

    public void Execute()
    {
        foreach (var property in AdapterGenerator.GetProperties(_interfaceType))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                continue;
            }

            var propertyName = property.Name;
            if (property.GetMethod != null)
            {
                var getKey = AdapterGenerator.DependencyKey(_interfaceType, propertyName, false);
                Ioc.Resolve<App.ICommand>("IoC.Register", getKey, (object[] args) =>
                    ((IGameItem)args[0]).GetProperty(propertyName)).Execute();
            }

            if (property.SetMethod != null)
            {
                var setKey = AdapterGenerator.DependencyKey(_interfaceType, propertyName, true);
                Ioc.Resolve<App.ICommand>("IoC.Register", setKey, (object[] args) =>
                    new DelegateCommand(() => ((IGameItem)args[0]).SetProperty(propertyName, args[1]))).Execute();
            }
        }
    }
}
