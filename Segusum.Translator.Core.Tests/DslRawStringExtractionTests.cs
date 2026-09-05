using Segusum.Translator.Core;

namespace Segusum.Translator.Core.Tests;

public sealed class DslRawStringExtractionTests
{
    [Fact]
    public void ExtractsQuotedAndRawDialogueAndNarration()
    {
        const string dsl = """
world game
use camilla here:
    camilla: "Quoted"
    camilla: Raw dialogue
    nar: Raw narration
    nar-room: Raw room narration
end
""";
        var extracted = Extract(dsl);

        Assert.Equal(new[] { "Quoted", "Raw dialogue", "Raw narration", "Raw room narration" }, extracted.Select(x => x.Value));
    }

    [Fact]
    public void PreservesSourceOrderForMixedQuotedAndRawStrings()
    {
        const string dsl = """
world game
use camilla here:
    nar: First raw
    camilla: "Second quoted"
    nar-room: Third raw
end
""";
        var extracted = Extract(dsl);

        Assert.Equal(new[] { "First raw", "Second quoted", "Third raw" }, extracted.Select(x => x.Value));
        Assert.All(extracted, x => Assert.Equal("fixture.seg", x.RelativePath));
    }

    private static IReadOnlyList<SourceString> Extract(string dsl)
    {
        var root = Path.Combine(Path.GetTempPath(), "segusum-dsl-raw-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "fixture.seg"), dsl);
            return new DslSourceStringExtractor().Extract(root);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
