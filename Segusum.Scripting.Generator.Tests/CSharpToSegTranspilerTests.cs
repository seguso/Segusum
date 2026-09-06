using System;
using System.Linq;
using Segusum.Scripting.Tooling;

namespace Segusum.Scripting.Generator.Tests;

public sealed class CSharpToSegTranspilerTests
{
    [Fact]
    public void ExpressionEmitterPreservesLiteralTokenAndBooleanStructure()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { private void M() { if (ready && !blocked) { dial(camilla, \"[[translation]] ! &&\"); } } }");
        Assert.Contains("camilla: \"[[translation]] ! &&\"", result.Text, StringComparison.Ordinal);
        Assert.Contains("if ready and not blocked:", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedLinqIsNeverEmittedAsExecutableSeg()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { private void M() { var n = values.Count(x => x > 0); } }");
        Assert.Contains(result.Diagnostics, x => x.Status == MigrationUnitStatus.Unsupported);
        Assert.Contains("C2SEG-MANUAL", CSharpToSegTranspiler.Transpile("x.cs", "class W { private void M() { var n = values.Count(x => x > 0); } }", true).Text, StringComparison.Ordinal);
    }

    [Fact]
    public void CycleAndNamedCutsceneAreEmittedStructurally()
    {
        const string source = "class W { private void M() { var cyc = startCycle(bb5, Importance.Important, Repeat.OnlyOnce, x => x.notSeenRecently(30), x => { using (namedCutScene(ncs, roomA, objA)) { dial(camilla, \"Hello\"); } }).addToCycle(bb6, x => x.notSeenRecently(20), x => { }); execNextInCycle(cyc); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        Assert.Contains("add cyc bb5 important once", result.Text, StringComparison.Ordinal);
        Assert.Contains("add cyc bb6", result.Text, StringComparison.Ordinal);
        Assert.Contains("named-cutscene ncs", result.Text, StringComparison.Ordinal);
        Assert.Contains("next cyc", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void HelperParametersAndCommentsAreRetained()
    {
        const string source = "class W { // design note\n private bool Helper(int x) { // TODO\n return x > 0; // trailing\n } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        Assert.Contains("def Helper x:", result.Text, StringComparison.Ordinal);
        Assert.Contains("design note", result.Text, StringComparison.Ordinal);
        Assert.Contains("TODO", result.Text, StringComparison.Ordinal);
        Assert.Contains("trailing", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void UnitDiagnosticsDoNotLeakFromLaterHandler()
    {
        const string source = "class W { void M() { addHandlerUseHere(a, handler: i => { dial(camilla, \"A\"); }); addHandlerUseHere(b, handler: i => { while (true) { } }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        Assert.Equal(MigrationUnitStatus.Translated, result.Units.Single(x => x.Id == "use-here:a").Status);
        Assert.Equal(MigrationUnitStatus.Unsupported, result.Units.Single(x => x.Id == "use-here:b").Status);
    }

    [Fact]
    public void UnitDiagnosticsDoNotLeakFromEarlierHandler()
    {
        const string source = "class W { void M() { addHandlerUseHere(a, handler: i => { while (true) { } }); addHandlerUseHere(b, handler: i => { dial(camilla, \"B\"); }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        Assert.Equal(MigrationUnitStatus.Unsupported, result.Units.Single(x => x.Id == "use-here:a").Status);
        Assert.Equal(MigrationUnitStatus.Translated, result.Units.Single(x => x.Id == "use-here:b").Status);
    }

    [Fact]
    public void UnresolvedNamedCutsceneAffectsOnlyItsUnit()
    {
        const string source = "class W { void M() { addHandlerUseHere(a, handler: i => { using (namedCutScene(missingId, roomA)) { dial(camilla, \"A\"); } }); addHandlerUseHere(b, handler: i => { dial(camilla, \"B\"); }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        Assert.Equal(MigrationUnitStatus.Unsupported, result.Units.Single(x => x.Id == "use-here:a").Status);
        Assert.Equal(MigrationUnitStatus.Translated, result.Units.Single(x => x.Id == "use-here:b").Status);
    }

    [Fact]
    public void DuplicateCommentsArePreservedWithCardinality()
    {
        const string source = "class W { void M() { addHandlerUseHere(a, handler: i => { // same\n dial(camilla, \"A\"); // same\n }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        Assert.Equal(2, result.Text.Split("same", StringSplitOptions.None).Length - 1);
        Assert.DoesNotContain(result.Diagnostics, x => x.Reason.StartsWith("comment was not preserved:", StringComparison.Ordinal));
    }

    [Fact]
    public void CycleAliasRewriteDoesNotTouchStringLiteral()
    {
        const string source = "class W { private void M() { var cyc = startCycle(bb5, Importance.Important, Repeat.OnlyOnce, x => foo(x, \"x\"), x => { }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        Assert.Contains("foo it \"x\"", result.Text, StringComparison.Ordinal);
    }
}
