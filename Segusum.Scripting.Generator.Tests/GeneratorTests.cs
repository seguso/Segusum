using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.IO;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Segusum.Scripting.Core;
using Segusum.Scripting.Generator;

namespace Segusum.Scripting.Generator.Tests;

public sealed class GeneratorTests
{
    private const string MikeAcceptanceDsl = """
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
    exp exQualcunoRiceveraUnaBottaComeQuellaPrecedente
    if call namedCutSceneIsSeen ncsMikeStalloneIlBenefattore:
        olivia: "Mike Stallone! Mi aiuti a far rinsavire lo scemo di guerra dandogli una botta in testa come quella che ha avuto in guerra?"
        var cyc = call creaCicloMikeNonRipete
        next cyc
    else:
        makes-no-sense
    end
end
""";

    [Fact]
    public void MikeStalloneAcceptanceCompilesAndBindsRealShape()
    {
        var parsed = DslParser.Parse(new DslSource("mike.seg", MikeAcceptanceDsl));
        Assert.Empty(parsed.Diagnostics);
        var result = Run(MikeAcceptanceDsl, """
public Character mikeStallone = null!;
public Character olivia = null!;
public Character camilla = null!;
public Objective puAiutareLoScemoDiGuerra = null!;
public Explanation exQualcunoRiceveraUnaBottaComeQuellaPrecedente = null!;
public NamedCutSceneId ncsMikeStalloneIlBenefattore = null!;
""");
        Assert.True(result.Diagnostics.Length == 0, string.Join(Environment.NewLine, result.Diagnostics.Select(d => d.ToString())));
        AssertGeneratedCompilationSucceeds(result);
        var generated = Generated(result);
        Assert.Equal(1, Count(generated, "public CycleElemId cidNonRipete1 { get; set; } = new();"));
        Assert.Equal(1, Count(generated, "public CycleElemId cidNonRipete2 { get; set; } = new();"));
        Assert.Contains("cyc.addToCycle(cidNonRipete1", generated);
        Assert.Contains("cyc.addToCycle(cidNonRipete2", generated);
        Assert.Contains("namedCutSceneIsSeen(ncsMikeStalloneIlBenefattore)", generated);
        Assert.Contains("execNextInCycle(cyc)", generated);
        Assert.Contains("e.makesNoSenseAtThisTime = true;", generated);
    }

