using System;
using System.IO;
using System.Linq;
using Segusum.Scripting.Core;
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
    public void HandlerInputParameterNamesAreNormalizedToCanonicalSegInput()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addHandlerUseHere(a, handler: i => { useInput(i); var keep = i.chosenText; }); } } ");

        Assert.Contains("useInput e", result.Text, StringComparison.Ordinal);
        Assert.Contains("var keep = e.chosenText", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("useInput i", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Status == MigrationUnitStatus.Unsupported);
    }

    [Fact]
    public void AnyPredicateUsesExistingExistsQuerySyntax()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addHandlerUseHere(a, handler: i => { if (values.Any(x => x.notSeenRecently(30))) { foo(); } }); } }");
        Assert.Contains("exists [from values x where x.notSeenRecently 30]", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Status == MigrationUnitStatus.Unsupported);
    }

    [Fact]
    public void NestedCallArgumentsAreParenthesizedForMlApplication()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addHandlerUseHere(a, handler: i => { outer(inner(a)); outer(inner(a), b); }); } }");
        Assert.Contains("outer (inner a)", result.Text, StringComparison.Ordinal);
        Assert.Contains("outer (inner a) b", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Status == MigrationUnitStatus.Unsupported);
    }

    [Fact]
    public void VerbatimStringArgumentsBecomeValidSegStringsWithoutChangingContent()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addHandlerUseHere(a, handler: i => { narImg(\"...\", @\"img/camilla-violin.png\"); }); } }");
        Assert.Contains("narImg \"...\" \"img/camilla-violin.png\"", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("@\"img/camilla-violin.png\"", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeNarrationCallsKeepAllArgumentsOnTheNormalCallPath()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addRoomChangedHandler(roomA, i => { narRoom(\"...\", roomCamilla, false, alsoShowGraphicsInTextMode: true); narImg(\"...\", \"img/a.png\", alsoShowGraphicsInTextMode: true); }); } }");
        Assert.Contains("narRoom \"...\" roomCamilla false alsoShowGraphicsInTextMode: true", result.Text, StringComparison.Ordinal);
        Assert.Contains("narImg \"...\" \"img/a.png\" alsoShowGraphicsInTextMode: true", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void UninitializedPrimitiveLocalsUseSafeSegDefaults()
    {
        const string source = "class W { void M() { addHandlerUseHere(a, handler: e => { bool ready; int count; string text; foo(ready, count, text); }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        Assert.Contains("var ready: bool = false", result.Text, StringComparison.Ordinal);
        Assert.Contains("var count: int = 0", result.Text, StringComparison.Ordinal);
        Assert.Contains("var text: string = null", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("var ready: bool = null", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("var count: int = null", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void CombineExplanationDoesNotBecomePhrase()
    {
        const string source = "class W { void M() { addHandlerCombine(a, b, explanation: ex, fullSentenceUntransl: \"use a on b\".translatable(), handler: e => { foo(); }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        Assert.Contains("phrase \"use a on b\"", result.Text, StringComparison.Ordinal);
        Assert.Contains("exp ex", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("phrase ex", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Status == MigrationUnitStatus.Unsupported);
    }

    [Fact]
    public void NarTextUsesColonSyntaxAndNarrativeLayout()
    {
        const string source = "class W { void M() { addRoomChangedHandler(roomA, i => { foo(); narText(\"Un'ora dopo...\"); narRoom(\"La stanza\", roomA, false); narImg(\"Testo\", \"img/a.png\", alsoShowGraphicsInTextMode: true); bar(); }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        var text = result.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("nar: Un'ora dopo...", text, StringComparison.Ordinal);
        Assert.Contains("narRoom \"La stanza\" roomA false", text, StringComparison.Ordinal);
        Assert.Contains("narImg \"Testo\" \"img/a.png\" alsoShowGraphicsInTextMode: true", text, StringComparison.Ordinal);
        AssertNarrativeHasOneBlankLineAround(text, "nar: Un'ora dopo...");
        AssertNarrativeHasOneBlankLineAround(text, "narRoom \"La stanza\"");
        AssertNarrativeHasOneBlankLineAround(text, "narImg \"Testo\"");
        Assert.Empty(DslParser.Parse(new DslSource("generated.seg", text)).Diagnostics);
    }

    [Fact]
    public void TopLevelDeclarationsHaveExactlyOneBlankLineBetweenThem()
    {
        const string source = """
            class W
            {
                void M()
                {
                    addHandlerUseHere(a, handler: e => { Helper(); });
                    addHandlerCombine(a, b, "combine", handler: e => { foo(); });
                    addHandlerUseFor(c, d, "use-for", handler: e => { bar(); });
                    addHandlerPickUp(e, handler: i => { baz(); });
                }

                private void Helper()
                {
                    qux();
                }
            }
            """;

        var first = CSharpToSegTranspiler.Transpile("x.cs", source);
        var second = CSharpToSegTranspiler.Transpile("x.cs", source);
        var text = first.Text.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("end\n\ncombine a with b:", text, StringComparison.Ordinal);
        Assert.Contains("end\n\nuse c for d:", text, StringComparison.Ordinal);
        Assert.Contains("end\n\npickup e:", text, StringComparison.Ordinal);
        Assert.Contains("end\n\ndef Helper:", text, StringComparison.Ordinal);
        Assert.DoesNotContain("\n\n\n", text, StringComparison.Ordinal);
        Assert.Equal(text, second.Text.Replace("\r\n", "\n", StringComparison.Ordinal));
        Assert.Empty(DslParser.Parse(new DslSource("generated.seg", text)).Diagnostics);
    }

    [Fact]
    public void ConsecutiveAddBlocksMayOmitIntermediateEnd()
    {
        const string source = "world game\nroom-changed roomA:\n    var cyc = new-cycle\n    add cyc cidA\n        when true\n        foo\n    add cyc cidB\n        when true\n        bar\n    add cyc cidC\n        when true\n        baz\n    end\nend\n";
        var parsed = DslParser.Parse(new DslSource("adds.seg", source));
        Assert.Empty(parsed.Diagnostics);
        var room = Assert.IsType<HandlerDeclaration>(parsed.Document.Declarations.Single());
        Assert.Equal(4, room.Body.Count);
        Assert.Equal(3, room.Body.OfType<AddCycleElementStatement>().Count());
    }

    [Fact]
    public void LongReturnExpressionWrapsAtLogicalAstNodes()
    {
        const string source = "class W { void M() { addHandlerUseHere(a, handler: i => { Check(); }); } private bool Check() { return a || b && c && d && e; } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        var text = result.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("ret a\n        or b\n        and c\n        and d\n        and e", text, StringComparison.Ordinal);
    }

    [Fact]
    public void TopLevelDefinitionsHaveExactlyOneBlankLineBetweenThem()
    {
        const string source = "class W { void M() { addHandlerUseHere(a, handler: i => { Foo(); Bar(); }); } private bool Foo() { return true; } private bool Bar() { return false; } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        var text = result.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("end\n\ndef Bar", text, StringComparison.Ordinal);
        Assert.DoesNotContain("end\n\n\ndef", text, StringComparison.Ordinal);
    }

    private static void AssertNarrativeHasOneBlankLineAround(string text, string marker)
    {
        var line = text.Split('\n').ToList();
        var index = line.FindIndex(x => x.Contains(marker, StringComparison.Ordinal));
        Assert.True(index > 0 && line[index - 1].Length == 0, text);
        Assert.True(index + 1 < line.Count && line[index + 1].Length == 0, text);
        Assert.True(index < 2 || line[index - 2].Length != 0, text);
        Assert.True(index + 2 >= line.Count || line[index + 2].Length != 0, text);
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
    public void ConsecutiveExistingCycleAddsOmitIntermediateEnd()
    {
        const string source = "class W { void M() { addRoomChangedHandler(roomA, e => { cyc.addToCycle(cidA, x => true, x => { foo(); }); cyc.addToCycle(cidB, x => true, x => { bar(); }); cyc.addToCycle(cidC, x => true, x => { baz(); }); }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        Assert.Contains("add cyc cidA", result.Text, StringComparison.Ordinal);
        Assert.Contains("add cyc cidB", result.Text, StringComparison.Ordinal);
        Assert.Contains("add cyc cidC", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("foo\n    end\n    add cyc cidB", result.Text.Replace("\r\n", "\n", StringComparison.Ordinal), StringComparison.Ordinal);
    }

    [Fact]
    public void RandomNextModuloUsesExistingRandomExpression()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addRoomChangedHandler(roomA, e => { if (rand.Next() % 2 == 0) { foo(); } }); } }");
        Assert.Contains("if random 2 == 0:", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Status == MigrationUnitStatus.Unsupported);
    }

    [Fact]
    public void LocalFunctionIsLiftedToTopLevelDef()
    {
        const string source = "class W { void beforeRoomChangeManual() { double tempoPerProssimoZauroz() { return 5; } if (tempoPerProssimoZauroz() > 0) { foo(); } } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source, true, "beforeRoomChangeManual");
        Assert.Contains("def tempoPerProssimoZauroz ret double:", result.Text, StringComparison.Ordinal);
        Assert.Contains("if tempoPerProssimoZauroz > 0:", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Reason.Contains("LocalFunctionStatement", StringComparison.Ordinal));
    }

    [Fact]
    public void LocalFunctionCapturesBecomeExplicitDefParameters()
    {
        const string source = "class W { void beforeRoomChangeManual() { int delay = 5; int helper() { return delay; } if (helper() > 0) { foo(); } } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source, true, "beforeRoomChangeManual");
        Assert.Contains("def helper delay: int ret int:", result.Text, StringComparison.Ordinal);
        Assert.Contains("if helper delay > 0:", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Reason.Contains("LocalFunctionStatement", StringComparison.Ordinal));
    }

    [Fact]
    public void AfterActionExecutedRemainsPartialUntilItHasASemanticVerifier()
    {
        const string source = "class W { void afterActionExecutedCSharp() { dial(camilla, \"Test\"); } }";
        var result = CSharpToSegTranspiler.Transpile("after-action.cs", source, true);

        var unit = Assert.Single(result.Units, x => x.Id == "afterActionExecutedCSharp");
        Assert.Equal(MigrationUnitStatus.Partial, unit.Status);
        Assert.Contains(unit.Diagnostics, x => x.Reason == "special handler round-trip is not yet certifiable");
    }

    [Fact]
    public void BeforeRoomChangeUsesItsRealSemanticVerifier()
    {
        const string source = "class W { void beforeRoomChangeManual(Room from, Room to, WalkPath segment, WalkPath path, BeforeRoomChangeInput e) { dial(camilla, \"Test\"); } }";
        var result = CSharpToSegTranspiler.Transpile("before-room.cs", source, true, "beforeRoomChangeManual");

        var unit = Assert.Single(result.Units, x => x.Id == "beforeRoomChangeManual");
        Assert.Equal(MigrationUnitStatus.Translated, unit.Status);
        Assert.DoesNotContain(unit.Diagnostics, x => x.Reason.Contains("special handler round-trip", StringComparison.Ordinal));
    }

    [Fact]
    public void GeneralModuloExpressionIsEmittedWithNumericPrecedence()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addRoomChangedHandler(roomA, e => { var ra = rand.Next() % 3; if (ra % 2 == 0) { foo(); } }); } }");
        var normalized = result.Text.Replace("\r\n", "\n", StringComparison.Ordinal);
        Assert.Contains("var ra = rand.Next % 3", normalized, StringComparison.Ordinal);
        Assert.Contains("if ra % 2 == 0:", normalized, StringComparison.Ordinal);
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
            Assert.True(index + 1 < lines.Length && lines[index + 1].Length == 0, result.Text);
            Assert.True(index + 2 >= lines.Length || lines[index + 2].Length != 0, result.Text);
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
    public void ConditionHeadersHaveExactlyOneBlankLineBeforeTheirBodies()
    {
        const string source = "class W { void M() { addRoomChangedHandler(roomA, e => { var cyc = startCycle(bb5, x => first && second && third, x => { if (first && second && third) { foo(); } }); }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source);
        var normalized = result.Text.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("when first\n            and second\n            and third\n\n", normalized, StringComparison.Ordinal);
        Assert.Contains("if first\n            and second\n            and third:\n\n", normalized, StringComparison.Ordinal);
        Assert.DoesNotContain("\n\n\n", normalized, StringComparison.Ordinal);
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
    public void ExplicitArrayCreationUsesExistingListLiteralSyntax()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addHandlerUseHere(a, handler: i => { foo(new Foo[] { a, b, c }); }); } }");
        Assert.Contains("foo [a, b, c]", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Status == MigrationUnitStatus.Unsupported);
    }

    [Fact]
    public void ImplicitArrayCreationUsesExistingListLiteralSyntax()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addHandlerUseHere(a, handler: i => { var xs = new[] { a, b, c }; foo(xs); }); } }");
        Assert.Contains("var xs = [a, b, c]", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Status == MigrationUnitStatus.Unsupported);
    }

    [Fact]
    public void ArrayCreationPreservesNestedExpressionsAndWorksAsNamedCutsceneArgument()
    {
        const string source = "class W { void M() { addHandlerUseHere(a, handler: i => { using (namedCutScene(ncs, roomA, new Mentionable[] { foo(x), this, \"[[translation]]\" })) { bar(); } }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source, true);
        Assert.Contains("named-cutscene ncs", result.Text, StringComparison.Ordinal);
        Assert.Contains("[foo x, this, \"[[translation]]\"]", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Reason.Contains("ArrayCreationExpression", StringComparison.Ordinal));
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
    public void NamedCutsceneTitleUsesTitleUntranslatedInsteadOfSerId()
    {
        var directory = Path.Combine(Path.GetTempPath(), "segusum-c2seg-title-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "worldObjects.cs"), "class W { NamedCutSceneId ncs = new NamedCutSceneId { serId = \"ncsId\", titleUntranslated = \"Correct title\".translatable() }; }");
            var sourcePath = Path.Combine(directory, "handlers.cs");
            var result = CSharpToSegTranspiler.Transpile(sourcePath, "class W { void M() { addHandlerUseHere(a, handler: i => { using (namedCutScene(ncs, roomA)) { dial(camilla, \"A\"); } }); } }", true);
            Assert.Contains("named-cutscene ncs \"Correct title\" roomA", result.Text, StringComparison.Ordinal);
            Assert.DoesNotContain("named-cutscene ncs \"ncsId\"", result.Text, StringComparison.Ordinal);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void NamedCutsceneTitleSupportsTargetTypedPropertyInitializerAcrossContextRoot()
    {
        var directory = Path.Combine(Path.GetTempPath(), "segusum-c2seg-property-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "worldObjects.cs"),
                "class W { public NamedCutSceneId Ncs { get; set; } = new() { titleUntranslated = \"Property title[[p]]\".translatable() }; }");
            var sourcePath = Path.Combine(directory, "handlers.cs");
            var result = CSharpToSegTranspiler.Transpile(sourcePath,
                "class W { void M() { addHandlerUseHere(a, handler: i => { using (namedCutScene(Ncs, roomA)) { dial(camilla, \"A\"); } }); } }",
                true, null, "game", directory);
            Assert.Contains("named-cutscene Ncs \"Property title[[p]]\" roomA", result.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(result.Diagnostics, x => x.Reason.Contains("title cannot be resolved", StringComparison.Ordinal));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void NamedCutsceneTitleReportsUnresolvedStaticValueWithoutInventingTitle()
    {
        var directory = Path.Combine(Path.GetTempPath(), "segusum-c2seg-unresolved-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "worldObjects.cs"),
                "class W { NamedCutSceneId ncs = new() { titleUntranslated = GetTitle() }; }");
            var result = CSharpToSegTranspiler.Transpile(Path.Combine(directory, "handlers.cs"),
                "class W { void M() { addHandlerUseHere(a, handler: i => { using (namedCutScene(ncs, roomA)) { dial(camilla, \"A\"); } }); } }",
                true, null, "game", directory);
            Assert.Contains(result.Diagnostics, x => x.Reason.Contains("title cannot be resolved", StringComparison.Ordinal));
            Assert.Contains("C2SEG-MANUAL", result.Text, StringComparison.Ordinal);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void NamedCutsceneContextIndexScansManyFilesOnceWithoutChangingResolution()
    {
        var directory = Path.Combine(Path.GetTempPath(), "segusum-c2seg-context-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            for (var i = 0; i < 100; i++)
                File.WriteAllText(Path.Combine(directory, $"unrelated{i}.cs"), $"class Unrelated{i} {{ int Value => {i}; }}");
            File.WriteAllText(Path.Combine(directory, "objects.cs"),
                "partial class W { NamedCutSceneId ncs = new NamedCutSceneId { titleUntranslated = \"Indexed title\".translatable() }; }");
            var result = CSharpToSegTranspiler.Transpile(Path.Combine(directory, "handlers.cs"),
                "partial class W { void M() { addHandlerUseHere(a, handler: i => { using (namedCutScene(ncs, roomA)) { dial(camilla, \"A\"); } }); } }",
                true, null, "game", directory);
            Assert.Contains("named-cutscene ncs \"Indexed title\" roomA", result.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(result.Diagnostics, x => x.Reason.Contains("title cannot be resolved", StringComparison.Ordinal));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void NamedCutsceneContextFlowsThroughCycleBodies()
    {
        var directory = Path.Combine(Path.GetTempPath(), "segusum-c2seg-cycle-context-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "objects.cs"),
                "partial class W { NamedCutSceneId ncs = new() { titleUntranslated = \"Cycle title\".translatable() }; }");
            var source = "partial class W { void M() { addHandlerUseHere(a, handler: i => { var cyc = startCycle(cid, x => true, x => { using (namedCutScene(ncs, roomA)) { dial(camilla, \"A\"); } }); }); } }";
            var result = CSharpToSegTranspiler.Transpile(Path.Combine(directory, "handlers.cs"), source, true, null, "game", directory);
            Assert.Contains("named-cutscene ncs \"Cycle title\" roomA", result.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(result.Diagnostics, x => x.Reason.Contains("title cannot be resolved", StringComparison.Ordinal));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void NamedCutsceneContextFlowsThroughAssignedAddToCycle()
    {
        var directory = Path.Combine(Path.GetTempPath(), "segusum-c2seg-assigned-cycle-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "objects.cs"),
                "partial class W { NamedCutSceneId ncs = new() { titleUntranslated = \"Assigned cycle title\".translatable() }; }");
            var source = "partial class W { void M() { addHandlerUseHere(a, handler: i => { var cyc = startCycle(cid, x => true, x => { }); cyc = cyc.addToCycle(cid2, x => true, x => { using (namedCutScene(ncs, roomA)) { dial(camilla, \"A\"); } }); }); } }";
            var result = CSharpToSegTranspiler.Transpile(Path.Combine(directory, "handlers.cs"), source, true, null, "game", directory);
            Assert.Contains("named-cutscene ncs \"Assigned cycle title\" roomA", result.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(result.Diagnostics, x => x.Reason.Contains("title cannot be resolved", StringComparison.Ordinal));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void NamedCutsceneIndexSeparatesTopDirectoryAndContextRootScopes()
    {
        var directory = Path.Combine(Path.GetTempPath(), "segusum-c2seg-cache-scope-" + Guid.NewGuid().ToString("N"));
        var nested = Path.Combine(directory, "nested");
        Directory.CreateDirectory(nested);
        try
        {
            File.WriteAllText(Path.Combine(nested, "objects.cs"),
                "class W { NamedCutSceneId nestedNcs = new() { titleUntranslated = \"Nested title\".translatable() }; }");
            var sourcePath = Path.Combine(directory, "handlers.cs");
            const string source = "class W { void M() { addHandlerUseHere(a, handler: i => { using (namedCutScene(nestedNcs, roomA)) { dial(camilla, \"A\"); } }); } }";

            var topOnly = CSharpToSegTranspiler.Transpile(sourcePath, source, true);
            Assert.Contains(topOnly.Diagnostics, x => x.Reason.Contains("title cannot be resolved", StringComparison.Ordinal));

            var withContext = CSharpToSegTranspiler.Transpile(sourcePath, source, true, null, "game", directory);
            Assert.Contains("named-cutscene nestedNcs \"Nested title\" roomA", withContext.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(withContext.Diagnostics, x => x.Reason.Contains("title cannot be resolved", StringComparison.Ordinal));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void NamedCutsceneTitleCanBeResolvedFromSegContext()
    {
        var directory = Path.Combine(Path.GetTempPath(), "segusum-c2seg-seg-context-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "objects.seg"), "world game\ndef existing:\n    named-cutscene ncsFromSeg \"SEG title[[x]]\" roomA:\n    end\nend\n");
            var result = CSharpToSegTranspiler.Transpile(Path.Combine(directory, "handlers.cs"),
                "class W { void M() { addHandlerUseHere(a, handler: i => { using (namedCutScene(ncsFromSeg, roomA)) { dial(camilla, \"A\"); } }); } }",
                true, null, "game", directory);
            Assert.Contains("named-cutscene ncsFromSeg \"SEG title[[x]]\" roomA", result.Text, StringComparison.Ordinal);
            Assert.DoesNotContain(result.Diagnostics, x => x.Reason.Contains("title cannot be resolved", StringComparison.Ordinal));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void NamedCutsceneTitleSameInCSharpAndSegContextIsAccepted()
    {
        var directory = Path.Combine(Path.GetTempPath(), "segusum-c2seg-seg-same-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "objects.cs"), "class W { NamedCutSceneId ncs = new() { titleUntranslated = \"Same title\".translatable() }; }");
            File.WriteAllText(Path.Combine(directory, "objects.seg"), "world game\ndef existing:\n    named-cutscene ncs \"Same title\" roomA:\n    end\nend\n");
            var result = CSharpToSegTranspiler.Transpile(Path.Combine(directory, "handlers.cs"),
                "class W { void M() { addHandlerUseHere(a, handler: i => { using (namedCutScene(ncs, roomA)) { dial(camilla, \"A\"); } }); } }",
                true, null, "game", directory);
            Assert.DoesNotContain(result.Diagnostics, x => x.Reason.Contains("title conflict", StringComparison.Ordinal));
            Assert.Contains("named-cutscene ncs \"Same title\" roomA", result.Text, StringComparison.Ordinal);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void NamedCutsceneDeclarationWithoutResolvableTitleIsDiagnostic()
    {
        var directory = Path.Combine(Path.GetTempPath(), "segusum-c2seg-missing-title-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "objects.cs"), "class W { NamedCutSceneId ncs = new() { serId = \"ncs\" }; }");
            var result = CSharpToSegTranspiler.Transpile(Path.Combine(directory, "handlers.cs"),
                "class W { void M() { addHandlerUseHere(a, handler: i => { using (namedCutScene(ncs, roomA)) { dial(camilla, \"A\"); } }); } }",
                true, null, "game", directory);
            Assert.Contains(result.Diagnostics, x => x.Reason.Contains("has no resolvable title", StringComparison.Ordinal));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void NamedCutsceneTitleConflictBetweenCSharpAndSegIsDiagnostic()
    {
        var directory = Path.Combine(Path.GetTempPath(), "segusum-c2seg-seg-conflict-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            File.WriteAllText(Path.Combine(directory, "objects.cs"), "class W { NamedCutSceneId ncs = new() { titleUntranslated = \"CSharp title\".translatable() }; }");
            File.WriteAllText(Path.Combine(directory, "objects.seg"), "world game\ndef existing:\n    named-cutscene ncs \"SEG title\" roomA:\n    end\nend\n");
            var result = CSharpToSegTranspiler.Transpile(Path.Combine(directory, "handlers.cs"),
                "class W { void M() { addHandlerUseHere(a, handler: i => { using (namedCutScene(ncs, roomA)) { dial(camilla, \"A\"); } }); } }",
                true, null, "game", directory);
            Assert.Contains(result.Diagnostics, x => x.Reason.Contains("title conflict", StringComparison.Ordinal));
        }
        finally { Directory.Delete(directory, true); }
    }

    [Fact]
    public void NewCycleExpressionEmitsExistingSegCycleLiteral()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addHandlerUseHere(a, handler: i => { var cyc = new Cycle(); execNextInCycle(cyc); }); } }", true);
        Assert.Contains("var cyc = new-cycle", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Text, "C2SEG-MANUAL", StringComparison.Ordinal);
    }

    [Fact]
    public void LinqWhereToListEmitsGeneralSegListComprehension()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addHandlerUseHere(a, handler: i => { var xs = values.Where(x => x != blocked).ToList(); use(xs); }); } }", true);
        Assert.Contains("[from values x where x != blocked select x]", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Reason.Contains("Unsupported C# call: Where", StringComparison.Ordinal));
    }

    [Fact]
    public void BareLinqWhereIsNotMaterializedAsListComprehension()
    {
        const string source = "class W { void M() { addHandlerUseHere(a, handler: i => { var filtered = values.Where(x => x.ready); }); } }";
        var result = CSharpToSegTranspiler.Transpile("where.cs", source, emitPartial: true);

        Assert.DoesNotContain("[from values", result.Text, StringComparison.Ordinal);
        Assert.Contains("C2SEG-MANUAL-BEGIN", result.Text, StringComparison.Ordinal);
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
        Assert.Contains("def Helper x: int:", result.Text, StringComparison.Ordinal);
        Assert.True(result.Text.Contains("design note", StringComparison.Ordinal), result.Text);
        Assert.Contains("TODO", result.Text, StringComparison.Ordinal);
        Assert.Contains("trailing", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void HelperParameterTypesAreMappedFromRoslynSyntax()
    {
        const string source = "class W { void M() { addHandlerUseHere(a, handler: i => { Helper(true, 1, \"x\", DateTime.Now, cyc, character, room); }); } private void Helper(bool flag, int count, string text, DateTime when, Cycle cyc, Character character, Room room) { foo(); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source, true, null, "game");
        Assert.Contains("def Helper flag: bool count: int text: string when: DateTime cyc: Cycle character: Character room: Room:", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain(result.Diagnostics, x => x.Reason.Contains("parameter type", StringComparison.Ordinal));
    }

    [Fact]
    public void HelperReturnTypeIsPreservedInSegSignature()
    {
        const string source = "class W { void M() { addRoomChangedHandler(roomA, handler: e => { var cyc = MakeCycle(true); }); } private Cycle MakeCycle(bool flag) { return startCycle(bb5, Importance.Important, Repeat.OnlyOnce, x => true, x => { }); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source, true, null, "game");
        Assert.Contains("def MakeCycle flag: bool ret Cycle:", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void UnsupportedHelperParameterTypeIsReportedWithoutDroppingTheType()
    {
        const string source = "class W { void M() { addHandlerUseHere(a, handler: i => { Helper(default); }); } private void Helper(List<int> value) { foo(); } }";
        var result = CSharpToSegTranspiler.Transpile("x.cs", source, true, null, "game");
        Assert.Contains("def Helper value: List<int>:", result.Text, StringComparison.Ordinal);
        Assert.Contains(result.Diagnostics, x => x.Reason.Contains("parameter type", StringComparison.Ordinal));
    }

    [Fact]
    public void WorldIdIsExplicitAndNeverUsesMigratedPlaceholder()
    {
        var result = CSharpToSegTranspiler.Transpile("x.cs", "class W { void M() { addRoomChangedHandler(roomA, e => { foo(); }); } }", true, null, "game");
        Assert.StartsWith("world game\n", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("world migrated", result.Text, StringComparison.Ordinal);
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

    [Fact]
    public void HelperDependencyStatusPropagatesThroughTheWholeChain()
    {
        const string source = "class W { void M() { addHandlerUseHere(a, handler: i => { A(); Independent(); }); } private void A() { B(); } private void B() { C(); } private void C() { while (true) { } } private void Independent() { foo(); } }";
        var result = CSharpToSegTranspiler.Transpile("dependency-chain.cs", source);

        Assert.Equal(MigrationUnitStatus.DependsOnCSharpHelper, result.Units.Single(x => x.Id == "A").Status);
        Assert.Equal(MigrationUnitStatus.DependsOnCSharpHelper, result.Units.Single(x => x.Id == "B").Status);
        Assert.Equal(MigrationUnitStatus.Unsupported, result.Units.Single(x => x.Id == "C").Status);
        Assert.Equal(MigrationUnitStatus.Translated, result.Units.Single(x => x.Id == "Independent").Status);
    }

    [Fact]
    public void LocalFunctionsWithTheSameNameUseCapturesFromTheirOwnMethod()
    {
        const string source = "class W { void beforeRoomChangeManual() { int first = 1; int helper() { return first; } foo(helper()); } void afterActionExecutedCSharp() { int second = 2; int helper() { return second; } foo(helper()); } }";
        var result = CSharpToSegTranspiler.Transpile("local-functions.cs", source);

        Assert.Contains("foo (helper first)", result.Text, StringComparison.Ordinal);
        Assert.Contains("foo (helper second)", result.Text, StringComparison.Ordinal);
        Assert.Contains("def helper first: int ret int", result.Text, StringComparison.Ordinal);
        Assert.Contains("def helper second: int ret int", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ReachableHelperIndexPreservesDirectAndTransitiveCallSemantics()
    {
        const string source = "class W { void M() { addHandlerUseHere(a, handler: i => { Direct(); }); } private void Direct() { Nested(); } private void Nested() { leaf(); } private void Uncalled() { never(); } }";
        var result = CSharpToSegTranspiler.Transpile("helpers.cs", source);

        Assert.Contains("def Direct", result.Text, StringComparison.Ordinal);
        Assert.Contains("def Nested", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("def Uncalled", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void MissingRequestedMethodDoesNotBuildHelperGraphOrUnits()
    {
        const string source = "class W { void Configure() { addHandlerUseHere(a, handler: i => { Direct(); }); } private void Direct() { Nested(); } private void Nested() { leaf(); } }";

        var result = CSharpToSegTranspiler.Transpile("missing-method.cs", source, emitPartial: true, methodName: "__none__", worldId: "game");

        Assert.Empty(result.Units);
        Assert.DoesNotContain("def Direct", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("def Nested", result.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void RequestedMethodIncludesOnlyItsTransitiveHelpers()
    {
        const string source = "class W { void Configure() { addHandlerUseHere(a, handler: i => { Direct(); }); } private void Direct() { Nested(); } private void Nested() { leaf(); } private void Uncalled() { never(); } }";

        var result = CSharpToSegTranspiler.Transpile("single-method.cs", source, emitPartial: true, methodName: "Configure", worldId: "game");

        Assert.Contains("def Direct", result.Text, StringComparison.Ordinal);
        Assert.Contains("def Nested", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("def Uncalled", result.Text, StringComparison.Ordinal);
        Assert.Contains(result.Units, x => x.Id == "Direct");
        Assert.Contains(result.Units, x => x.Id == "Nested");
    }

    [Fact]
    public void ManyRegistrationsUseOneReachabilityIndex()
    {
        var registrations = string.Join(" ", Enumerable.Range(0, 32)
            .Select(i => $"addHandlerUseHere(a{i}, handler: i => {{ Shared(); }});"));
        var source = $"class W {{ void M() {{ {registrations} }} private void Shared() {{ leaf(); }} private void Uncalled() {{ never(); }} }}";

        var result = CSharpToSegTranspiler.Transpile("many-registrations.cs", source);

        Assert.Contains("def Shared", result.Text, StringComparison.Ordinal);
        Assert.DoesNotContain("def Uncalled", result.Text, StringComparison.Ordinal);
        Assert.Equal(32, result.Units.Count(x => x.Id.StartsWith("use-here:", StringComparison.Ordinal)));
    }

    [Fact]
    public void CommentInventoryPreservesLineAndBlockCommentsWithCardinality()
    {
        const string source = "class W { /* design note */ void M() { // before\n addHandlerUseHere(a, handler: i => { /* inside */ foo(); // trailing\n }); } }";
        var result = CSharpToSegTranspiler.Transpile("comments.cs", source, emitPartial: true);

        Assert.True(result.CommentsPreserved);
        Assert.Equal(result.SourceComments.Count, result.GeneratedComments.Count);
        Assert.Equal(4, result.SourceComments.Count);
    }
}
