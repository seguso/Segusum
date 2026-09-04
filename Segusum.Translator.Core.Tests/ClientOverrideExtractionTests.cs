using Segusum.Translator.Core;

namespace Segusum.Translator.Core.Tests;

public sealed class ClientOverrideExtractionTests
{
    [Fact]
    public void OverrideClientStringExtractsOnlyTheSourceArgument()
    {
        var root = Path.Combine(Path.GetTempPath(), "segusum-client-override-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "World.cs"),
                "class World { void Configure() { options.OverrideClientString(\"saveGame\", \"Store your progress\"); } }");
            var result = new SourceStringExtractor().Extract(root);
            var source = Assert.Single(result);
            Assert.Equal("Store your progress", source.Value);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
