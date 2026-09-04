using System.Xml.Linq;
using Segusum.Translator.Core;
using Segusum.WebClient;

namespace Segusum.Translator.Core.Tests;

public sealed class ClientCatalogOwnershipTests
{
    [Fact]
    public void ConsumerSyncDoesNotMaterializeEngineClientCatalog()
    {
        var engine = SegusumClientCatalog.Load("en");
        var root = CreateConsumer("class World { void Configure() { } }");
        try
        {
            var catalogPath = Path.Combine(root, "transl_en.xml");
            new TranslationCatalogOperations().Synchronize(root, catalogPath);
            var document = XDocument.Load(catalogPath);
            Assert.DoesNotContain(document.Root!.Elements("str"), x => (string?)x.Attribute("orig") == engine["saveGame"].Source);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ConsumerSyncContainsOnlyExplicitOverrideSource()
    {
        var engine = SegusumClientCatalog.Load("en");
        var root = CreateConsumer("class World { void Configure() { options.OverrideClientString(\"saveGame\", \"Store your progress\"); } }");
        try
        {
            var catalogPath = Path.Combine(root, "transl_en.xml");
            new TranslationCatalogOperations().Synchronize(root, catalogPath);
            var originals = XDocument.Load(catalogPath).Root!.Elements("str")
                .Select(x => (string)x.Attribute("orig")!).ToArray();
            Assert.Contains("Store your progress", originals);
            Assert.DoesNotContain(engine["saveGame"].Source, originals);
            Assert.Equal("Save game", engine["saveGame"].Source);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    [Fact]
    public void ConsumerOverrideFallsBackFromGermanToItalianBeforeItsSource()
    {
        var root = CreateConsumer("class World { void Configure() { options.OverrideClientString(\"saveGame\", \"Store your progress\"); } }\n");
        try
        {
            File.WriteAllText(Path.Combine(root, "transl_de.xml"), "<root><str orig=\"Store your progress\" transl=\"+\" /></root>");
            File.WriteAllText(Path.Combine(root, "transl_it.xml"), "<root><str orig=\"Store your progress\" transl=\"Salva i progressi\" /></root>");

            var resolved = SegusumClientCatalog.Resolve("de",
                new Dictionary<string, string> { ["saveGame"] = "Store your progress" }, root);

            Assert.Equal("Salva i progressi", resolved["saveGame"]);
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string CreateConsumer(string source)
    {
        var root = Path.Combine(Path.GetTempPath(), "segusum-consumer-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        File.WriteAllText(Path.Combine(root, "World.cs"), source);
        File.WriteAllText(Path.Combine(root, "transl_en.xml"), "<root />");
        return root;
    }
}
