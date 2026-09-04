using System.Xml.Linq;

namespace Segusum.WebClient;

public sealed record SegusumClientString(string Key, string Source, string Translation);

/// <summary>Read-only engine client catalogues shipped inside Segusum.WebClient.</summary>
public static class SegusumClientCatalog
{
    private const string ResourcePrefix = "Segusum.WebClient.Translations.transl_";

    public static IReadOnlyDictionary<string, SegusumClientString> Load(string language)
    {
        var normalized = language is "it" or "de" ? language : "en";
        using var stream = typeof(SegusumClientCatalog).Assembly
            .GetManifestResourceStream(ResourcePrefix + normalized + ".xml")
            ?? throw new InvalidOperationException($"Missing embedded client catalogue for '{normalized}'.");
        return Parse(XDocument.Load(stream));
    }

    public static IReadOnlyDictionary<string, SegusumClientString> Parse(XDocument document)
    {
        var result = new Dictionary<string, SegusumClientString>(StringComparer.Ordinal);
        foreach (var element in document.Root?.Elements("str") ?? Enumerable.Empty<XElement>())
        {
            var key = element.Attribute("key")?.Value;
            var source = element.Attribute("orig")?.Value;
            var translation = element.Attribute("transl")?.Value;
            if (string.IsNullOrWhiteSpace(key) || source is null || translation is null)
                throw new InvalidOperationException("Every engine client string must have key, orig and transl attributes.");
            if (!result.TryAdd(key, new SegusumClientString(key, source, translation)))
                throw new InvalidOperationException($"Duplicate engine client string key '{key}'.");
        }
        return result;
    }

    public static Dictionary<string, string> Resolve(string language,
        IReadOnlyDictionary<string, string> overrides, string consumerRoot)
    {
        var engine = Load(language);
        var italian = language == "de" ? Load("it") : engine;
        var consumer = LoadConsumerCatalogue(language, consumerRoot);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var (key, definition) in engine)
        {
            if (overrides.TryGetValue(key, out var overrideSource))
            {
                var consumerSource = ToCatalogOriginal(overrideSource);
                result[key] = consumer.TryGetValue(consumerSource, out var translated) && translated != "+"
                    ? translated.Replace("''", "\"", StringComparison.Ordinal) : overrideSource;
                continue;
            }
            var selected = definition.Translation != "+"
                ? definition.Translation
                : italian[key].Translation != "+" ? italian[key].Translation : definition.Source;
            result[key] = selected.Replace("''", "\"", StringComparison.Ordinal);
        }
        return result;
    }

    public static string ToCatalogOriginal(string source)
        => source.Replace("\"", "''", StringComparison.Ordinal);

    private static Dictionary<string, string> LoadConsumerCatalogue(string language, string root)
    {
        var path = Path.Combine(root, $"transl_{language}.xml");
        if (!File.Exists(path)) return new(StringComparer.Ordinal);
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var element in XDocument.Load(path).Root?.Elements("str") ?? Enumerable.Empty<XElement>())
        {
            var original = element.Attribute("orig")?.Value;
            var translation = element.Attribute("transl")?.Value;
            if (original is not null && translation is not null) result[original] = translation;
        }
        return result;
    }
}
