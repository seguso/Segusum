using System.Security.Cryptography;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace Segusum.Translator.Core;

public sealed record TranslationWorkItem(int SequenceIndex, string SourceText, string TargetTranslation,
    bool IsTranslated, bool IsObsolete, bool IsChanged, double? Similarity,
    string? RelatedOldSource, string? RelatedOldTranslation, string? SourceFile, int? SourceLine);

public sealed record CatalogFileFingerprint(long Length, DateTime LastWriteUtc, string Sha256);

public sealed class TranslationWorkspace
{
    private readonly SourceStringExtractor extractor = new();
    private readonly TranslationCatalogSynchronizer synchronizer = new();
    private XDocument document = new(new XElement("root"));
    private CatalogFileFingerprint fingerprint = new(0, DateTime.MinValue, "");

    public required string RepositoryRoot { get; init; }
    public required string CatalogPath { get; init; }
    public IReadOnlyList<TranslationWorkItem> Items { get; private set; } = Array.Empty<TranslationWorkItem>();
    public bool IsDirty { get; private set; }

    public void Load(bool synchronize = false)
    {
        var sources = extractor.Extract(RepositoryRoot, options: SourceDiscoveryOptions.Load(RepositoryRoot));
        var current = XDocument.Load(CatalogPath, LoadOptions.PreserveWhitespace);
        var result = synchronize ? synchronizer.Synchronize(sources.Select(x => x.Value).ToList(), current) : new SyncResult(current, new(), false);
        document = result.Document;
        fingerprint = CatalogFileStore.Fingerprint(CatalogPath);
        var pairs = result.Statistics.ChangedPairs.ToDictionary(x => x.NewValue, StringComparer.Ordinal);
        var sourceMap = sources.ToDictionary(x => x.Value, StringComparer.Ordinal);
        var documentEntries = document.Root?.Elements("str").Select(TranslationEntry.FromXml).ToList() ?? new();
        var translatedByOriginal = documentEntries.Where(x => x.IsTranslated)
            .GroupBy(x => x.Original, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        Items = documentEntries.Select((entry, index) =>
        {
            pairs.TryGetValue(entry.Original, out var pair);
            sourceMap.TryGetValue(entry.Original, out var source);
            var previous = !entry.IsObsolete
                ? TranslationCatalogSynchronizer.PreviousTranslated(entry, translatedByOriginal)
                : null;
            return new TranslationWorkItem(index, entry.Original, entry.Translation, entry.IsTranslated, entry.IsObsolete,
                pair is not null, pair?.Similarity, previous?.Original ?? pair?.OldValue,
                previous?.Translation ?? (pair is null ? null : translatedByOriginal.GetValueOrDefault(pair.OldValue)?.Translation), source?.RelativePath, source?.LineNumber);
        }).ToArray() ?? Array.Empty<TranslationWorkItem>();
        IsDirty = synchronize && result.Changed;
    }

    public void Synchronize()
    {
        new TranslationCatalogOperations().Synchronize(RepositoryRoot, CatalogPath);
        Load(false);
    }

    public void SetTranslation(int sequenceIndex, string translation)
    {
        var entries = document.Root?.Elements("str").ToList() ?? new();
        if (sequenceIndex < 0 || sequenceIndex >= entries.Count) return;
        entries[sequenceIndex].SetAttributeValue("transl", translation);
        document = new XDocument(document.Declaration, new XElement("root", entries));
        Items = Items.Select((item, index) => index == sequenceIndex ? item with { TargetTranslation = translation, IsTranslated = translation != "+" } : item).ToArray();
        IsDirty = true;
    }

    public void Save()
    {
        if (!IsDirty) return;
        CatalogFileStore.SaveAtomic(CatalogPath, document, fingerprint);
        fingerprint = CatalogFileStore.Fingerprint(CatalogPath);
        IsDirty = false;
    }

}

public static class CatalogFileStore
{
    public static CatalogFileFingerprint Fingerprint(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return new CatalogFileFingerprint(new FileInfo(path).Length, File.GetLastWriteTimeUtc(path), Convert.ToHexString(sha.ComputeHash(stream)));
    }

    public static void SaveAtomic(string path, XDocument document, CatalogFileFingerprint expected)
    {
        if (Fingerprint(path) != expected) throw new IOException("Il catalogo è cambiato sul disco: ricaricalo prima di salvare.");
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            var settings = new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false, Encoding = new UTF8Encoding(false), NewLineChars = "\n", NewLineHandling = NewLineHandling.Entitize };
            using (var writer = XmlWriter.Create(temp, settings)) document.Save(writer);
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }

    public static void SaveAtomicNew(string path, XDocument document)
    {
        if (File.Exists(path)) throw new IOException($"Catalog already exists: {path}");
        var temp = path + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            var settings = new XmlWriterSettings { Indent = true, OmitXmlDeclaration = false, Encoding = new UTF8Encoding(false), NewLineChars = "\n", NewLineHandling = NewLineHandling.Entitize };
            using (var writer = XmlWriter.Create(temp, settings)) document.Save(writer);
            File.Move(temp, path);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
