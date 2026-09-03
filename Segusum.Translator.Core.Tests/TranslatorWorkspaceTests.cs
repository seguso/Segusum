using System.Xml.Linq;
using Segusum.Translator.Core;

namespace Segusum.Translator.Core.Tests;

public sealed class TranslatorWorkspaceTests
{
    [Fact]
    public void RoslynExtractsMarkerArgumentsAndTranslatableLiteralOnly()
    {
        using var project = NewProject("World.cs", """
            dial(character, "Dialogo");
            dial
            (
                character, "Dialogo multilinea"
            );
            nar("Narrazione");
            narText("Testo narrativo");
            narImg("Didascalia", "image.png");
            narRoom("Stanza", room);
            addHandlerCombine(first, second, "Usa oggetto");
            addHandlerLook(first, "Guarda oggetto");
            fatinaDiceQui(cs, room, "La fatina parla");
            "Nome stanza".translatable();
            using (namedCutScene(scene, room)) { narText("Nel corpo"); }
            """
        );

        var values = new SourceStringExtractor().Extract(project.Path).Select(x => x.Value).ToArray();
        Assert.Equal(new[] { "Dialogo", "Dialogo multilinea", "Narrazione", "Testo narrativo", "Didascalia", "Stanza", "Usa oggetto", "Guarda oggetto", "La fatina parla", "Nome stanza", "Nel corpo" }, values);
    }

    [Fact]
    public void RoslynIgnoresCommentsAndStringsContainingCode()
    {
        using var project = NewProject("World.cs", """
            var a = "dial(";
            var b = "narText(\\"ciao\\")";
            var c = "{\\"codeSnapshot\\":\\"dial();\\"}";
            var d = "if (x)\\n{\\n dial(...);\\n}";
            // dial("commento")
            /* nar("commento") */
            """
        );

        Assert.Empty(new SourceStringExtractor().Extract(project.Path));
    }

    [Fact]
    public void RoslynSupportsVerbatimRawAndMultilineInvocationFormatting()
    {
        using var project = NewProject("World.cs", "dial\n(character, @\"Verbatim\");\nnarText(\"\"\"Raw text\"\"\");\n");

        Assert.Equal(new[] { "Verbatim", "Raw text" }, new SourceStringExtractor().Extract(project.Path).Select(x => x.Value));
    }

    [Fact]
    public void DiscoveryScansCsAndHonorsIncludeExcludeConfiguration()
    {
        using var project = NewProject("Game/World.cs", "// test\ndial(character, \"Ciao\");\n");
        var root = project.Path;
        Directory.CreateDirectory(Path.Combine(root, "Tests"));
        File.WriteAllText(Path.Combine(root, "Tests", "Ignored.cs"), "dial(character, \"Da ignorare\");");
        var extractor = new SourceStringExtractor();

        Assert.Equal(2, extractor.Extract(root).Count);
        var configured = extractor.Extract(root, options: new SourceDiscoveryOptions { Include = new[] { "Game" }, Exclude = new[] { "Tests" } });
        Assert.Equal("Ciao", Assert.Single(configured).Value);
    }

    [Fact]
    public void WorkspaceKeepsSequenceObsoleteAndPlusAndSavesOnlyTargetCatalog()
    {
        using var project = NewProject("World.cs", "dial(character, \"Nuova frase\");\n");
        var root = project.Path;
        var path = Path.Combine(root, "transl_en.xml");
        File.WriteAllText(path, "<root><str orig=\"Vecchia frase\" transl=\"Old translation\" obsolete=\"true\" /></root>");
        var workspace = new TranslationWorkspace { RepositoryRoot = root, CatalogPath = path };

        workspace.Synchronize();
        Assert.Equal(new[] { "Nuova frase", "Vecchia frase" }, workspace.Items.Select(x => x.SourceText));
        Assert.Equal("+", workspace.Items[0].TargetTranslation);
        Assert.True(workspace.Items[1].IsObsolete);
        workspace.SetTranslation(0, "New translation");
        workspace.Save();

        var saved = XDocument.Load(path);
        Assert.Equal("New translation", saved.Root!.Elements("str").First().Attribute("transl")!.Value);
        Assert.Equal("true", saved.Root.Elements("str").Last().Attribute("obsolete")!.Value);
    }