    [Fact]
    public void WorldDiscoveryUsesExplicitAttributeAndIgnoresDerivedTutorialWorld()
    {
        var result = RunWithWorld("world game\nvar cyc = new-cycle\nadd cyc xww7\nend", "[SegusumWorld(\"game\")] public abstract partial class GameWorld : WorldBase { protected GameWorld() : base(\"en\") { } protected override void configureActionHandlers() { } } [SegusumWorld(\"tutorial\")] public partial class TutorialWorld : GameWorld { }");
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "SEGDSL200");
        Assert.Contains("partial class GameWorld", Generated(result));
        Assert.Contains("partial class TutorialWorld", Generated(result));
        Assert.Equal(2, Count(Generated(result), "protected override void configureGeneratedActionHandlers()"));
    }

    [Fact]
    public void WorldDiscoveryDoesNotUseClassNameConvention()
    {
        var result = RunWithWorld("world game\nvar cyc = new-cycle", "[SegusumWorld(\"game\")] public abstract partial class PincoPallinoWorld : WorldBase { }");
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "SEGDSL200");
        Assert.Contains("partial class PincoPallinoWorld", Generated(result));
    }

    [Fact]
    public void WorldDiscoveryReportsUnknownWorldId()
    {
        var result = RunWithWorld("world missing\nvar cyc = new-cycle", "[SegusumWorld(\"game\")] public abstract partial class GameWorld : WorldBase { }");
        Assert.Contains(result.Diagnostics, d => d.GetMessage().Contains("Unknown SegusumWorld id 'missing'"));
    }

    [Fact]
    public void WorldDiscoveryReportsDuplicateAttributeId()
    {
        var result = RunWithWorld("world game\nvar cyc = new-cycle", "[SegusumWorld(\"game\")] public abstract partial class FirstWorld : WorldBase { } namespace Other { [SegusumWorld(\"game\")] public abstract partial class SecondWorld : WorldBase { } }");
        Assert.Contains(result.Diagnostics, d => d.GetMessage().Contains("Duplicate SegusumWorld id 'game'"));
    }

    [Fact]
    public void WorldDiscoveryReportsNonPartialTarget()
    {
        var result = RunWithWorld("world game\nvar cyc = new-cycle", "[SegusumWorld(\"game\")] public abstract class GameWorld : WorldBase { }");
        Assert.Contains(result.Diagnostics, d => d.GetMessage().Contains("must be declared partial"));
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void WorldDiscoveryReportsMissingTarget()
    {
        var result = RunWithWorld("world game\nvar cyc = new-cycle", "public abstract partial class OtherWorld : WorldBase { }");
        Assert.Contains(result.Diagnostics, d => d.GetMessage().Contains("Unknown SegusumWorld id 'game'"));
    }

    [Fact]
    public void MultipleFilesForOneWorldShareDslScope()
    {
        var result = RunWithWorldFiles("[SegusumWorld(\"game\")] public abstract partial class GameWorld : WorldBase { }", ("Mike.seg", "world game\ndef helper:\nend"), ("Dracula.seg", "world game\ndef main:\n call helper\nend"));
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "SEGDSL200");
        Assert.Contains("private void helper", Generated(result));
        Assert.Contains("private void main", Generated(result));
    }

    [Fact]
    public void DifferentWorldIdsGenerateSeparatePartialTargets()
    {
        var result = RunWithWorldFiles("[SegusumWorld(\"game\")] public abstract partial class GameWorld : WorldBase { protected GameWorld() : base(\"en\") { } } [SegusumWorld(\"tutorial\")] public abstract partial class TutorialWorld : GameWorld { protected TutorialWorld() : base() { } }", ("Game.seg", "world game\nvar gameCycle = new-cycle\nadd gameCycle gameId\nend"), ("Tutorial.seg", "world tutorial\nvar tutorialCycle = new-cycle\nadd tutorialCycle tutorialId\nend"));
        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "SEGDSL200");
        Assert.Contains("partial class GameWorld", Generated(result));
        Assert.Contains("partial class TutorialWorld", Generated(result));
        Assert.Contains("public CycleElemId gameId", Generated(result));
        Assert.Contains("public CycleElemId tutorialId", Generated(result));
        AssertGeneratedCompilationSucceeds(result);
    }

    [Fact]
    public void WorldDirectiveDiagnosticsAreExplicit()
    {
        var world = "[SegusumWorld(\"game\")] public abstract partial class GameWorld : WorldBase { }";
        Assert.Contains(RunWithWorld("var cyc = new-cycle", world).Diagnostics, d => d.GetMessage().Contains("must begin with a world directive"));
        Assert.Contains(RunWithWorld("world game\nworld game\nvar cyc = new-cycle", world).Diagnostics, d => d.GetMessage().Contains("exactly once"));
        Assert.Contains(RunWithWorld("var cyc = new-cycle\nworld game", world).Diagnostics, d => d.GetMessage().Contains("before declarations"));
    }
    [Fact]
    public void CycleElementIdGeneratesOneExactPropertyAndUsesIt()
    {
        var result = Run("var cyc = new-cycle\nadd cyc xww7\nend\nadd cyc xww7\nend");
        Assert.Contains(result.Diagnostics, d => d.GetMessage().Contains("Duplicate CycleElementId"));

        var valid = Run("var cyc = new-cycle\nadd cyc xww7\nend\nnext cyc");
        AssertGeneratedCompilationSucceeds(valid);
        var generated = Generated(valid);
        Assert.Equal(1, Count(generated, "public CycleElemId xww7 { get; set; } = new();"));
        Assert.Contains("addToCycle(xww7", generated);
    }

    [Fact]
    public void KebabCycleElementIdIsRejected()
    {
        var result = Run("var cyc = new-cycle\nadd cyc mike-spac-legn-livello24\nend");
        Assert.Contains(result.Diagnostics, d => d.GetMessage().Contains("stable C# identifier"));
        Assert.DoesNotContain(result.GeneratedSources, x => x.SourceText.ToString().Contains("mikeSpac"));
    }

    [Fact]
    public void ForwardCycleElementReferenceResolvesToTheGeneratedProperty()
    {
        var result = Run("def use id: CycleElemId:\n call helper id\nend\ndef main:\n call use xww7\nend\nvar cyc = new-cycle\nadd cyc xww7\nend");
        Assert.DoesNotContain(result.Diagnostics, d => d.Id.StartsWith("SEGDSL"));
        Assert.Contains("use(xww7)", Generated(result));
        Assert.Contains("helper(id)", Generated(result));
        AssertGeneratedCompilationSucceeds(result);
    }

    [Fact]
    public void NamedKebabArgumentUsesTheRealParameterName()
    {
        var result = Run("def main:\n call helper nome-parametro: 1\nend");
        Assert.DoesNotContain(result.Diagnostics, d => d.Id.StartsWith("SEGDSL"));
        Assert.Contains("helper(nomeParametro: 1)", Generated(result));
        AssertGeneratedCompilationSucceeds(result);
    }

    [Fact]
    public void UnknownIdentifierHasDslDiagnosticAndNoGeneratedSource()
    {
        var result = Run("def main:\n call missing\nend");
        Assert.Contains(result.Diagnostics, d => d.GetMessage().Contains("Unknown function or method"));
        Assert.Empty(result.GeneratedSources);
    }

    [Theory]
    [InlineData("once", "Repeat.OnlyOnce")]
    [InlineData("forever", "Repeat.Forever")]
    public void RepeatModifiersUseRuntimeEnum(string modifier, string emittedValue)
    {
        var result = Run($"var cyc = new-cycle\nadd cyc xww7 {modifier}\nend");
        Assert.DoesNotContain(result.Diagnostics, d => d.GetMessage().Contains("Repeat"));
        Assert.Contains(emittedValue, Generated(result));
        AssertGeneratedCompilationSucceeds(result);
    }

    [Fact]
    public void OmittedRepeatUsesRuntimeDefaultOverload()
    {
        var result = Run("var cyc = new-cycle\nadd cyc xww7\nend");
        var generated = Generated(result);
        Assert.DoesNotContain("Repeat.", generated);
        Assert.Contains("addToCycle(xww7", generated);
        AssertGeneratedCompilationSucceeds(result);
    }

    [Theory]
    [InlineData("public CycleElemId xww7 { get; set; };")]
    [InlineData("public CycleElemId xww7;")]
    public void CycleElementIdCollidesWithExistingWorldMember(string member)
    {
        var result = Run("var cyc = new-cycle\nadd cyc xww7\nend", member);
        Assert.Contains(result.Diagnostics, d => d.GetMessage().Contains("collides with an existing World member"));
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void DomainOperationUsesBoundCyclePropertyAndDateTimeContext()
    {
        var result = Run("var cyc = new-cycle\nadd cyc xww7\n when it not-seen-recently 5\nend\ndef check id: CycleElemId ret bool:\n ret id was-seen-at-least-once\nend", "public DateTime? last { get; set; }");
        var generated = Generated(result);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id.StartsWith("SEGDSL"));
        Assert.Contains("x.notSeenRecently(5)", generated);
        Assert.Contains("wasSeenAtLeastOnce(id)", generated);
        AssertGeneratedCompilationSucceeds(result);
    }

    [Fact]
    public void NotPrecedenceIsPreservedInGeneratedCSharp()
    {
        var result = Run("var cyc = new-cycle\nadd cyc xww7\n when not it not-seen-recently 5\nend\ndef compare ret bool:\n ret not a == b\nend\ndef compare2 ret bool:\n ret not a != b\nend\ndef compare3 ret bool:\n ret not n < m\nend\ndef seen ret bool:\n ret not xww7 was-seen-at-least-once\nend", "public bool a; public bool b; public int n; public int m;");
        Assert.DoesNotContain(result.Diagnostics, d => d.Id.StartsWith("SEGDSL"));
        var generated = Generated(result);
        Assert.Contains("return !(a == b);", generated);
        Assert.Contains("return !(a != b);", generated);
        Assert.Contains("return !(n < m);", generated);
        Assert.Contains("!wasSeenAtLeastOnce(xww7)", generated);
        Assert.Contains("!x.notSeenRecently(5)", generated);
        AssertGeneratedCompilationSucceeds(result);
    }

    [Fact]
    public void PrivateMemberInTheTargetPartialIsAccessible()
    {
        var result = Run("def check ret bool:\n ret ownFlag\nend", "private bool ownFlag;");
        Assert.DoesNotContain(result.Diagnostics, d => d.Id.StartsWith("SEGDSL"));
        Assert.Contains("return ownFlag;", Generated(result));
        AssertGeneratedCompilationSucceeds(result);
    }

    [Fact]
    public void PrivateBaseMemberIsNotAccessibleButProtectedAndPublicAre()
    {
        const string world = "public abstract class BaseWorld : WorldBase { protected BaseWorld() : base(\"en\") { } private bool hiddenBase; protected bool protectedBase; public bool publicBase; } [SegusumWorld(\"game\")] public abstract partial class World : BaseWorld { }";
        var hidden = RunWithWorld("world game\ndef check ret bool:\n ret hiddenBase\nend", world);
        Assert.Contains(hidden.Diagnostics, d => d.GetMessage().Contains("Unknown identifier"));

        var accessible = RunWithWorld("world game\ndef check ret bool:\n ret protectedBase and publicBase\nend", world);
        Assert.DoesNotContain(accessible.Diagnostics, d => d.Id.StartsWith("SEGDSL"));
        AssertGeneratedCompilationSucceeds(accessible);
    }

    [Fact]
    public void InaccessibleExactMemberDoesNotBlockAccessibleNormalizedMember()
    {
        const string world = "public abstract class BaseWorld : WorldBase { protected BaseWorld() : base(\"en\") { } private bool fooBar; public bool FooBar; } [SegusumWorld(\"game\")] public abstract partial class World : BaseWorld { }";
        var result = RunWithWorld("world game\ndef check ret bool:\n ret foo-bar\nend", world);
        Assert.DoesNotContain(result.Diagnostics, d => d.Id.StartsWith("SEGDSL"));
        Assert.Contains("return FooBar;", Generated(result));
        AssertGeneratedCompilationSucceeds(result);
    }

    [Fact]
    public void MultipleAccessibleNormalizedMembersRemainAmbiguous()
    {
        const string world = "public abstract class BaseWorld : WorldBase { protected BaseWorld() : base(\"en\") { } public bool fooBar; public bool FooBar; } [SegusumWorld(\"game\")] public abstract partial class World : BaseWorld { }";
        var result = RunWithWorld("world game\ndef check ret bool:\n ret foo-bar\nend", world);
        Assert.Contains(result.Diagnostics, d => d.GetMessage().Contains("Ambiguous name"));
        Assert.Empty(result.GeneratedSources);
    }

    [Fact]
    public void GeneratedMikeSourceContainsDslLineMappings()
    {
        var result = RunWithWorldFiles("[SegusumWorld(\"game\")] public abstract partial class World : WorldBase { protected World() : base(\"en\") { } public Character mikeStallone = null!; public Character olivia = null!; public Character camilla = null!; public Objective puAiutareLoScemoDiGuerra = null!; public Explanation exQualcunoRiceveraUnaBottaComeQuellaPrecedente = null!; public NamedCutSceneId ncsMikeStalloneIlBenefattore = null!; protected override void configureActionHandlers() { } }", ("Gameplay/Mike.seg", MikeAcceptanceDsl));
        Assert.DoesNotContain(result.Diagnostics, d => d.Id.StartsWith("SEGDSL"));
        var generated = Generated(result);
        Assert.Contains("#line 7 \"Gameplay/Mike.seg\"", generated);
        Assert.Contains("#line 2 \"Gameplay/Mike.seg\"", generated);
        Assert.Contains("#line hidden", generated);
        AssertGeneratedCompilationSucceeds(result);
    }

    [Fact]
    public void DownstreamGeneratedErrorsMapToFunctionDialogueAndHandlerStatements()
    {
        var function = Run("def main ret bool:\n ret flag\nend", "public bool flag;");
        var functionError = CompileGeneratedMutation(function, "return flag;", "return missingValue;");
        AssertMappedTo(functionError, "test.seg", 3);

        var dialogue = Run("use olivia here:\n olivia: \"hello\"\nend", "public Character olivia = null!;");
        var dialogueError = CompileGeneratedMutation(dialogue, "dial(olivia,\"hello\");", "missingDial(olivia,\"hello\");");
        AssertMappedTo(dialogueError, "test.seg", 3);

        var handler = Run("use olivia here:\n call helper\nend", "public Character olivia = null!; public void helper() { }");
        var handlerError = CompileGeneratedMutation(handler, "helper();", "missingHelper();");
        AssertMappedTo(handlerError, "test.seg", 3);
    }

    [Fact]
    public void DownstreamGeneratedErrorMapsToTheCorrectFileInMergedWorld()
    {
        var result = RunWithWorldFiles("[SegusumWorld(\"game\")] public abstract partial class World : WorldBase { protected World() : base(\"en\") { } protected override void configureActionHandlers() { } }", ("Gameplay/Mike.seg", "world game\ndef ok ret bool:\n ret true\nend"), ("Gameplay/Dracula.seg", "world game\nstate broken: MissingType = 1"));
        var errors = result.Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error && d.Id == "CS0246").ToArray();
        var error = Assert.Single(errors);
        var span = error.Location.GetMappedLineSpan();
        Assert.True(span.Path.EndsWith("Gameplay\\Dracula.seg", StringComparison.OrdinalIgnoreCase) || span.Path.EndsWith("Gameplay/Dracula.seg", StringComparison.OrdinalIgnoreCase), $"Unexpected mapped path: {span.Path}\n{string.Join("\n", result.Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error))}");
        Assert.Equal(1, span.StartLinePosition.Line);
    }

    private static RunResult Run(string dsl, string additionalMembers = "")
    {
        if (!dsl.StartsWith("world ", StringComparison.Ordinal)) dsl = "world game\n" + dsl;
        return RunWithWorld(dsl, $"[SegusumWorld(\"game\")] public abstract partial class World : WorldBase {{ protected World() : base(\"en\") {{ }} public void helper(int nomeParametro) {{ }} public void helper(CycleElemId id) {{ }} {additionalMembers} protected override void configureActionHandlers() {{ }} }}");
    }

    private static RunResult RunWithWorld(string dsl, string worldSource)
    {
        return RunWithWorldFiles(worldSource, ("test.seg", dsl));
    }

    private static RunResult RunWithWorldFiles(string worldSource, params (string Path, string Text)[] files)
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))?.Split(Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)).Cast<MetadataReference>().ToList() ?? new List<MetadataReference>();
        if (typeof(Seg.WorldBase).Assembly.Location is { Length: > 0 } location) references.Add(MetadataReference.CreateFromFile(location));
        var tree = CSharpSyntaxTree.ParseText($"using System; using Seg; namespace Demo {{ {worldSource} }}");
        var compilation = CSharpCompilation.Create("DslTest", new[] { tree }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new SegusumGenerator());
        driver = driver.AddAdditionalTexts(ImmutableArray.Create<AdditionalText>(files.Select(x => (AdditionalText)new InMemoryAdditionalText(x.Path, x.Text)).ToArray()));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
        var generatorDiagnostics = driver.GetRunResult().Diagnostics;
        var generated = driver.GetRunResult().Results.SelectMany(x => x.GeneratedSources).ToImmutableArray();
        return new RunResult(generatorDiagnostics, generated, updated);
    }

    private static string Generated(RunResult result) => string.Join("\n", result.GeneratedSources.Select(x => x.SourceText.ToString()));
    private static void AssertGeneratedCompilationSucceeds(RunResult result)
    {
        var errors = result.Compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.True(errors.Length == 0, string.Join(Environment.NewLine, errors.Select(x => x.ToString())));
    }
    private static Diagnostic CompileGeneratedMutation(RunResult result, string original, string replacement)
    {
        var tree = result.Compilation.SyntaxTrees.Single(x => x.GetText().ToString().Contains("// <auto-generated />", StringComparison.Ordinal));
        var generated = tree.GetText().ToString().Replace(original, replacement, StringComparison.Ordinal);
        Assert.Contains(replacement, generated);
        var compilation = result.Compilation.RemoveSyntaxTrees(tree).AddSyntaxTrees(CSharpSyntaxTree.ParseText(generated, path: "generated.g.cs"));
        var errors = compilation.GetDiagnostics().Where(d => d.Severity == DiagnosticSeverity.Error).ToArray();
        Assert.NotEmpty(errors);
        var mapped = errors.Where(d => d.Location.GetMappedLineSpan().Path.EndsWith("test.seg", StringComparison.OrdinalIgnoreCase)).ToArray();
        Assert.True(mapped.Length == 1, string.Join(Environment.NewLine, errors.Select(d => d.ToString())));
        return mapped[0];
    }
    private static void AssertMappedTo(Diagnostic diagnostic, string path, int line)
    {
        var span = diagnostic.Location.GetMappedLineSpan();
        Assert.Equal(path, span.Path);
        Assert.Equal(line - 1, span.StartLinePosition.Line);
    }
    private static int Count(string text, string value) => text.Split(value, StringSplitOptions.None).Length - 1;
    private sealed record RunResult(ImmutableArray<Diagnostic> Diagnostics, ImmutableArray<GeneratedSourceResult> GeneratedSources, Compilation Compilation);

    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path => path;
        public override SourceText GetText(System.Threading.CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
