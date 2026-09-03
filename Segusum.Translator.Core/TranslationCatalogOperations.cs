using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace Segusum.Translator.Core;

public sealed record CatalogSynchronizationResult(string Path, SyncResult Result, bool Created);

/// <summary>Shared catalog preparation operations used by both CLI and Web.</summary>
public sealed class TranslationCatalogOperations
{
    private static readonly Regex LanguageCode = new("^[A-Za-z][A-Za-z0-9-]*$", RegexOptions.Compiled);
    private readonly SourceStringExtractor extractor = new();
    private readonly TranslationCatalogSynchronizer synchronizer = new();

    public CatalogSynchronizationResult Synchronize(string root, string catalogPath, bool write = true)
    {
        var fullPath = Path.GetFullPath(catalogPath);
        if (!File.Exists(fullPath)) throw new FileNotFoundException("Translation catalogue not found.", fullPath);
        var result = SynchronizeDocument(root, fullPath, XDocument.Load(fullPath, LoadOptions.PreserveWhitespace));
        if (write && result.Result.Changed)
            CatalogFileStore.SaveAtomic(fullPath, result.Result.Document, CatalogFileStore.Fingerprint(fullPath));
        return result;
    }

    public CatalogSynchronizationResult Create(string root, string language, bool write = true)
    {
        if (!LanguageCode.IsMatch(language))
            throw new ArgumentException("Language must start with a letter and contain only letters, digits, or '-'.", nameof(language));
        var fullPath = Path.Combine(Path.GetFullPath(root), $"transl_{language.ToLowerInvariant()}.xml");
        if (File.Exists(fullPath)) throw new IOException($"Translation catalogue already exists: {fullPath}");
        var result = SynchronizeDocument(root, fullPath, new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement("root")));
        if (write) CatalogFileStore.SaveAtomicNew(fullPath, result.Result.Document);
        return result with { Created = true };
    }

    private CatalogSynchronizationResult SynchronizeDocument(string root, string path, XDocument current)
    {
        var sources = extractor.Extract(root, options: SourceDiscoveryOptions.Load(root));
        var result = synchronizer.Synchronize(sources.Select(x => x.Value).ToList(), current);
        return new CatalogSynchronizationResult(path, result, false);
    }
}
