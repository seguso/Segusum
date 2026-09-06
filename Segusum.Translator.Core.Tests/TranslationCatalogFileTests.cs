namespace Segusum.Translator.Core.Tests;

public sealed class TranslationCatalogFileTests
{
    [Fact]
    public void DiscoverIncludesSourceCatalogsButExcludesBinAndObjSegments()
    {
        var root = Path.Combine(Path.GetTempPath(), "segusum-translator-" + Guid.NewGuid().ToString("N"));
        try
        {
            Directory.CreateDirectory(Path.Combine(root, "WebApiLitGir"));
            Directory.CreateDirectory(Path.Combine(root, "WebApiLitGir", "bin", "Debug", "net10.0"));
            Directory.CreateDirectory(Path.Combine(root, "WebApiLitGir", "obj", "Debug", "net10.0"));
            File.WriteAllText(Path.Combine(root, "WebApiLitGir", "transl_en.xml"), "<root />");
            File.WriteAllText(Path.Combine(root, "WebApiLitGir", "bin", "Debug", "net10.0", "transl_en.xml"), "<root />");
            File.WriteAllText(Path.Combine(root, "WebApiLitGir", "obj", "Debug", "net10.0", "transl_de.xml"), "<root />");

            var catalogs = TranslationCatalogFile.Discover(root);

            var catalog = Assert.Single(catalogs);
            Assert.Equal("en", catalog.Language);
            Assert.EndsWith(Path.Combine("WebApiLitGir", "transl_en.xml"), catalog.Path, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }
}
