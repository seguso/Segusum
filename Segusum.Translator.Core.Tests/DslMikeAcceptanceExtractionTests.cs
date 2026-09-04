using Segusum.Translator.Core;

namespace Segusum.Translator.Core.Tests;

public sealed class DslMikeAcceptanceExtractionTests
{
    [Fact]
    public void ExtractsAllNineMikeAcceptanceDialoguesFromDslAst()
    {
        const string dsl = """
world game
def creaCicloMikeNonRipete ret Cycle:
    var cyc = new-cycle
    add cyc cidNonRipete1
        mikeStallone: "No! Mike Stallone non ripete!"
        olivia: "Ma che cavolo, Mike Stallone!"
        mikeStallone: "Voi non capite, bambine! Io sono un eroe leggendario! Ogni pugno che io elargisco è come una piccola poesia! E, come tale, è irripetibile!"
        camilla: "Tu sei uno psicopatico, Mike Stallone! Fatti curare!"
    end
    add cyc cidNonRipete2
        mikeStallone: "No! Mike Stallone non ripete la stessa impresa due volte!"
        olivia: "Ma che cavolo, Mike Stallone! Aiutaci!"
        mikeStallone: "Bambine, voi non capite! Le mie gesta sono uniche e irripetibili!"
        camilla: "Vai a farti friggere, Mike Stallone!"
    end
    ret cyc
end
use mikeStallone for puAiutareLoScemoDiGuerra:
    exp explanation
    if call namedCutSceneIsSeen ncs:
        olivia: "Mike Stallone! Mi aiuti a far rinsavire lo scemo di guerra dandogli una botta in testa come quella che ha avuto in guerra?"
        makes-no-sense
    end
end
""";
        var root = Path.Combine(Path.GetTempPath(), "segusum-dsl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "mike.seg"), dsl);
            var extracted = new DslSourceStringExtractor().Extract(root);
            Assert.Equal(9, extracted.Count);
            Assert.All(extracted, x => Assert.Equal("mike.seg", x.RelativePath));
            Assert.Equal(new[]
            {
                "No! Mike Stallone non ripete!",
                "Ma che cavolo, Mike Stallone!",
                "Voi non capite, bambine! Io sono un eroe leggendario! Ogni pugno che io elargisco è come una piccola poesia! E, come tale, è irripetibile!",
                "Tu sei uno psicopatico, Mike Stallone! Fatti curare!",
                "No! Mike Stallone non ripete la stessa impresa due volte!",
                "Ma che cavolo, Mike Stallone! Aiutaci!",
                "Bambine, voi non capite! Le mie gesta sono uniche e irripetibili!",
                "Vai a farti friggere, Mike Stallone!",
                "Mike Stallone! Mi aiuti a far rinsavire lo scemo di guerra dandogli una botta in testa come quella che ha avuto in guerra?"
            }, extracted.Select(x => x.Value));
            Assert.Equal(4, extracted.Take(4).Select(x => x.LineNumber).Distinct().Count());
            Assert.True(extracted.Max(x => x.LineNumber) > extracted.Min(x => x.LineNumber));
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
