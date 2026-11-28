using System.Reflection;
using System.Text.RegularExpressions;

namespace SpaceBattle.Lib;

public static class TemplateEngine
{
    public static string Load(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        var assembly = typeof(TemplateEngine).Assembly;
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith(fileName, StringComparison.Ordinal));
        if (resourceName == null)
        {
            throw new FileNotFoundException($"Template '{fileName}' was not found.");
        }

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new FileNotFoundException($"Template '{fileName}' was not found.");
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }

    public static string Render(string template, IReadOnlyDictionary<string, object?> model)
    {
        return Apply(template, model).Replace("\n", Environment.NewLine);
    }

    public static string Apply(string template, IReadOnlyDictionary<string, object?> model)
    {
        ArgumentNullException.ThrowIfNull(template);
        ArgumentNullException.ThrowIfNull(model);
        return RenderCore(template.Replace("\r\n", "\n").Replace('\r', '\n'), model);
    }

    private static string RenderCore(string template, IReadOnlyDictionary<string, object?> model)
    {
        var rendered = template;
        var block = new Regex(@"\{\{#(\w+)\}\}((?:(?!\{\{#).)*?)\{\{/\1\}\}", RegexOptions.Singleline);
        Match match;
        while ((match = block.Match(rendered)).Success)
        {
            rendered = rendered.Remove(match.Index, match.Length)
                .Insert(match.Index, Expand(match.Groups[1].Value, match.Groups[2].Value, model));
        }

        foreach (var pair in model)
        {
            if (pair.Value is string text)
            {
                rendered = rendered.Replace("{{" + pair.Key + "}}", text);
            }
        }

        return rendered;
    }

    private static string Expand(string key, string body, IReadOnlyDictionary<string, object?> model)
    {
        model.TryGetValue(key, out var value);
        if (value is bool enabled)
        {
            return enabled ? body : string.Empty;
        }

        if (value is IEnumerable<IReadOnlyDictionary<string, object?>> items)
        {
            return string.Concat(items.Select(item => RenderCore(body, item)));
        }

        return string.Empty;
    }
}
