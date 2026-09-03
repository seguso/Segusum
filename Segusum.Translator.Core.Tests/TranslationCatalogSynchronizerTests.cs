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

    private static XDocument Catalog(params (string Original, string Translation)[] values) => Catalog(values.Select(x => (x.Original, x.Translation, false, (string?)null)).ToArray());
    private static XDocument Catalog(params (string Original, string Translation, bool Obsolete, string? Metadata)[] values) => new(new XElement("root", values.Select(x => new XElement("str", new XAttribute("orig", x.Original), new XAttribute("transl", x.Translation), x.Obsolete ? new XAttribute("obsolete", "true") : null, x.Metadata is null ? null : new XAttribute("metadata", x.Metadata)))));
    private static string[] Originals(XDocument document) => document.Root!.Elements("str").Select(x => x.Attribute("orig")!.Value).ToArray();
}
