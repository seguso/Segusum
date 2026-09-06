using System;
using System.IO;
using System.Linq;
using Segusum.Scripting.Tooling;

namespace Segusum.Scripting.Generator.Tests;

public sealed class CSharpToSegTranspilerTests
{
    [Fact]
    public void ExpressionEmitterPreservesLiteralTokenAndBooleanStructure()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addHandlerUseHere(a, handler: i => { if (ready && !blocked) { dial(camilla, \"[[translation]] ! &&\"); } }); } }");
        Assert.Contains("camilla: \"[[translation]] ! &&\"", result.Text, StringComparison.Ordinal);
        Assert.Contains("if ready and not blocked:", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void AnyPredicateUsesExistingExistsQuerySyntax()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addHandlerUseHere(a, handler: i => { if (values.Any(x => x.notSeenRecently(30))) { foo(); } }); } }");
        Assert.Contains("exists [from values x where x.notSeenRecently 30]", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Status == MigrationUnitStatus.Unsupported);
    }

    [Fact]
    public void CollectionExpressionUsesExistingListLiteralSyntax()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addHandlerUseHere(a, handler: i => { var xs = [one, two, 3]; foo(xs); }); } }");
        Assert.Contains("var xs = [one, two, 3]", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Status == MigrationUnitStatus.Unsupported);
    }

    [Fact]
    public void NamedCutsceneTitleIsResolvedAcrossSiblingSourceFiles()
    {
        var directory = Path.Combine(Path.GetTempPath(), "segusum-c2seg-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "worldObjects.cs"), "class W { NamedCutSceneId ncs = new NamedCutSceneId { titleUntranslated = translatable(\"A title\") }; }");
            var sourcePath = Path.Combine(directory, "handlers.cs");
            var result = CSharpToSegTranspiler.Transpile(sourcePath, "class W { void M() { addHandlerUseHere(a, handler: i => { using (namedCutScene(ncs, roomA)) { dial(camilla, \"A\"); } }); } }");
            Assert.Contains("named-cutscene ncs \"A title\" roomA", result.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(result.Diagnostics, x => x.Reason.Contains("title cannot be resolved", StringComparison.Ordinal));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void UnsupportedLinqIsNeverEmittedAsExecutableSeg()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addHandlerUseHere(a, handler: i => { var n = values.Count(x => x > 0); }); } }");
        Assert.Contains(result.Diagnostics, x => x.Status == MigrationUnitStatus.Unsupported);
        var partial = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addHandlerUseHere(a, handler: i => { var n = values.Count(x => x > 0); }); } }", true);
        Assert.Contains("C2SEG-MANUAL", partial.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void CycleAndNamedCutsceneAreEmittedStructurally()
    {
        const string source = "class W { void M() { addHandlerUseHere(a, handler: i => { var cyc = startCycle(bb5, Importance.Important, Repeat.OnlyOnce, x => x.notSeenRecently(30), x => { using (namedCutScene(ncs, roomA, objA)) { dial(camilla, \"Hello\"); } }).addToCycle(bb6, x => x.notSeenRecently(20), x => { }); execNextInCycle(cyc); }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        Assert.Contains("add cyc bb5 important once", result.Text, StringComparison.Ordinal);
        Assert.Contains("add cyc bb6", result.Text, StringComparison.Ordinal);
        Assert.Contains("named-cutscene ncs", result.Text, StringComparison.Ordinal);
        Assert.Contains("next cyc", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void HelperParametersAndCommentsAreRetained()
    {
        const string source = "class W { void M() { addHandlerUseHere(a, handler: i => { Helper(1); }); } // design note\n private bool Helper(int x) { // TODO\n return x > 0; // trailing\n } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        Assert.Contains(result.Units, x => x.Id == "Helper");
        Assert.Contains("def Helper x:", result.Text, StringComparison.Ordinal);
        Assert.True(result.Text.Contains("design note", StringComparison.Ordinal), result.Text);
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
    public void HeaderAndDisabledCodeCommentsArePreserved()
    {
        const string source = "class W { void M() { // TODO disabled gameplay\n // addHandlerUseHere(old, e => { });\n addHandlerUseHere(a, handler: e => { foo(); }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        Assert.Contains("TODO disabled gameplay", result.Text, StringComparison.Ordinal);
        Assert.Contains("addHandlerUseHere(old, e => { });", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Reason.StartsWith("comment was not preserved:", StringComparison.Ordinal));
    }

    [Fact]
    public void CycleAliasRewriteDoesNotTouchStringLiteral()
    {
        const string source = "class W { void M() { addHandlerUseHere(a, handler: i => { var cyc = startCycle(bb5, Importance.Important, Repeat.OnlyOnce, x => foo(x, \"x\"), x => { }); }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        Assert.Contains("foo it \"x\"", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistrationContainerIsNotAHelperUnit()
    {
        const string source = "class W { protected void configureActionHandlers() { addHandlerUseHere(a, e => { Helper(); }); addHandlerUseHere(b, e => { }); } private void Helper() { foo(); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        Assert.Equal(new[] { "use-here:a", "use-here:b", "Helper" }, result.Units.Select(x => x.Id));
        Assert.DoesNotContain("def configureActionHandlers", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void RoomRegistrationContainerIsNotAHelperUnit()
    {
        const string source = "class W { private void configureRoomHandlers() { addRoomChangedHandler(roomA, e => { foo(); }); } private void Helper() { bar(); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        Assert.Equal(new[] { "room-changed:roomA" }, result.Units.Select(x => x.Id));
        Assert.DoesNotContain("def configureRoomHandlers", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void HelperDependencyStatusIsLimitedToReachableHelpers()
    {
        const string source = "class W { void M() { addHandlerUseHere(a, handler: i => { Good(); }); } private void Good() { Bad(); } private void Bad() { while (true) { } } private void Uncalled() { while (true) { } } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        Assert.Equal(MigrationUnitStatus.DependsOnCSharpHelper, result.Units.Single(x => x.Id == "Good").Status);
        Assert.Equal(MigrationUnitStatus.Unsupported, result.Units.Single(x => x.Id == "Bad").Status);
        Assert.DoesNotContain(result.Units, x => x.Id == "Uncalled");
    }
}
