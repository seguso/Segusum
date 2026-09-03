using System.Xml.Linq;
using System.Xml;

namespace Segusum.Translator.Core;

public sealed record TranslationEntry(string Original, string Translation,
    IReadOnlyDictionary<string, string> Attributes)
{
    public bool IsObsolete => Attributes.TryGetValue("obsolete", out var value) && value == "true";
    public bool IsTranslated => Translation != "+";

    public XElement ToXml() => new("str", Attributes
        .Where(x => x.Key is not "orig" and not "transl")
        .Select(x => new XAttribute(x.Key, x.Value))
        .Prepend(new XAttribute("orig", Original))
        .Append(new XAttribute("transl", Translation)));

    public TranslationEntry With(string? translation = null, bool? obsolete = null) {
        var attrs = new Dictionary<string, string>(Attributes, StringComparer.Ordinal);
        if (obsolete == true) attrs["obsolete"] = "true";
        if (obsolete == false) attrs.Remove("obsolete");
        return this with { Translation = translation ?? Translation, Attributes = attrs };
    }

    public static TranslationEntry FromXml(XElement element)
    {
        var attrs = element.Attributes().ToDictionary(x => x.Name.LocalName, x => x.Value, StringComparer.Ordinal);
        return new TranslationEntry(attrs.GetValueOrDefault("orig", ""),
            attrs.GetValueOrDefault("transl", "+"), attrs);
    }
}

public sealed record ChangedPair(string OldValue, string NewValue, int Distance, double Similarity);

public sealed class SyncStatistics
{
    public int Unchanged { get; internal set; }
    public int New { get; internal set; }
    public int ModifiedOrReplaced { get; internal set; }
    public int PreservedTranslatedObsolete { get; internal set; }
    public int RemovedUntranslatedObsolete { get; internal set; }
    public int Reactivated { get; internal set; }
    public List<ChangedPair> ChangedPairs { get; } = new();
}

public sealed record SyncResult(XDocument Document, SyncStatistics Statistics, bool Changed);

public sealed class TranslationCatalogSynchronizer
{
    internal const string TranslationChainAttribute = "translation-chain";
    internal const string PreviousTranslatedAttribute = "previous-translated-orig";
    // Similarity is only a conservative hint inside a small sequence-diff
    // replacement block; exact originals are always matched first.
    public const double DefaultSimilarityThreshold = 0.80;
    private const double AmbiguityMargin = 0.03;
    private readonly double similarityThreshold;

    public TranslationCatalogSynchronizer(double similarityThreshold = DefaultSimilarityThreshold)
    {
        this.similarityThreshold = similarityThreshold;
    }

