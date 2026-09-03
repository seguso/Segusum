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

    private static XDocument Catalog(params (string Original, string Translation)[] values) => Catalog(values.Select(x => (x.Original, x.Translation, false, (string?)null)).ToArray());
    private static XDocument Catalog(params (string Original, string Translation, bool Obsolete, string? Metadata)[] values) => new(new XElement("root", values.Select(x => new XElement("str", new XAttribute("orig", x.Original), new XAttribute("transl", x.Translation), x.Obsolete ? new XAttribute("obsolete", "true") : null, x.Metadata is null ? null : new XAttribute("metadata", x.Metadata)))));
    private static string[] Originals(XDocument document) => document.Root!.Elements("str").Select(x => x.Attribute("orig")!.Value).ToArray();
}
