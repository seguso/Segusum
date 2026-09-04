using Segusum.Translator.Core;

namespace Segusum.Translator.Core.Tests;

public sealed class DslLetiziaAcceptanceExtractionTests
{
    [Fact]
    public void ExtractsLetiziaAcceptanceDialoguesFromDslAst()
    {
        const string dsl = """
world game
combine travestitiDa with letiziaDeVille:
    phrase "travestiti da Letizia De Ville per farti vedere da Dracula"
    exp exDaDracula
    if call objectiveIsCurrent puFaiInModoCheDraculaTiAccolga:
        if call namedCutSceneIsSeen ncsLetteraDiDraculaAllaZiaEdwige:
            olivia: "Ho una grande idea, Camilla! Dracula vuole essere famoso!"
            camilla: "Cosa?"
            olivia: "Presto, Letizia! Si spogli!"
            letiziaDeVille: "Ma che stai dicendo, bambina?"
            olivia: "Ah, già! È vero!"
            camilla: "Non mollare!"
        else:
            olivia: "Non capisco che senso ha!"
        end
    elif call draculaAdessoETuoAmico:
        olivia: "Ho un'idea! Mi travesto da Letizia De Ville!"
        camilla: "Che dici, Olivia?"
        olivia: "Ah, già! È vero! Scusa!"
    else:
        olivia: "Forse è meglio di no!"
        camilla: "Certo! Succederà che Dracula ti ammazza!"
        olivia: "Ehm, giusto!"
    end
end
""";
        var root = Path.Combine(Path.GetTempPath(), "segusum-dsl-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            File.WriteAllText(Path.Combine(root, "letizia.seg"), dsl);
            var extracted = new DslSourceStringExtractor().Extract(root);
            Assert.Equal(14, extracted.Count);
            Assert.All(extracted, x => Assert.Equal("letizia.seg", x.RelativePath));
            Assert.Equal(new[]
            {
                "travestiti da Letizia De Ville per farti vedere da Dracula", "Ho una grande idea, Camilla! Dracula vuole essere famoso!", "Cosa?", "Presto, Letizia! Si spogli!", "Ma che stai dicendo, bambina?", "Ah, già! È vero!", "Non mollare!", "Non capisco che senso ha!", "Ho un'idea! Mi travesto da Letizia De Ville!", "Che dici, Olivia?", "Ah, già! È vero! Scusa!", "Forse è meglio di no!", "Certo! Succederà che Dracula ti ammazza!", "Ehm, giusto!"
            }, extracted.Select(x => x.Value));
            Assert.All(extracted, x => Assert.InRange(x.LineNumber, 1, 26));
            Assert.Equal(14, extracted.Select(x => x.LineNumber).Distinct().Count());
        }
        finally { Directory.Delete(root, recursive: true); }
    }
}
