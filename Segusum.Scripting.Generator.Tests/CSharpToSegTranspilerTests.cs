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
        Assert.Contains("camilla: [[translation]] ! &&", result.Text, StringComparison.Ordinal);
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
    public void ConditionalExpressionIsEmittedAsSegExpression()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addHandlerUseHere(a, handler: i => { var protag = camilla.isInCurParty() ? camilla : olivia; protag = ifReady ? camilla : olivia; foo(camilla.isInCurParty() ? camilla : olivia); }); } }");
        Assert.Contains("var protag = if camilla.isInCurParty then camilla else olivia", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Status == MigrationUnitStatus.Unsupported);
    }

    [Fact]
    public void ExistingCycleFluentChainIsEmittedInSourceOrder()
    {
        const string source = "class W { void M() { addRoomChangedHandler(roomA, e => { cyc.addToCycle(bas2, x => x.notSeenRecently(2), x => { if (ready) { dial(camilla, \"[[translation]]\"); } }).addToCycle(bas3, x => x.notSeenRecently(3), x => { timestamp = DateTime.Now; }); }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source, true);
        Assert.True(result.Text.IndexOf("add cyc bas2", StringComparison.Ordinal) < result.Text.IndexOf("add cyc bas3", StringComparison.Ordinal), result.Text);
        Assert.Contains("[[translation]]", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Reason.Contains("Unsupported C# call: addToCycle", StringComparison.Ordinal));
    }

    [Fact]
    public void ExistingCycleFluentChainAssignedBackToCycleIsFlattened()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addRoomChangedHandler(roomA, e => { cyc = cyc.addToCycle(bas2, x => true, x => { foo(); }).addToCycle(bas3, x => true, x => { bar(); }); }); } }");
        Assert.Contains("add cyc bas2", result.Text, StringComparison.Ordinal);
        Assert.Contains("add cyc bas3", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Reason.Contains("Unsupported C# call: addToCycle", StringComparison.Ordinal));
    }

    [Fact]
    public void RandomNextModuloUsesExistingRandomExpression()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addRoomChangedHandler(roomA, e => { if (rand.Next() % 2 == 0) { foo(); } }); } }");
        Assert.Contains("if random 2 == 0:", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Status == MigrationUnitStatus.Unsupported);
    }

    [Fact]
    public void DialogueFormattingRemovesOnlyDialogueQuotes()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addHandlerUseHere(a, handler: i => { dial(olivia, \"Oh, certo![[right, right.]]\"); }); } }");
        Assert.Contains("olivia: Oh, certo![[right, right.]]", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("olivia: \"", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void LongConditionsAreFormattedStructurallyWithParenthesesPreserved()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addRoomChangedHandler(roomA, e => { if (a && (b || c) && d && e) { foo(); } }); } }");
        var normalized = result.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("if a\n        and (b or c)\n        and d\n        and e:", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Reason.Contains("generated SEG is not parsable", StringComparison.Ordinal));
    }

    [Fact]
    public void ThisExpressionIsEmittedAsCurrentInstance()
    {
        const string source = "class W { void M() { addRoomChangedHandler(roomA, e => { if (!CycleMemory.wouldSaySomethingNewAndImportant(cyc2, this, cidPresentatoStrilloneMag)) { foo(); } }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        Assert.Contains("CycleMemory.wouldSaySomethingNewAndImportant cyc2 this cidPresentatoStrilloneMag", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Reason.Contains("ThisExpression", StringComparison.Ordinal));
    }

    [Fact]
    public void LayoutUsesFourSpaceIndentationAndOneBlankLineBeforeEveryDialogue()
    {
        const string source = "class W { void M() { addRoomChangedHandler(roomA, e => { if (a && b && c) { foo(); dial(olivia, \"Uno\"); dial(camilla, \"Due\"); dial(olivia, \"Tre\"); } }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        var lines = result.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        foreach (var line in lines.Where(x => x.Trim().Length != 0))
            Assert.Equal(0, line.TakeWhile(char.IsWhiteSpace).Count() % 4);
        var dialogueIndexes = lines.Select((line, index) => (line, index)).Where(x => x.line.Contains(": Uno", StringComparison.Ordinal) || x.line.Contains(": Due", StringComparison.Ordinal) || x.line.Contains(": Tre", StringComparison.Ordinal)).Select(x => x.index).ToArray();
        Assert.Equal(3, dialogueIndexes.Length);
        foreach (var index in dialogueIndexes)
        {
            Assert.True(index > 0 && lines[index - 1].Length == 0, result.Text);
            Assert.True(index < 2 || lines[index - 2].Length != 0, result.Text);
        }
    }

    [Fact]
    public void StructuralContinuationIndentationIsAlwaysFourSpaces()
    {
        const string source = "class W { void M() { addHandlerUseHere(a, isPossibleNow: () => first && second && third, handler: e => { var cyc = startCycle(bb5, Importance.Important, Repeat.OnlyOnce, x => first && second && third, x => { using (namedCutScene(ncs, roomA)) { if (first && second && third) { dial(olivia, \"Ciao\"); } } }); }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source, true);
        foreach (var line in result.Text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').Where(x => x.Trim().Length != 0))
            Assert.Equal(0, line.TakeWhile(char.IsWhiteSpace).Count() % 4);
    }

    [Fact]
    public void ShortConditionsRemainSingleLine()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addRoomChangedHandler(roomA, e => { if (ready && !blocked) { foo(); } }); } }");
        Assert.Contains("if ready and not blocked:", result.Text, StringComparison.Ordinal);
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
