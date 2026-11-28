using System.Reflection;
using App;

namespace SpaceBattle.Lib;

public class AdapterFactory : IFactory
{
    private readonly IFactory _generatedFactory;

    public AdapterFactory(
        Type interfaceType,
        IEnumerable<Assembly>? strategyAssemblies = null,
        IEnumerable<AdapterStrategyAttribute>? strategies = null)
    {
        ArgumentNullException.ThrowIfNull(interfaceType);
        var generator = new AdapterGenerator();
        var source = generator.Generate(interfaceType, strategyAssemblies, strategies);
        var compiled = AdapterCompiler.Compile(source, ReferencesFor(interfaceType, strategyAssemblies, strategies));
        var factoryTypeName = generator.GetGeneratedFactoryFullName(interfaceType);
        var factoryType = compiled.GetType(factoryTypeName)
            ?? throw new InvalidOperationException($"Generated factory '{factoryTypeName}' was not found.");
        _generatedFactory = (IFactory)Activator.CreateInstance(factoryType)!;
    }

    public object Create(IGameItem item)
    {
        ArgumentNullException.ThrowIfNull(item);
        return _generatedFactory.Create(item);
    }

    private static IEnumerable<Assembly> ReferencesFor(
        Type interfaceType,
        IEnumerable<Assembly>? strategyAssemblies,
        IEnumerable<AdapterStrategyAttribute>? strategies)
    {
        var references = new List<Assembly>
        {
            interfaceType.Assembly,
            typeof(AdapterFactory).Assembly,
            typeof(Ioc).Assembly
        };

        if (strategyAssemblies != null)
        {
            references.AddRange(strategyAssemblies);
        }

        if (strategies != null)
        {
            references.AddRange(strategies.Select(strategy => strategy.StrategyType.Assembly));
        }

        var scanned = new List<Assembly> { interfaceType.Assembly };
        if (strategyAssemblies != null)
        {
            scanned.AddRange(strategyAssemblies);
        }

        foreach (var assembly in scanned.Distinct())
        {
            if (assembly == null)
            {
                continue;
            }

            foreach (var attribute in assembly.GetCustomAttributes<AdapterStrategyAttribute>())
            {
                references.Add(attribute.StrategyType.Assembly);
            }
        }

        return references;
    }
}

public class AdapterFactory<TInterface> : IFactory where TInterface : class
{
    private readonly AdapterFactory _inner;

    public AdapterFactory(
        IEnumerable<Assembly>? strategyAssemblies = null,
        IEnumerable<AdapterStrategyAttribute>? strategies = null)
    {
        _inner = new AdapterFactory(typeof(TInterface), strategyAssemblies, strategies);
    }

    public object Create(IGameItem item) => _inner.Create(item);
}
