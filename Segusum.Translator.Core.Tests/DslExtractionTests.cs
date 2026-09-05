using Segusum.Scripting.Core;
using Segusum.Translator.Core;

namespace Segusum.Translator.Core.Tests;

public sealed class DslExtractionTests
{
    [Fact]
    public void ParserRecognizesStateFunctionHandlerAndCycleElement()
    {
        var source = new DslSource("demo.seg", "world game\nstate attempts: int = 0\ndef check object: LogicObj ret bool:\n    ret true\nend\ncombine a with b:\n    phrase \"A\"\nend\nadd cyc stable-id important\n    when it not-seen-recently 5\nend");
        var result = DslParser.Parse(source);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(4, result.Document.Declarations.Count);
    }

    [Fact]
    public void DuplicateCycleIdsAreVisibleToGeneratorModel()
    {
        var source = new DslSource("demo.seg", "world game\nadd cyc same\nend\nadd cyc same\nend");
        var result = DslParser.Parse(source);
        var ids = result.Document.Declarations.OfType<CycleElementDeclaration>().Select(x => x.Id).ToArray();
        Assert.Equal(new[] { "same", "same" }, ids);
    }

    [Fact]
    public void BeforeRoomChangeNarrativeIsExtractedLikeOtherDslBodies()
    {
        var root = Path.Combine(Path.GetTempPath(), "segusum-before-room-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "BeforeRoomChange.seg"), "world game\nbefore-room-change:\n    camilla: Una frase nel cambio stanza.\nend\n");
            var values = new DslSourceStringExtractor().Extract(root).Select(x => x.Value).ToArray();
            Assert.Equal(new[] { "Una frase nel cambio stanza." }, values);
        }
        finally { Directory.Delete(root, true); }
    }
}