    public SyncResult Synchronize(IReadOnlyList<string> sourceStrings, XDocument current)
    {
        var all = current.Root?.Elements("str").Select(TranslationEntry.FromXml).ToList()
                  ?? new List<TranslationEntry>();
        var active = all.Where(x => !x.IsObsolete).ToList();
        var newValues = sourceStrings.Distinct(StringComparer.Ordinal).ToList();
        var newValueSet = new HashSet<string>(newValues, StringComparer.Ordinal);
        var activeIndex = active.Select((entry, index) => (entry, index))
            .GroupBy(x => x.entry.Original, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First().index, StringComparer.Ordinal);
        var obsoleteIndex = all.Select((entry, index) => (entry, index))
            .Where(x => x.entry.IsObsolete)
            .GroupBy(x => x.entry.Original, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First().index, StringComparer.Ordinal);
        var unmatchedOld = active.Where(x => !newValueSet.Contains(x.Original)).ToList();
        var unmatchedNew = newValues.Where(x => !activeIndex.ContainsKey(x) && !obsoleteIndex.ContainsKey(x)).ToList();
        var matchesByNew = new Dictionary<string, (TranslationEntry Entry, int Distance, double Similarity)>(StringComparer.Ordinal);
        // Build the diff over the complete active/new sequences. Exact strings
        // are the narrative anchors; only each resulting replacement block may
        // use fuzzy matching. The exact maps above remain global so a moved
        // translation is preserved rather than treated as a replacement.
        if (active.Count <= 10_000 && newValues.Count <= 10_000)
        {
            foreach (var op in SequenceDiff.Build(active.Select(x => x.Original).ToList(), newValues))
            {
                if (op.Kind != DiffKind.Replace) continue;
                var oldBlock = active.Skip(op.OldStart).Take(op.OldLength)
                    .Where(x => !newValueSet.Contains(x.Original)).ToList();
                var newBlock = newValues.Skip(op.NewStart).Take(op.NewLength)
                    .Where(x => !activeIndex.ContainsKey(x) && !obsoleteIndex.ContainsKey(x)).ToList();
                if (oldBlock.Count > 250 || newBlock.Count > 250) continue;
                foreach (var match in MatchBlock(oldBlock, newBlock))
                    matchesByNew[newBlock[match.NewIndex]] = (oldBlock[match.OldIndex], match.Distance, match.Similarity);
            }
        }

        var output = new List<TranslationEntry>();
        var retainedObsolete = new HashSet<string>(StringComparer.Ordinal);
        var usedActive = new HashSet<int>();
        var processedOld = new HashSet<string>(StringComparer.Ordinal);
        var stats = new SyncStatistics();
        foreach (var value in newValues)
        {
            if (activeIndex.TryGetValue(value, out var activePosition))
            {
                usedActive.Add(activePosition);
                var entry = active[activePosition].With(obsolete: false);
                output.Add(entry); stats.Unchanged++;
                AppendPreviousTranslated(entry, all, output, retainedObsolete);
                continue;
            }
            if (obsoleteIndex.TryGetValue(value, out var obsoletePosition))
            {
                var entry = all[obsoletePosition].With(obsolete: false);
                output.Add(entry);
                AppendPreviousTranslated(entry, all, output, retainedObsolete);
                retainedObsolete.Add(value); stats.Reactivated++;
                continue;
            }
            if (matchesByNew.TryGetValue(value, out var match))
            {
                var previous = PreviousTranslated(match.Entry, all);
                var lineage = EnsureLineage(match.Entry, previous);
                var newEntry = new TranslationEntry(value, "+", lineage);
                output.Add(newEntry);
                if (previous is not null)
                {
                    var previousEntry = WithLineage(previous, lineage);
                    output.Add(previousEntry.With(obsolete: true));
                    retainedObsolete.Add(previousEntry.Original); stats.PreservedTranslatedObsolete++;
                }
                else if (!match.Entry.IsTranslated) stats.RemovedUntranslatedObsolete++;
                if (match.Entry.IsTranslated && previous?.Original != match.Entry.Original)
                    stats.PreservedTranslatedObsolete++;
                processedOld.Add(match.Entry.Original);
                stats.ModifiedOrReplaced++;
                stats.ChangedPairs.Add(new ChangedPair(match.Entry.Original, value, match.Distance, match.Similarity));
                continue;
            }
            output.Add(new TranslationEntry(value, "+", new Dictionary<string, string>(StringComparer.Ordinal)));
            stats.New++;
        }

        foreach (var (entry, index) in active.Select((entry, index) => (entry, index)))
            if (!usedActive.Contains(index) && !processedOld.Contains(entry.Original) && !retainedObsolete.Contains(entry.Original))
                PreserveOrDropObsolete(entry, output, retainedObsolete, stats);
        foreach (var entry in all.Where(x => x.IsObsolete && x.IsTranslated && !retainedObsolete.Contains(x.Original)))
            output.Add(entry);

        var doc = new XDocument(new XDeclaration("1.0", "utf-8", null), new XElement("root", output.Select(x => x.ToXml())));
        return new SyncResult(doc, stats, !DocumentsEquivalent(current, doc));
    }

