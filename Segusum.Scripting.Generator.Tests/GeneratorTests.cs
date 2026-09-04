using System;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Text;
using Segusum.Scripting.Generator;

namespace Segusum.Scripting.Generator.Tests;

public sealed class GeneratorTests
{
    [Fact]
    public void CycleElementIdGeneratesOneExactPropertyAndUsesIt()
    {
        var result = Run("var cyc = new-cycle\nadd cyc xww7\nend\nadd cyc xww7\nend");
        Assert.Contains(result.Diagnostics, d => d.GetMessage().Contains("Duplicate CycleElementId"));

        var valid = Run("var cyc = new-cycle\nadd cyc xww7\nend\nnext cyc");
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
    }

    [Fact]
    public void NamedKebabArgumentUsesTheRealParameterName()
    {
        var result = Run("def main:\n call helper nome-parametro: 1\nend");
        Assert.DoesNotContain(result.Diagnostics, d => d.Id.StartsWith("SEGDSL"));
        Assert.Contains("helper(nomeParametro: 1)", Generated(result));
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
    }

    [Fact]
    public void OmittedRepeatUsesRuntimeDefaultOverload()
    {
        var result = Run("var cyc = new-cycle\nadd cyc xww7\nend");
        var generated = Generated(result);
        Assert.DoesNotContain("Repeat.", generated);
        Assert.Contains("addToCycle(xww7", generated);
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
    }

    private static RunResult Run(string dsl, string additionalMembers = "")
    {
        var references = ((string?)typeof(Seg.WorldBase).Assembly.Location) is { Length: > 0 } location
            ? new[] { MetadataReference.CreateFromFile(location) }
            : Array.Empty<MetadataReference>();
        var tree = CSharpSyntaxTree.ParseText($"using Seg; namespace Demo {{ public partial class World : WorldBase {{ public void helper(int nomeParametro) {{ }} public void helper(CycleElemId id) {{ }} {additionalMembers} protected override void configureActionHandlers() {{ }} }} }}");
        var compilation = CSharpCompilation.Create("DslTest", new[] { tree }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
        GeneratorDriver driver = CSharpGeneratorDriver.Create(new SegusumGenerator());
        driver = driver.AddAdditionalTexts(ImmutableArray.Create<AdditionalText>(new InMemoryAdditionalText("test.seg", dsl)));
        driver = driver.RunGeneratorsAndUpdateCompilation(compilation, out var updated, out _);
        var generatorDiagnostics = driver.GetRunResult().Diagnostics;
        var generated = driver.GetRunResult().Results.SelectMany(x => x.GeneratedSources).ToImmutableArray();
        return new RunResult(generatorDiagnostics, generated, updated);
    }

    private static string Generated(RunResult result) => string.Join("\n", result.GeneratedSources.Select(x => x.SourceText.ToString()));
    private static int Count(string text, string value) => text.Split(value, StringSplitOptions.None).Length - 1;
    private sealed record RunResult(ImmutableArray<Diagnostic> Diagnostics, ImmutableArray<GeneratedSourceResult> GeneratedSources, Compilation Compilation);

    private sealed class InMemoryAdditionalText(string path, string text) : AdditionalText
    {
        public override string Path => path;
        public override SourceText GetText(System.Threading.CancellationToken cancellationToken = default) => SourceText.From(text);
    }
}
