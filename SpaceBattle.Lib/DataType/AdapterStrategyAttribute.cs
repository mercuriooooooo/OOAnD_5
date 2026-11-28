namespace SpaceBattle.Lib;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
public sealed class AdapterStrategyAttribute : Attribute
{
    public AdapterStrategyAttribute(Type interfaceType, string propertyName, Type strategyType, bool isSetter = false)
    {
        ArgumentNullException.ThrowIfNull(interfaceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        ArgumentNullException.ThrowIfNull(strategyType);

        InterfaceType = interfaceType;
        PropertyName = propertyName;
        StrategyType = strategyType;
        IsSetter = isSetter;
    }

    public Type InterfaceType { get; }
    public string PropertyName { get; }
    public Type StrategyType { get; }
    public bool IsSetter { get; }
}
