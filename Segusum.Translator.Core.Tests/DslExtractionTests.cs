using Segusum.Scripting.Core;
using Segusum.Translator.Core;

namespace Segusum.Translator.Core.Tests;

public sealed class DslExtractionTests
{
    [Fact]
    public void ParserRecognizesStateFunctionHandlerAndCycleElement()
    {
        var source = new DslSource("demo.seg", "state attempts: int = 0\ndef check object: LogicObj ret bool:\n    ret true\nend\ncombine a with b:\n    phrase \"A\"\nend\nadd cyc stable-id important\n    when it not-seen-recently 5\nend");
        var result = DslParser.Parse(source);
        Assert.Empty(result.Diagnostics);
        Assert.Equal(4, result.Document.Declarations.Count);
    }

    [Fact]
    public void DuplicateCycleIdsAreVisibleToGeneratorModel()
    {
        var source = new DslSource("demo.seg", "add cyc same\nend\nadd cyc same\nend");
        var result = DslParser.Parse(source);
        var ids = result.Document.Declarations.OfType<CycleElementDeclaration>().Select(x => x.Id).ToArray();
        Assert.Equal(new[] { "same", "same" }, ids);
    }
}
