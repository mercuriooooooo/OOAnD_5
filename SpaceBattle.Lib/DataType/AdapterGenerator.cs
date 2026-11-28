using System.Reflection;
using System.Text;

namespace SpaceBattle.Lib;

public class AdapterGenerator
{
    public const string GeneratedNamespace = "SpaceBattle.Lib.Generated";

    public string Generate(
        Type interfaceType,
        IEnumerable<Assembly>? strategyAssemblies = null,
        IEnumerable<AdapterStrategyAttribute>? strategies = null)
    {
        Validate(interfaceType);
        var selected = SelectStrategies(interfaceType, strategyAssemblies, strategies);
        var adapterName = GetAdapterName(interfaceType);
        var properties = new StringBuilder();
        foreach (var property in GetProperties(interfaceType))
        {
            if (property.GetIndexParameters().Length > 0)
            {
                throw new NotSupportedException($"Indexer '{property.Name}' is not supported.");
            }

            properties.Append(RenderProperty(interfaceType, property, selected));
        }

        var values = new Dictionary<string, object?>
        {
            ["AdapterName"] = adapterName,
            ["FactoryName"] = GetFactoryName(interfaceType),
            ["InterfaceType"] = TypeName(interfaceType),
            ["Properties"] = properties.ToString().TrimEnd('\n')
        };
        var adapter = TemplateEngine.Apply(TemplateEngine.Load("adapter.txt"), values);
        var factory = TemplateEngine.Apply(TemplateEngine.Load("factory.txt"), values);
        return TemplateEngine.Render(TemplateEngine.Load("generated-file.txt"), new Dictionary<string, object?>
        {
            ["Adapter"] = adapter.TrimEnd('\r', '\n'),
            ["Factory"] = factory.TrimEnd('\r', '\n')
        });
    }

