using System.Xml.Linq;
using Segusum.Translator.Core;

namespace Segusum.Translator.Core.Tests;

public sealed class TranslationCatalogSynchronizerTests
{
    [Fact]
    public void UnchangedSequenceIsIdempotentAndPreservesTranslations()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "A", "B" }, Catalog(("A", "a"), ("B", "b")));
        Assert.False(result.Changed);
        Assert.Equal(new[] { "a", "b" }, result.Document.Root!.Elements("str").Select(x => x.Attribute("transl")!.Value));
    }

    [Fact]
    public void ExtractedAfterActionStringReactivatesExistingTranslatedCatalogEntry()
    {
        var root = Path.Combine(Path.GetTempPath(), "segusum-after-action-catalog-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            const string value = "Dialogo già tradotto spostato da C# a SEG.";
            File.WriteAllText(Path.Combine(root, "AfterAction.seg"), $"world game\nafter-action-executed:\n    olivia: {value}\nend\n");
            var source = new DslSourceStringExtractor().Extract(root).Select(x => x.Value).ToArray();
            var result = new TranslationCatalogSynchronizer().Synchronize(source, Catalog((value, "Already translated", true, null)));
            var entry = Assert.Single(result.Document.Root!.Elements("str"));
            Assert.Equal(value, entry.Attribute("orig")!.Value);
            Assert.Equal("Already translated", entry.Attribute("transl")!.Value);
            Assert.Null(entry.Attribute("obsolete"));
            Assert.Equal(1, result.Statistics.Reactivated);
            Assert.Empty(result.Statistics.NewlyObsoleteStrings);
        }
        finally { Directory.Delete(root, true); }
    }

    [Fact]
    public void NewStringIsInsertedInCanonicalOrderAsPlus()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "A", "B", "X", "C" }, Catalog(("A", "a"), ("B", "b"), ("C", "c")));
        Assert.Equal(new[] { "A", "B", "X", "C" }, Originals(result.Document));
        Assert.Equal("+", result.Document.Root!.Elements("str").ElementAt(2).Attribute("transl")!.Value);
    }

    [Fact]
    public void SimilarReplacementKeepsOldTranslatedEntryImmediatelyAfterNew()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "A", "Non mi sembra il caso.", "C" }, Catalog(("A", "a"), ("Non mi semba il caso.", "old translation"), ("C", "c")));
        var entries = result.Document.Root!.Elements("str").ToList();
        var index = entries.FindIndex(x => x.Attribute("orig")!.Value == "Non mi sembra il caso.");
        Assert.Equal("+", entries[index].Attribute("transl")!.Value);
        Assert.Equal("Non mi semba il caso.", entries[index + 1].Attribute("orig")!.Value);
        Assert.Equal("true", entries[index + 1].Attribute("obsolete")!.Value);
        Assert.Single(result.Statistics.ChangedPairs);
    }

    [Fact]
    public void ObsoletePlusIsRemovedButTranslatedObsoleteIsKept()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "A" }, Catalog(("A", "a"), ("Old plus", "+"), ("Old translated", "old")));
        var originals = Originals(result.Document);
        Assert.DoesNotContain("Old plus", originals);
        Assert.Contains("Old translated", originals);
    }

    [Fact]
    public void ReactivatedEntryPreservesUnknownAttributes()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "A" }, Catalog(("A", "a", true, "7")));
        var entry = result.Document.Root!.Elements("str").Single();
        Assert.Null(entry.Attribute("obsolete"));
        Assert.Equal("7", entry.Attribute("metadata")!.Value);
    }

    [Fact]
    public void HintsRemainPartOfIdentityAndDoNotAppearInTargetAutomatically()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "Sì.[[contestoA]]" }, Catalog(("Sì.[[contestoA]]", "+")));
        Assert.Equal("Sì.[[contestoA]]", result.Document.Root!.Elements("str").Single().Attribute("orig")!.Value);
        Assert.Equal("+", result.Document.Root!.Elements("str").Single().Attribute("transl")!.Value);
    }

    [Fact]
    public void ExactOriginalsAreMatchedEvenWhenCatalogOrderDiffers()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "A", "B", "C" }, Catalog(("B", "translated B"), ("A", "translated A"), ("C", "translated C")));
        Assert.Equal(new[] { "A", "B", "C" }, Originals(result.Document));
        Assert.Equal(3, result.Statistics.Unchanged);
    }

    [Fact]
    public void AmbiguousFuzzyMatchIsLeftUnpaired()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "Nuova frase simile alpha", "Nuova frase simile beta" }, Catalog(("Vecchia frase simile", "old")));
        Assert.Empty(result.Statistics.ChangedPairs);
        Assert.Contains(result.Document.Root!.Elements("str"), x => x.Attribute("obsolete")?.Value == "true");
    }

    [Fact]
    public void TranslatedLineageSurvivesTwoUntranslatedRevisions()
    {
        var v1 = "Perche il cancello è chiuso?";
        var v2 = "Perché il cancello è chiuso?";
        var v3 = "Perché il cancello è ancora chiuso?";
        var v4 = "Perché il cancello è ancora ben chiuso?";

        var first = new TranslationCatalogSynchronizer().Synchronize(new[] { v2 }, Catalog((v1, "Why is the gate closed?")));
        Assert.Equal(new[] { v2, v1 }, Originals(first.Document));
        Assert.Equal("true", first.Document.Root!.Elements("str").ElementAt(1).Attribute("obsolete")!.Value);

        var second = new TranslationCatalogSynchronizer().Synchronize(new[] { v3 }, first.Document);
        var entries = second.Document.Root!.Elements("str").ToList();
        Assert.Equal(new[] { v3, v1 }, Originals(second.Document));
        Assert.Equal("+", entries[0].Attribute("transl")!.Value);
        Assert.Equal("Why is the gate closed?", entries[1].Attribute("transl")!.Value);
        Assert.Equal(entries[0].Attribute("translation-chain")!.Value, entries[1].Attribute("translation-chain")!.Value);
        Assert.Equal(v1, entries[0].Attribute("previous-translated-orig")!.Value);

        var third = new TranslationCatalogSynchronizer().Synchronize(new[] { v4 }, second.Document);
        Assert.Equal(new[] { v4, v1 }, Originals(third.Document));
        var repeated = new TranslationCatalogSynchronizer().Synchronize(new[] { v4 }, third.Document);
        Assert.Equal(new[] { v4, v1 }, Originals(repeated.Document));
    }

    [Fact]
    public void LatestTranslatedRevisionBecomesPreviousTranslation()
    {
        var v1 = "Perche il cancello è chiuso?";
        var v2 = "Perché il cancello è chiuso?";
        var v3 = "Perché il cancello è ancora chiuso?";
        var first = new TranslationCatalogSynchronizer().Synchronize(new[] { v2 }, Catalog((v1, "Why is the gate closed?")));
        var v2Translated = new XDocument(first.Document);
        v2Translated.Root!.Elements("str").First().SetAttributeValue("transl", "Why is the gate still closed?");

        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { v3 }, v2Translated);
        var entries = result.Document.Root!.Elements("str").ToList();
        Assert.Equal(new[] { v3, v2, v1 }, Originals(result.Document));
        Assert.Equal("Why is the gate still closed?", entries[1].Attribute("transl")!.Value);
        Assert.Equal(v2, entries[0].Attribute("previous-translated-orig")!.Value);
    }

    [Fact]
    public void LegacyMaterializedPairIsNotFuzzyRecoveredWithoutLineage()
    {
        var v1 = "Perche il cancello è chiuso?";
        var v2 = "Perché il cancello è chiuso?";
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { v2 }, Catalog((v2, "+", false, (string?)null), (v1, "Why is the gate closed?", true, null)));

        Assert.Equal(new[] { v2, v1 }, Originals(result.Document));
        Assert.Empty(result.Statistics.ChangedPairs);
    }

    [Fact]
    public void ExactReactivationWinsAndDropsUntranslatedIntermediateRevision()
    {
        var v1 = "Perche il cancello è chiuso?";
        var v2 = "Perché il cancello è chiuso?";
        var first = new TranslationCatalogSynchronizer().Synchronize(new[] { v2 }, Catalog((v1, "Why is the gate closed?")));

        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { v1 }, first.Document);

        var entries = result.Document.Root!.Elements("str").ToList();
        Assert.Single(entries);
        Assert.Equal(v1, entries[0].Attribute("orig")!.Value);
        Assert.Equal("Why is the gate closed?", entries[0].Attribute("transl")!.Value);
        Assert.Null(entries[0].Attribute("obsolete"));
    }

    [Fact]
    public void TrailingWhitespaceFallbackReusesTranslationAndCanonicalizesOriginal()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "testo" }, Catalog(("testo ", "translation")));
        var entry = Assert.Single(result.Document.Root!.Elements("str"));
        Assert.Equal("testo", entry.Attribute("orig")!.Value);
        Assert.Equal("translation", entry.Attribute("transl")!.Value);
        Assert.Equal(0, result.Statistics.NewlyUntranslated);
        Assert.Equal(0, result.Statistics.NewlyObsolete);
    }

    [Fact]
    public void LeadingWhitespaceFallbackReusesTranslationAndCanonicalizesOriginal()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "testo" }, Catalog(("  testo", "translation")));
        var entry = Assert.Single(result.Document.Root!.Elements("str"));
        Assert.Equal("testo", entry.Attribute("orig")!.Value);
        Assert.Equal("translation", entry.Attribute("transl")!.Value);
        Assert.Equal(0, result.Statistics.NewlyUntranslated);
        Assert.Equal(0, result.Statistics.NewlyObsolete);
    }

    [Fact]
    public void InternalWhitespaceIsNotNormalized()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "ciao mondo" }, Catalog(("ciao  mondo", "translation")));
        Assert.Equal(new[] { "ciao mondo", "ciao  mondo" }, Originals(result.Document));
        Assert.Equal("+", result.Document.Root!.Elements("str").First().Attribute("transl")!.Value);
    }

    [Fact]
    public void AmbiguousTrimmedMatchesAreNotReusedArbitrarily()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "testo" }, Catalog(("testo ", "first"), (" testo", "second")));
        var entries = result.Document.Root!.Elements("str").ToArray();
        Assert.Equal("+", entries[0].Attribute("transl")!.Value);
        Assert.Contains(entries, x => x.Attribute("transl")!.Value == "first" && x.Attribute("obsolete")?.Value == "true");
        Assert.Contains(entries, x => x.Attribute("transl")!.Value == "second" && x.Attribute("obsolete")?.Value == "true");
    }

    [Fact]
    public void SynchronizationStatisticsCountOnlyNewTransitions()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "Current", "New" }, Catalog(("Current", "translation"), ("Removed", "old translation")));
        Assert.Equal(1, result.Statistics.NewlyUntranslated);
        Assert.Equal(1, result.Statistics.NewlyObsolete);
    }

    [Fact]
    public void NewStringPersistsTransitionMetadataAndRootSyncMetadata()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "New" }, new XDocument(new XElement("root")));
        var entry = Assert.Single(result.Document.Root!.Elements("str"));
        Assert.Equal("+", entry.Attribute("transl")!.Value);
        Assert.Equal("new", entry.Attribute("sync-status")!.Value);
        Assert.Equal(result.Statistics.SyncId, entry.Attribute("sync-id")!.Value);
        Assert.Equal(result.Statistics.SyncAt, entry.Attribute("sync-at")!.Value);
        Assert.Equal(new[] { "New" }, result.Statistics.NewlyUntranslatedStrings);
        Assert.Equal(result.Statistics.SyncId, result.Document.Root.Attribute("last-sync-id")!.Value);
        Assert.Equal(result.Statistics.SyncAt, result.Document.Root.Attribute("last-sync-at")!.Value);
    }

    [Fact]
    public void RemovedTranslatedStringIsMarkedObsolete()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(Array.Empty<string>(), Catalog(("Removed", "translation")));
        var entry = Assert.Single(result.Document.Root!.Elements("str"));
        Assert.Equal("true", entry.Attribute("obsolete")!.Value);
        Assert.Equal("obsolete", entry.Attribute("sync-status")!.Value);
        Assert.Equal(new[] { "Removed" }, result.Statistics.NewlyObsoleteStrings);
    }

    [Fact]
    public void AlreadyObsoleteStringDoesNotReceiveCurrentMarker()
    {
        var current = new XDocument(new XElement("root", new XElement("str",
            new XAttribute("orig", "Old"), new XAttribute("transl", "translation"),
            new XAttribute("obsolete", "true"), new XAttribute("sync-id", "previous"))));
        var result = new TranslationCatalogSynchronizer().Synchronize(Array.Empty<string>(), current);
        var entry = Assert.Single(result.Document.Root!.Elements("str"));
        Assert.Equal("previous", entry.Attribute("sync-id")!.Value);
        Assert.Empty(result.Statistics.NewlyObsoleteStrings);
    }

    [Fact]
    public void TrimFallbackIsCanonicalizedWithoutNewOrObsoleteTransition()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "testo" }, Catalog(("testo ", "translation")));
        var entry = Assert.Single(result.Document.Root!.Elements("str"));
        Assert.Equal("testo", entry.Attribute("orig")!.Value);
        Assert.Equal("translation", entry.Attribute("transl")!.Value);
        Assert.Equal("canonicalized", entry.Attribute("sync-status")!.Value);
        Assert.Empty(result.Statistics.NewlyUntranslatedStrings);
        Assert.Empty(result.Statistics.NewlyObsoleteStrings);
        Assert.Equal(new[] { "testo" }, result.Statistics.CanonicalizedStrings);
    }

    [Fact]
    public void ObsoleteStringCanBeReactivatedWithMetadata()
    {
        var current = new XDocument(new XElement("root", new XElement("str",
            new XAttribute("orig", "Text"), new XAttribute("transl", "translation"), new XAttribute("obsolete", "true"))));
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "Text" }, current);
        var entry = Assert.Single(result.Document.Root!.Elements("str"));
        Assert.Null(entry.Attribute("obsolete"));
        Assert.Equal("reactivated", entry.Attribute("sync-status")!.Value);
        Assert.Equal(new[] { "Text" }, result.Statistics.ReactivatedStrings);
    }

    [Fact]
    public void FuzzyReplacementMarksBothTransitions()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(
            new[] { "Camilla corre nella stanza." }, Catalog(("Camilla va nella stanza.", "translated")));
        var entries = result.Document.Root!.Elements("str").ToList();
        var current = Assert.Single(entries.Where(x => x.Attribute("orig")!.Value == "Camilla corre nella stanza."));
        var old = Assert.Single(entries.Where(x => x.Attribute("orig")!.Value == "Camilla va nella stanza."));
        Assert.Equal("new", current.Attribute("sync-status")!.Value);
        Assert.Equal("obsolete", old.Attribute("sync-status")!.Value);
        Assert.Equal(1, result.Statistics.NewlyUntranslated);
        Assert.Equal(1, result.Statistics.NewlyObsolete);
    }

    [Fact]
    public void UnchangedSynchronizationDoesNotChangeRootOrMarkers()
    {
        var current = new XDocument(new XElement("root", new XAttribute("last-sync-id", "stable"),
            new XAttribute("last-sync-at", "old"), new XElement("str", new XAttribute("orig", "A"),
            new XAttribute("transl", "a"), new XAttribute("sync-status", "canonicalized"), new XAttribute("sync-id", "entry"))));
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "A" }, current);
        Assert.False(result.Changed);
        Assert.Equal("stable", result.Document.Root!.Attribute("last-sync-id")!.Value);
        Assert.Equal("entry", result.Document.Root.Element("str")!.Attribute("sync-id")!.Value);
    }

    [Fact]
    public void SyncMetadataDoesNotPreventExactMatchingAndDuplicatesAreReported()
    {
        var current = new XDocument(new XElement("root",
            new XElement("str", new XAttribute("orig", "Text"), new XAttribute("transl", "active"), new XAttribute("sync-status", "new")),
            new XElement("str", new XAttribute("orig", "Text"), new XAttribute("transl", "old"), new XAttribute("obsolete", "true"))));
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "Text" }, current);
        Assert.Equal("active", result.Document.Root!.Elements("str").First().Attribute("transl")!.Value);
        Assert.Contains("Text", result.Statistics.DuplicateActiveObsoleteStrings);
    }

    [Fact]
    public void ConsolidatesUntranslatedActiveWithTranslatedObsolete()
    {
        var original = "Ti sconfiggeremo, Mike Stallone!";
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { original }, DuplicateCatalog(
            (original, "+", false), (original, "We will defeat you, Mike Stallone!", true)));
        var entry = Assert.Single(result.Document.Root!.Elements("str"));
        Assert.Equal("We will defeat you, Mike Stallone!", entry.Attribute("transl")!.Value);
        Assert.Null(entry.Attribute("obsolete"));
        Assert.Equal(new[] { original }, result.Statistics.ConsolidatedActiveObsoleteStrings);
        Assert.Empty(result.Statistics.ConflictingActiveObsoleteStrings);
        Assert.Equal(0, result.Statistics.NewlyUntranslated);
        Assert.Equal(0, result.Statistics.NewlyObsolete);
        Assert.Equal(0, result.Statistics.Reactivated);
        Assert.Empty(result.Statistics.CanonicalizedStrings);
    }

    [Fact]
    public void ConsolidatesActiveAndObsoleteWithSameTranslation()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "X" }, DuplicateCatalog(
            ("X", "Translation", false), ("X", "Translation", true)));
        Assert.Single(result.Document.Root!.Elements("str"));
        Assert.Single(result.Statistics.ConsolidatedActiveObsoleteStrings);
        Assert.Empty(result.Statistics.DuplicateActiveObsoleteStrings);
    }

    [Fact]
    public void KeepsDifferentActiveAndObsoleteTranslationsAndReportsConflict()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "X" }, DuplicateCatalog(
            ("X", "Translation A", false), ("X", "Translation B", true)));
        Assert.Equal(2, result.Document.Root!.Elements("str").Count());
        var conflict = Assert.Single(result.Statistics.ConflictingActiveObsoleteStrings);
        Assert.Equal("X", conflict.Original);
        Assert.Equal("Translation A", conflict.ActiveTranslation);
        Assert.Equal(new[] { "Translation B" }, conflict.ObsoleteTranslations);
        Assert.Contains("X", result.Statistics.DuplicateActiveObsoleteStrings);
    }

    [Fact]
    public void ConsolidatesMultipleObsoleteEntriesWhenTheirTranslationIsUnique()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "X" }, DuplicateCatalog(
            ("X", "+", false), ("X", "Translation", true), ("X", "Translation", true)));
        var entry = Assert.Single(result.Document.Root!.Elements("str"));
        Assert.Equal("Translation", entry.Attribute("transl")!.Value);
        Assert.Empty(result.Statistics.ConflictingActiveObsoleteStrings);
    }

    [Fact]
    public void DoesNotChooseAmongDifferentObsoleteTranslations()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "X" }, DuplicateCatalog(
            ("X", "+", false), ("X", "Translation A", true), ("X", "Translation B", true)));
        Assert.Equal(3, result.Document.Root!.Elements("str").Count());
        Assert.Empty(result.Statistics.ConsolidatedActiveObsoleteStrings);
        Assert.Contains("X", result.Statistics.DuplicateActiveObsoleteStrings);
        Assert.Single(result.Statistics.ConflictingActiveObsoleteStrings);
    }

    [Fact]
    public void ConsolidationIsIdempotentAndDoesNotCreateTransitionStatistics()
    {
        var first = new TranslationCatalogSynchronizer().Synchronize(new[] { "X" }, DuplicateCatalog(
            ("X", "+", false), ("X", "Translation", true)));
        var second = new TranslationCatalogSynchronizer().Synchronize(new[] { "X" }, first.Document);
        Assert.False(second.Changed);
        Assert.Empty(second.Statistics.ConsolidatedActiveObsoleteStrings);
        Assert.Equal(0, second.Statistics.NewlyUntranslated);
        Assert.Equal(0, second.Statistics.NewlyObsolete);
        Assert.Equal(0, second.Statistics.Reactivated);
        Assert.Empty(second.Statistics.CanonicalizedStrings);
        Assert.Equal(first.Document.Root!.Attribute("last-sync-id")!.Value, second.Document.Root!.Attribute("last-sync-id")!.Value);
    }

    [Fact]
    public void DifferentOriginalObsoleteEntryRemainsPreserved()
    {
        var result = new TranslationCatalogSynchronizer().Synchronize(new[] { "Current" },
            DuplicateCatalog(("Current", "Translation", false), ("Old", "Old translation", true)));
        Assert.Equal(new[] { "Current", "Old" }, Originals(result.Document));
        Assert.Empty(result.Statistics.ConsolidatedActiveObsoleteStrings);
    }

    private static XDocument Catalog(params (string Original, string Translation)[] values) => Catalog(values.Select(x => (x.Original, x.Translation, false, (string?)null)).ToArray());
    private static XDocument Catalog(params (string Original, string Translation, bool Obsolete, string? Metadata)[] values) => new(new XElement("root", values.Select(x => new XElement("str", new XAttribute("orig", x.Original), new XAttribute("transl", x.Translation), x.Obsolete ? new XAttribute("obsolete", "true") : null, x.Metadata is null ? null : new XAttribute("metadata", x.Metadata)))));
    private static XDocument DuplicateCatalog(params (string Original, string Translation, bool Obsolete)[] values) => new(new XElement("root", values.Select(x => new XElement("str", new XAttribute("orig", x.Original), new XAttribute("transl", x.Translation), x.Obsolete ? new XAttribute("obsolete", "true") : null))));
    private static string[] Originals(XDocument document) => document.Root!.Elements("str").Select(x => x.Attribute("orig")!.Value).ToArray();
}
