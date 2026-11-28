using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace SpaceBattle.Lib;

public static class AdapterCompiler
{
    public static Assembly Compile(string source, IEnumerable<Assembly>? references = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(source);
        var tree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "SpaceBattle.GeneratedAdapters_" + Guid.NewGuid().ToString("N"),
            new[] { tree },
            CollectReferences(references),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var pe = new MemoryStream();
        var result = compilation.Emit(pe);
        if (!result.Success)
        {
            var errors = string.Join(
                Environment.NewLine,
                result.Diagnostics
                    .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)
                    .Select(diagnostic => diagnostic.ToString()));
            throw new InvalidOperationException("Adapter compilation failed." + Environment.NewLine + errors);
        }

        return Assembly.Load(pe.ToArray());
    }

    private static IEnumerable<MetadataReference> CollectReferences(IEnumerable<Assembly>? extra)
    {
        var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var references = new List<MetadataReference>();

        void Add(string? path)
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !paths.Add(path))
            {
                return;
            }

            references.Add(MetadataReference.CreateFromFile(path));
        }

        var trusted = AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string;
        if (!string.IsNullOrEmpty(trusted))
        {
            foreach (var path in trusted.Split(Path.PathSeparator))
            {
                Add(path);
            }
        }

        Add(typeof(object).Assembly.Location);
        if (extra == null)
        {
            return references;
        }

        foreach (var assembly in extra)
        {
            if (assembly != null)
            {
                Add(assembly.Location);
            }
        }

        return references;
    }
}