    private static void AppendPreviousTranslated(TranslationEntry current, IReadOnlyList<TranslationEntry> all,
        List<TranslationEntry> output, HashSet<string> retainedObsolete)
    {
        var previous = PreviousTranslated(current, all);
        if (previous is null || !previous.IsObsolete || retainedObsolete.Contains(previous.Original)) return;
        output.Add(previous.With(obsolete: true));
        retainedObsolete.Add(previous.Original);
    }

    internal static TranslationEntry? PreviousTranslated(TranslationEntry entry, IReadOnlyList<TranslationEntry> all)
    {
        if (entry.IsTranslated) return entry;
        if (!entry.Attributes.TryGetValue(PreviousTranslatedAttribute, out var previousOriginal)) return null;
        return all.FirstOrDefault(x => x.Original == previousOriginal && x.IsTranslated);
    }

    internal static TranslationEntry? PreviousTranslated(TranslationEntry entry,
        IReadOnlyDictionary<string, TranslationEntry> translatedByOriginal)
    {
        if (entry.IsTranslated) return entry;
        if (!entry.Attributes.TryGetValue(PreviousTranslatedAttribute, out var previousOriginal)) return null;
        return translatedByOriginal.GetValueOrDefault(previousOriginal);
    }

    private static Dictionary<string, string> EnsureLineage(TranslationEntry entry, TranslationEntry? previous)
    {
        var attributes = new Dictionary<string, string>(entry.Attributes, StringComparer.Ordinal);
        if (!attributes.TryGetValue(TranslationChainAttribute, out var chain))
            attributes[TranslationChainAttribute] = Guid.NewGuid().ToString("N");
        if (previous is not null) attributes[PreviousTranslatedAttribute] = previous.Original;
        else attributes.Remove(PreviousTranslatedAttribute);
        return attributes;
    }

    private static TranslationEntry WithLineage(TranslationEntry entry, IReadOnlyDictionary<string, string> lineage)
    {
        var attributes = new Dictionary<string, string>(entry.Attributes, StringComparer.Ordinal);
        if (lineage.TryGetValue(TranslationChainAttribute, out var chain)) attributes[TranslationChainAttribute] = chain;
        return entry with { Attributes = attributes };
    }

    private static void PreserveOrDropObsolete(TranslationEntry entry, List<TranslationEntry> output,
        HashSet<string> retained, SyncStatistics stats)
    {
        if (entry.IsTranslated)
        {
            output.Add(entry.With(obsolete: true)); retained.Add(entry.Original);
            stats.PreservedTranslatedObsolete++;
        }
        else stats.RemovedUntranslatedObsolete++;
    }

