using System.Xml.Linq;
using Segusum.Translator.Core;

namespace Segusum.Translator.Core.Tests;

public sealed class TranslatorWorkspaceTests
{
    [Fact]
    public void DiscoveryScansCsAndHonorsIncludeExcludeConfiguration()
    {
        using var project = NewProject("Game/World.cs", "// test\ndial(\"Ciao\");\n");
        var root = project.Path;
        Directory.CreateDirectory(Path.Combine(root, "Tests"));
        File.WriteAllText(Path.Combine(root, "Tests", "Ignored.cs"), "dial(\"Da ignorare\");");
        var extractor = new SourceStringExtractor();

        Assert.Equal(2, extractor.Extract(root).Count);
        var configured = extractor.Extract(root, options: new SourceDiscoveryOptions { Include = new[] { "Game" }, Exclude = new[] { "Tests" } });
        Assert.Equal("Ciao", Assert.Single(configured).Value);
    }

    [Fact]
    public void WorkspaceKeepsSequenceObsoleteAndPlusAndSavesOnlyTargetCatalog()
    {
        using var project = NewProject("World.cs", "dial(\"Nuova frase\");\n");
        var root = project.Path;
        var path = Path.Combine(root, "transl_en.xml");
        File.WriteAllText(path, "<root><str orig=\"Vecchia frase\" transl=\"Old translation\" obsolete=\"true\" /></root>");
        var workspace = new TranslationWorkspace { RepositoryRoot = root, CatalogPath = path };

        workspace.Load();
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
        using var project = NewProject("World.cs", "dial(\"One\");\n");
        var root = project.Path;
        var path = Path.Combine(root, "transl_en.xml");
        File.WriteAllText(path, "<root><str orig=\"One\" transl=\"+\" /></root>");
        var workspace = new TranslationWorkspace { RepositoryRoot = root, CatalogPath = path };
        workspace.Load();
        workspace.SetTranslation(0, "Uno");
        File.AppendAllText(path, "\n");
        Assert.Throws<IOException>(() => workspace.Save());
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