    public void GenerateToFile(
        Type interfaceType,
        string directory,
        IEnumerable<Assembly>? strategyAssemblies = null,
        IEnumerable<AdapterStrategyAttribute>? strategies = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var source = Generate(interfaceType, strategyAssemblies, strategies);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, GetAdapterName(interfaceType) + ".cs");
        File.WriteAllText(path, source);
    }

    public string GetAdapterName(Type interfaceType)
    {
        ArgumentNullException.ThrowIfNull(interfaceType);
        var name = interfaceType.Name;
        if (name.Length > 1 && name[0] == 'I' && char.IsUpper(name[1]))
        {
            name = name[1..];
        }

        return name + "Adapter";
    }

    public string GetFactoryName(Type interfaceType) => GetAdapterName(interfaceType) + "Factory";

    public string GetGeneratedFactoryFullName(Type interfaceType) =>
        GeneratedNamespace + "." + GetFactoryName(interfaceType);

    public static IReadOnlyList<PropertyInfo> GetProperties(Type interfaceType)
    {
        ArgumentNullException.ThrowIfNull(interfaceType);
        var properties = new Dictionary<string, PropertyInfo>();
        foreach (var parent in interfaceType.GetInterfaces())
        {
            foreach (var property in parent.GetProperties())
            {
                properties[property.Name] = property;
            }
        }

        foreach (var property in interfaceType.GetProperties())
        {
            properties[property.Name] = property;
        }

        return properties.Values.ToList();
    }

    public static string DependencyKey(Type interfaceType, string propertyName, bool setter)
    {
        ArgumentNullException.ThrowIfNull(interfaceType);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);
        var fullName = (interfaceType.FullName ?? interfaceType.Name).Replace('+', '.');
        return setter ? $"{fullName}.{propertyName}.Set" : $"{fullName}.{propertyName}";
    }

    private static void Validate(Type interfaceType)
    {
        ArgumentNullException.ThrowIfNull(interfaceType);
        if (!interfaceType.IsInterface)
        {
            throw new ArgumentException("Adapters can be generated only for interfaces.", nameof(interfaceType));
        }

        if (interfaceType.IsGenericType)
        {
            throw new NotSupportedException("Generic interfaces are not supported.");
        }

        var hierarchy = new[] { interfaceType }.Concat(interfaceType.GetInterfaces());
        if (hierarchy.Any(type => type.GetEvents().Length > 0))
        {
            throw new NotSupportedException("Adapter generator supports properties only.");
        }

        var methods = hierarchy
            .SelectMany(type => type.GetMethods())
            .Where(method => !method.IsSpecialName)
            .Select(method => method.Name)
            .Distinct()
            .ToArray();
        if (methods.Length > 0)
        {
            throw new NotSupportedException("Adapter generator supports properties only. Methods: " + string.Join(", ", methods) + ".");
        }
    }

    private static Dictionary<(string Name, bool IsSetter), AdapterStrategyAttribute> SelectStrategies(
        Type interfaceType,
        IEnumerable<Assembly>? strategyAssemblies,
        IEnumerable<AdapterStrategyAttribute>? strategies)
    {
        var selected = new Dictionary<(string Name, bool IsSetter), AdapterStrategyAttribute>();
        var seen = new HashSet<Assembly>();

        void Take(AdapterStrategyAttribute attribute)
        {
            if (attribute.InterfaceType.IsAssignableFrom(interfaceType))
            {
                selected[(attribute.PropertyName, attribute.IsSetter)] = attribute;
            }
        }

        void Scan(Assembly? assembly)
        {
            if (assembly == null || !seen.Add(assembly))
            {
                return;
            }

            foreach (var attribute in assembly.GetCustomAttributes<AdapterStrategyAttribute>())
            {
                Take(attribute);
            }
        }

        Scan(interfaceType.Assembly);
        if (strategyAssemblies != null)
        {
            foreach (var assembly in strategyAssemblies)
            {
                Scan(assembly);
            }
        }

        if (strategies != null)
        {
            foreach (var attribute in strategies)
            {
                Take(attribute);
            }
        }

        return selected;
    }

    private static string RenderProperty(
        Type interfaceType,
        PropertyInfo property,
        Dictionary<(string Name, bool IsSetter), AdapterStrategyAttribute> selected)
    {
        var hasGetter = property.GetMethod != null;
        var hasSetter = property.SetMethod != null;
        AdapterStrategyAttribute? getter = null;
        AdapterStrategyAttribute? setter = null;
        var customGetter = hasGetter && selected.TryGetValue((property.Name, false), out getter);
        var customSetter = hasSetter && selected.TryGetValue((property.Name, true), out setter);
        return TemplateEngine.Apply(TemplateEngine.Load("property.txt"), new Dictionary<string, object?>
        {
            ["Name"] = property.Name,
            ["TypeName"] = TypeName(property.PropertyType),
            ["HasGetter"] = hasGetter,
            ["HasSetter"] = hasSetter,
            ["CustomGetter"] = customGetter,
            ["DefaultGetter"] = hasGetter && !customGetter,
            ["CustomSetter"] = customSetter,
            ["DefaultSetter"] = hasSetter && !customSetter,
            ["GetterCall"] = customGetter ? StrategyCall(getter!) : string.Empty,
            ["SetterCall"] = customSetter ? StrategyCall(setter!) : string.Empty,
            ["GetKey"] = hasGetter ? DependencyKey(interfaceType, property.Name, false) : string.Empty,
            ["SetKey"] = hasSetter ? DependencyKey(interfaceType, property.Name, true) : string.Empty
        });
    }

    private static string StrategyCall(AdapterStrategyAttribute attribute)
    {
        var method = attribute.StrategyType.GetMethod(
            "Invoke",
            BindingFlags.Public | BindingFlags.Static,
            binder: null,
            types: new[] { typeof(object[]) },
            modifiers: null);

        if (method == null || method.ReturnType != typeof(object))
        {
            throw new InvalidOperationException(
                $"Strategy '{attribute.StrategyType.FullName}' must declare 'public static object Invoke(object[] args)'.");
        }

        return TypeName(attribute.StrategyType) + ".Invoke(args)";
    }

    private static string TypeName(Type type)
    {
        if (type == typeof(void)) return "void";
        if (type == typeof(bool)) return "bool";
        if (type == typeof(byte)) return "byte";
        if (type == typeof(int)) return "int";
        if (type == typeof(long)) return "long";
        if (type == typeof(float)) return "float";
        if (type == typeof(double)) return "double";
        if (type == typeof(string)) return "string";
        if (type == typeof(object)) return "object";
        if (type.IsArray)
        {
            return TypeName(type.GetElementType()!) + "[]";
        }

        if (type.IsGenericType)
        {
            var name = type.GetGenericTypeDefinition().FullName ?? type.Name;
            var tick = name.IndexOf('`');
            if (tick >= 0)
            {
                name = name[..tick];
            }

            var arguments = string.Join(", ", type.GetGenericArguments().Select(TypeName));
            return "global::" + name.Replace('+', '.') + "<" + arguments + ">";
        }

        var fullName = (type.FullName ?? type.Name).Replace('+', '.');
        return "global::" + fullName;
    }
}
