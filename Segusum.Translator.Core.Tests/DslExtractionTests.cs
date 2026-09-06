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

    [Fact]
    public void ExtractsNarrativeTextFromNormalSegCallsUsingCentralCallSemantics()
    {
        const string dsl = """
world game
def scene:
    narRoom "La guardia vede te e Camilla che uscite dal cancello." roomCastelloDavantiGate removeIfLast:false
    narRoom "Arrivi nel bagno di Dracula." roomBagnoDracula removeIfLast:false
end
""";
        var extracted = Extract(dsl);
        Assert.Equal(new[]
        {
            "La guardia vede te e Camilla che uscite dal cancello.",
            "Arrivi nel bagno di Dracula."
        }, extracted.Select(x => x.Value));
    }

    [Fact]
    public void DecodesEscapedSegLiteralToTheExistingCatalogCanonicalValue()
    {
        const string dsl = """
world game
def scene:
    narRoom "C'è un biglietto sulla porta con scritto \"Sono andata a cogliere funghi velenosi. Torno più tardi.\"" room
end
""";
        var extracted = Extract(dsl);
        Assert.Equal("C'è un biglietto sulla porta con scritto ''Sono andata a cogliere funghi velenosi. Torno più tardi.''", Assert.Single(extracted).Value);
    }

    [Fact]
    public void DecodesBackslashAndPreservesApostropheInNormalLiteral()
    {
        const string dsl = """
world game
def scene:
    narText "L'apostrofo e il percorso \\tmp restano corretti"
end
""";
        Assert.Equal("L'apostrofo e il percorso \\tmp restano corretti", Assert.Single(Extract(dsl)).Value);
    }

    private static IReadOnlyList<SourceString> Extract(string dsl)
    {
        var root = Path.Combine(Path.GetTempPath(), "segusum-dsl-calls-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "fixture.seg"), dsl);
            return new DslSourceStringExtractor().Extract(root);
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