    private List<BlockMatch> MatchBlock(IReadOnlyList<TranslationEntry> oldBlock, IReadOnlyList<string> newBlock)
    {
        if (oldBlock.Count == 0 || newBlock.Count == 0) return new();
        var scores = new BlockMatch?[oldBlock.Count, newBlock.Count];
        var similarities = new double?[oldBlock.Count, newBlock.Count];
        for (var oldIndex = 0; oldIndex < oldBlock.Count; oldIndex++)
            for (var newIndex = 0; newIndex < newBlock.Count; newIndex++)
            {
                var distance = Levenshtein(oldBlock[oldIndex].Original, newBlock[newIndex]);
                var similarity = Similarity(oldBlock[oldIndex].Original, newBlock[newIndex], distance);
                similarities[oldIndex, newIndex] = similarity;
            }
        for (var oldIndex = 0; oldIndex < oldBlock.Count; oldIndex++)
            for (var newIndex = 0; newIndex < newBlock.Count; newIndex++)
            {
                var similarity = similarities[oldIndex, newIndex]!.Value;
                if (similarity < similarityThreshold || IsAmbiguous(similarities, oldIndex, newIndex, similarity, similarityThreshold)) continue;
                var distance = Levenshtein(oldBlock[oldIndex].Original, newBlock[newIndex]);
                var oldRelative = oldBlock.Count == 1 ? 0d : (double)oldIndex / (oldBlock.Count - 1);
                var newRelative = newBlock.Count == 1 ? 0d : (double)newIndex / (newBlock.Count - 1);
                var positionalPenalty = Math.Abs(oldRelative - newRelative) * 0.10;
                scores[oldIndex, newIndex] = new BlockMatch(oldIndex, newIndex, distance, similarity, similarity - positionalPenalty);
            }

        var best = new double[oldBlock.Count + 1, newBlock.Count + 1];
        var take = new bool[oldBlock.Count + 1, newBlock.Count + 1];
        for (var oldIndex = 1; oldIndex <= oldBlock.Count; oldIndex++)
            for (var newIndex = 1; newIndex <= newBlock.Count; newIndex++)
            {
                best[oldIndex, newIndex] = Math.Max(best[oldIndex - 1, newIndex], best[oldIndex, newIndex - 1]);
                var candidate = scores[oldIndex - 1, newIndex - 1];
                if (candidate is not null && best[oldIndex - 1, newIndex - 1] + candidate.Score + 0.25 > best[oldIndex, newIndex])
                {
                    best[oldIndex, newIndex] = best[oldIndex - 1, newIndex - 1] + candidate.Score + 0.25;
                    take[oldIndex, newIndex] = true;
                }
            }

        var chosen = new List<BlockMatch>();
        var i = oldBlock.Count; var j = newBlock.Count;
        while (i > 0 && j > 0)
        {
            if (take[i, j]) { chosen.Add(scores[i - 1, j - 1]!); i--; j--; }
            else if (best[i - 1, j] >= best[i, j - 1]) i--;
            else j--;
        }
        chosen.Reverse();
        return chosen;
    }

    private static bool IsAmbiguous(double?[,] similarities, int oldIndex, int newIndex,
        double similarity, double threshold)
    {
        for (var otherOld = 0; otherOld < similarities.GetLength(0); otherOld++)
        {
            if (otherOld == oldIndex) continue;
            var otherSimilarity = similarities[otherOld, newIndex];
            if (otherSimilarity >= threshold && Math.Abs(otherSimilarity.Value - similarity) < AmbiguityMargin) return true;
        }
        for (var otherNew = 0; otherNew < similarities.GetLength(1); otherNew++)
        {
            if (otherNew == newIndex) continue;
            var otherSimilarity = similarities[oldIndex, otherNew];
            if (otherSimilarity >= threshold && Math.Abs(otherSimilarity.Value - similarity) < AmbiguityMargin) return true;
        }
        return false;
    }

    private static double Similarity(string a, string b, int distance)
    {
        var max = Math.Max(a.Length, b.Length);
        return max == 0 ? 1 : 1d - (double)distance / max;
    }

    private sealed record BlockMatch(int OldIndex, int NewIndex, int Distance, double Similarity, double Score);

    private static bool DocumentsEquivalent(XDocument left, XDocument right) =>
        (left.Root?.Elements("str").Select(x => x.ToString(SaveOptions.DisableFormatting)) ?? Enumerable.Empty<string>())
            .SequenceEqual(right.Root?.Elements("str").Select(x => x.ToString(SaveOptions.DisableFormatting)) ?? Enumerable.Empty<string>(),
                StringComparer.Ordinal);

    private static int Levenshtein(string a, string b)
    {
        var previous = Enumerable.Range(0, b.Length + 1).ToArray();
        var current = new int[b.Length + 1];
        for (var i = 1; i <= a.Length; i++)
        {
            current[0] = i;
            for (var j = 1; j <= b.Length; j++)
                current[j] = Math.Min(Math.Min(current[j - 1] + 1, previous[j] + 1),
                    previous[j - 1] + (a[i - 1] == b[j - 1] ? 0 : 1));
            (previous, current) = (current, previous);
        }
        return previous[b.Length];
    }
}