    [Fact]
    public void SaveRejectsExternalCatalogChange()
    {
        using var project = NewProject("World.cs", "dial(character, \"One\");\n");
        var root = project.Path;
        var path = Path.Combine(root, "transl_en.xml");
        File.WriteAllText(path, "<root><str orig=\"One\" transl=\"+\" /></root>");
        var workspace = new TranslationWorkspace { RepositoryRoot = root, CatalogPath = path };
        workspace.Load();
        workspace.SetTranslation(0, "Uno");
        File.AppendAllText(path, "\n");
        Assert.Throws<IOException>(() => workspace.Save());
    }

    [Fact]
    public void LoadDoesNotSynchronizeOrWriteCatalog()
    {
        using var project = NewProject("World.cs", "dial(character, \"New source\");\n");
        var path = Path.Combine(project.Path, "transl_en.xml");
        File.WriteAllText(path, "<root><str orig=\"Old source\" transl=\"Old\" /></root>");
        var before = File.ReadAllText(path);
        var workspace = new TranslationWorkspace { RepositoryRoot = project.Path, CatalogPath = path };

        workspace.Load();

        Assert.Equal(before, File.ReadAllText(path));
        Assert.Equal("Old source", Assert.Single(workspace.Items).SourceText);
    }

    [Fact]
    public void CreateCatalogUsesCurrentSourcesAndPlus()
    {
        using var project = NewProject("World.cs", "dial(character, \"One\");\ndial(character, \"Two\");\n");
        var result = new TranslationCatalogOperations().Create(project.Path, "fr");
        var path = Path.Combine(project.Path, "transl_fr.xml");

        Assert.True(result.Created);
        Assert.True(File.Exists(path));
        var entries = XDocument.Load(path).Root!.Elements("str").ToArray();
        Assert.Equal(new[] { "One", "Two" }, entries.Select(x => (string)x.Attribute("orig")!));
        Assert.All(entries, x => Assert.Equal("+", (string)x.Attribute("transl")!));
        Assert.Throws<IOException>(() => new TranslationCatalogOperations().Create(project.Path, "fr"));
    }

    [Fact]
    public void SynchronizeIsIdempotentAndPersistsChangesExplicitly()
    {
        using var project = NewProject("World.cs", "dial(character, \"One\");\n");
        var path = Path.Combine(project.Path, "transl_en.xml");
        File.WriteAllText(path, "<root><str orig=\"One\" transl=\"+\" /></root>");
        var operations = new TranslationCatalogOperations();

        Assert.False(operations.Synchronize(project.Path, path).Result.Changed);
        Assert.False(operations.Synchronize(project.Path, path).Result.Changed);
    }

    [Fact]
    public void WorkspaceExposesPersistedPreviousTranslationAfterLaterRevision()
    {
        using var project = NewProject("World.cs", "dial(character, \"Version 2\");\n");
        var path = Path.Combine(project.Path, "transl_en.xml");
        File.WriteAllText(path, "<root><str orig=\"Version 1\" transl=\"Versione uno\" /></root>");
        var workspace = new TranslationWorkspace { RepositoryRoot = project.Path, CatalogPath = path };

        workspace.Synchronize();
        workspace.SetTranslation(0, "Versione due");
        workspace.Save();
        File.WriteAllText(Path.Combine(project.Path, "World.cs"), "dial(character, \"Version 3\");\n");

        workspace.Synchronize();

        Assert.Equal(new[] { "Version 3", "Version 2", "Version 1" }, workspace.Items.Select(x => x.SourceText));
        Assert.Equal("Versione due", workspace.Items[0].RelatedOldTranslation);
    }

    private static TempProject NewProject(string relativeFile, string contents)
    {
        var root = Directory.CreateTempSubdirectory("segusum-translator-").FullName;
        var path = Path.Combine(root, relativeFile.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, contents);
        return new TempProject(root);
    }

    private sealed class TempProject(string path) : IDisposable
    {
        public string Path { get; } = path;
        public void Dispose() => Directory.Delete(Path, true);
    }
}
