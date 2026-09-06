using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Segusum.Scripting.Core;
using Segusum.Scripting.Tooling;

namespace Segusum.Scripting.Generator.Tests;

public sealed class MigrationVerificationTests
{
    [Fact]
    public void CycleFingerprintPreservesStartMetadataPredicateBodyAndElementOrder()
    {
        const string csharp = "class W { void M() { var cyc = startCycle(bb5, Importance.Important, Repeat.OnlyOnce, x => x.notSeenRecently(30), x => { }); cyc.addToCycle(mammaaiuti2, x => x.notSeenRecently(20), x => { }); } }";
        const string dsl = "world game\ndef cycle:\n    var cyc = new-cycle\n    add cyc bb5 important once\n     when it not-seen-recently 30\n    end\n    add cyc mammaaiuti2\n     when it not-seen-recently 20\n    end\nend\n";
        var left = MigrationVerifier.ExtractCSharpCycles("baseline.cs", csharp);
        var right = MigrationVerifier.ExtractDslCycles(new DslSource("current.seg", dsl));
        var check = MigrationVerifier.CompareCycles(left, right);
        Assert.True(check.Status == EquivalenceStatus.Pass, check.Detail);
    }

    [Fact]
    public void CycleFingerprintRejectsMetadataPredicateAndElementChanges()
    {
        const string csharp = "class W { void M() { var cyc = startCycle(bb5, Importance.Important, Repeat.OnlyOnce, x => x.notSeenRecently(30), x => { }); cyc.addToCycle(mammaaiuti2, x => x.notSeenRecently(20), x => { }); } }";
        const string dsl = "world game\ndef cycle:\n    var cyc = new-cycle\n    add cyc bb5 once\n     when it not-seen-recently 20\n    end\n    add cyc otherId\n    end\nend\n";
        Assert.Equal(EquivalenceStatus.Fail, MigrationVerifier.CompareCycles(
            MigrationVerifier.ExtractCSharpCycles("baseline.cs", csharp),
            MigrationVerifier.ExtractDslCycles(new DslSource("current.seg", dsl))).Status);
    }

    [Fact]
    public void CycleLambdaParameterRenameIsIgnoredButOrderIsNot()
    {
        const string first = "class W { void M() { startCycle(bb5, x => x.notSeenRecently(30), x => { dial(camilla, \"First\"); }); } }";
        const string second = "class W { void M() { startCycle(bb5, lastTime => lastTime.notSeenRecently(30), x => { dial(camilla, \"First\"); }); } }";
        Assert.Equal(MigrationVerifier.ExtractCSharpCycles("a.cs", first)[0].Predicate, MigrationVerifier.ExtractCSharpCycles("b.cs", second)[0].Predicate);
        const string reordered = "class W { void M() { startCycle(bb5, x => x.notSeenRecently(30), x => { dial(camilla, \"Changed\"); }); } }";
        Assert.NotEqual(MigrationVerifier.ExtractCSharpCycles("a.cs", first)[0].BodyEffects, MigrationVerifier.ExtractCSharpCycles("c.cs", reordered)[0].BodyEffects);
    }

    [Fact]
    public void HandlerRegistrationsMatchByStructureAndReportAddedMissingAndOrder()
    {
        const string csharp = "class W { void M() { addHandlerUseFor(a, oa, e => { }); addHandlerUseFor(b, ob, e => { }); } }";
        const string dsl = "world game\nuse b for ob:\nend\nuse a for oa:\nend\nuse c for oc:\nend\n";
        var results = MigrationVerifier.VerifyHandlerRegistrations("baseline.cs", csharp, new DslSource("current.seg", dsl));
        Assert.Contains(results.SelectMany(x => x.Checks), x => x.Detail == "OrderMismatch");
        Assert.Contains(results.SelectMany(x => x.Checks), x => x.Detail == "AddedHandlerRegistration");
    }

    [Fact]
    public void FluentCycleChainExtractsAllElementsInSourceOrder()
    {
        const string csharp = "class W { void M() { var cyc = startCycle(bb5, Importance.Important, Repeat.OnlyOnce, x => x.notSeenRecently(30), x => { }).addToCycle(mammaaiuti2, x => x.notSeenRecently(20), x => { }).addToCycle(mammaaiuti3, x => x.notSeenRecently(25), x => { }); } }";
        var cycle = Assert.Single(MigrationVerifier.ExtractCSharpCycles("worldOnRoomChanged.cs", csharp));
        Assert.Equal(new[] { "mammaaiuti2", "mammaaiuti3" }, cycle.Elements.Select(x => x.Id));
        Assert.Equal("$cycleElement.notSeenRecently(20)", cycle.Elements[0].Predicate);
        Assert.Equal("$cycleElement.notSeenRecently(25)", cycle.Elements[1].Predicate);
    }

    [Fact]
    public void FluentAndSeparateCycleFormsHaveEquivalentFingerprints()
    {
        const string fluent = "class W { void M() { var cyc = startCycle(bb5, Importance.Important, Repeat.OnlyOnce, x => x.notSeenRecently(30), x => { }).addToCycle(mammaaiuti2, x => x.notSeenRecently(20), x => { }).addToCycle(mammaaiuti3, x => x.notSeenRecently(25), x => { }); } }";
        const string separate = "class W { void M() { var cyc = startCycle(bb5, Importance.Important, Repeat.OnlyOnce, x => x.notSeenRecently(30), x => { }); cyc.addToCycle(mammaaiuti2, x => x.notSeenRecently(20), x => { }); cyc.addToCycle(mammaaiuti3, x => x.notSeenRecently(25), x => { }); } }";
        Assert.Equal(EquivalenceStatus.Pass, MigrationVerifier.CompareCycles(
            MigrationVerifier.ExtractCSharpCycles("fluent.cs", fluent), MigrationVerifier.ExtractCSharpCycles("separate.cs", separate)).Status);
    }

    [Fact]
    public void FluentCycleOrderAndCardinalityDifferencesFailAgainstDsl()
    {
        const string csharp = "class W { void M() { var cyc = startCycle(bb5, Importance.Important, Repeat.OnlyOnce, x => x.notSeenRecently(30), x => { }).addToCycle(mammaaiuti2, x => x.notSeenRecently(20), x => { }).addToCycle(mammaaiuti3, x => x.notSeenRecently(25), x => { }); } }";
        const string dsl = "world game\ndef cycle:\n    var cyc = new-cycle\n    add cyc bb5 important once\n     when it not-seen-recently 30\n    end\n    add cyc mammaaiuti3\n     when it not-seen-recently 25\n    end\nend\n";
        Assert.Equal(EquivalenceStatus.Fail, MigrationVerifier.CompareCycles(
            MigrationVerifier.ExtractCSharpCycles("fluent.cs", csharp), MigrationVerifier.ExtractDslCycles(new DslSource("current.seg", dsl))).Status);
    }

    [Fact]
    public void UnknownCycleEnumValueIsInconclusive()
    {
        const string csharp = "class W { void M() { startCycle(bb5, Importance.Unknown, Repeat.OnlyOnce, x => x.notSeenRecently(30), x => { }); } }";
        var cycle = Assert.Single(MigrationVerifier.ExtractCSharpCycles("unknown.cs", csharp));
        Assert.StartsWith("unverifiable:Importance.Unknown", cycle.Importance, StringComparison.Ordinal);
        Assert.Equal(EquivalenceStatus.Inconclusive, MigrationVerifier.CompareCycles(new[] { cycle }, new[] { cycle }).Status);
    }

    [Fact]
    public void GameplayVerifierNormalizesEquivalentIfAndElseIfConditions()
    {
        const string csharp = "class W { void afterActionExecutedCSharp() { if (objectiveIsCurrent(puX) && !olivia.hasObject(obj)) { foo(obj); } else if (ready || !blocked) { bar(); } } }";
        const string dsl = "world game\nafter-action-executed:\n    if objectiveIsCurrent puX and not olivia.hasObject obj:\n        foo obj\n    elif ready or not blocked:\n        bar\n    end\nend\n";
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", csharp, "afterActionExecutedCSharp", new DslSource("current.seg", dsl));
        Assert.True(report.IsClean, report.ToText());
        Assert.Equal(new[] { "if", "else-if" }, report.CSharpBranches.Select(x => x.Kind));
        Assert.Equal(new[] { "if", "else-if" }, report.DslBranches.Select(x => x.Kind));
    }

    [Fact]
    public void GameplayVerifierReportsMissingBranchAndMissingSideEffect()
    {
        const string csharp = "class W { void M() { if (ready) { pickUp(obj); } else if (fallback) { changeRoom(room); } } }";
        const string dsl = "world game\nafter-action-executed:\n    if ready:\n        pickUp obj\n    end\nend\n";
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", csharp, "M", new DslSource("current.seg", dsl));
        Assert.Contains(report.Findings, x => x.Kind == MigrationFindingKind.MissingBranch && x.Status == MigrationMatchStatus.MissingCandidate);
        Assert.Contains(report.Findings, x => x.Message.Contains("changeRoom", StringComparison.Ordinal));

        var effectReport = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", "class W { void M() { if (ready) { pickUp(obj); } } }",
            "M", new DslSource("current.seg", "world game\nafter-action-executed:\n    if ready:\n        bar\n    end\nend\n"));
        Assert.Contains(effectReport.Findings, x => x.Kind == MigrationFindingKind.MissingSideEffect && x.Message.Contains("pickUp", StringComparison.Ordinal));
    }

    [Fact]
    public void GameplayVerifierReportsReorderedBranches()
    {
        const string csharp = "class W { void M() { if (first) { alpha(); } else if (second) { beta(); } } }";
        const string dsl = "world game\nafter-action-executed:\n    if second:\n        beta\n    elif first:\n        alpha\n    end\nend\n";
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", csharp, "M", new DslSource("current.seg", dsl));
        Assert.Contains(report.Findings, x => x.Kind == MigrationFindingKind.OrderMismatch);
    }

    [Fact]
    public void GameplayVerifierInventoriesAssignmentsIncrementsCallsAndStrings()
    {
        const string csharp = "class W { void M() { if (ready) { value = DateTime.Now; count++; dial(olivia, \"Ciao\"); narText(\"Narr\"); } } }";
        const string dsl = "world game\nafter-action-executed:\n    if ready:\n        value = DateTime.Now\n        count++\n        olivia: Ciao\n        narText \"Narr\"\n    end\nend\n";
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", csharp, "M", new DslSource("current.seg", dsl));
        Assert.True(report.IsClean, report.ToText());
        Assert.Contains(report.CSharpEffects, x => x.Kind == "assign");
        Assert.Contains(report.CSharpEffects, x => x.Kind == "increment");
        Assert.Equal(new[] { "Ciao", "Narr" }, report.CSharpStrings);
        Assert.Equal(report.CSharpStrings, report.DslStrings);
    }

    [Fact]
    public void GameplayVerifierRecognizesMemberCallAndPickupEffects()
    {
        const string csharp = "class W { void M() { if (ready) { olivia.pickUp(obj); olivia.putInRoom(room); } } }";
        const string dsl = "world game\nafter-action-executed:\n    if ready:\n        olivia.pickUp obj\n        olivia.putInRoom room\n    end\nend\n";
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", csharp, "M", new DslSource("current.seg", dsl));
        Assert.True(report.IsClean, report.ToText());
        Assert.Contains(report.DslEffects, x => x.Kind == "pickUp");
        Assert.Contains(report.DslEffects, x => x.Kind == "putInRoom");
    }

    [Fact]
    public void GameplayVerifierMarksUnparseableOrMissingInputsUnverifiable()
    {
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", "class W { void Other() {} }", "M", new DslSource("current.seg", "world game\n"));
        Assert.Contains(report.Findings, x => x.Status == MigrationMatchStatus.Unverifiable);
    }

    [Fact]
    public void GameplayVerifierDetectsEffectMovedAcrossNestedBranch()
    {
        var csharp = "class W { void M() { if (A) { if (B) { foo(); } } } }";
        var dsl = "world game\nafter-action-executed:\n    if A:\n        foo\n        if B:\n            bar\n        end\n    end\nend\n";
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", csharp, "M", new DslSource("current.seg", dsl));
        Assert.Contains(report.Findings, x => x.Kind is MigrationFindingKind.MissingSideEffect or MigrationFindingKind.AddedSideEffect);
    }

    [Fact]
    public void GameplayVerifierKeepsDuplicateConditionsInTheirScopes()
    {
        var csharp = "class W { void M() { if (A) { if (same) { outer(); } } if (same) { root(); } } }";
        var dsl = "world game\nafter-action-executed:\n    if A:\n        if same:\n            root\n        end\n    end\n    if same:\n        outer\n    end\nend\n";
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", csharp, "M", new DslSource("current.seg", dsl));
        Assert.Contains(report.Findings, x => x.Kind is MigrationFindingKind.MissingSideEffect or MigrationFindingKind.AddedSideEffect);
    }

    [Fact]
    public void GameplayVerifierReportsMissingNestedBranch()
    {
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", "class W { void M() { if (A) { if (B) { foo(); } } } }", "M",
            new DslSource("current.seg", "world game\nafter-action-executed:\n    if A:\n        bar\n    end\nend\n"));
        Assert.Contains(report.Findings, x => x.Kind == MigrationFindingKind.MissingBranch);
    }

    [Fact]
    public void GameplayVerifierReportsNestedElseIfReordering()
    {
        var csharp = "class W { void M() { if (A) { if (B) { b(); } else if (C) { c(); } } } }";
        var dsl = "world game\nafter-action-executed:\n    if A:\n        if C:\n            c\n        elif B:\n            b\n        end\n    end\nend\n";
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", csharp, "M", new DslSource("current.seg", dsl));
        Assert.Contains(report.Findings, x => x.Kind == MigrationFindingKind.OrderMismatch);
    }

    [Fact]
    public void GameplayVerifierReportsEffectMovedToAnotherSiblingBranch()
    {
        var csharp = "class W { void M() { if (A) { foo(); } else if (B) { bar(); } } }";
        var dsl = "world game\nafter-action-executed:\n    if A:\n        bar\n    elif B:\n        foo\n    end\nend\n";
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", csharp, "M", new DslSource("current.seg", dsl));
        Assert.Contains(report.Findings, x => x.Kind == MigrationFindingKind.OrderMismatch);
    }

    [Theory]
    [InlineData("A && B && C", "A and C and B")]
    [InlineData("A || B", "B or A")]
    public void GameplayVerifierDoesNotCommuteBooleanTerms(string csharpCondition, string dslCondition)
    {
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", $"class W {{ void M() {{ if ({csharpCondition}) {{ foo(); }} }} }}", "M",
            new DslSource("current.seg", $"world game\nafter-action-executed:\n    if {dslCondition}:\n        foo\n    end\nend\n"));
        Assert.Contains(report.Findings, x => x.Kind == MigrationFindingKind.ChangedBranch);
    }

    [Theory]
    [InlineData("foo(a,b,c)", "foo a c b")]
    [InlineData("foo(a,b,c)", "foo a b")]
    [InlineData("foo(a,b,c)", "foo a b c d")]
    public void GameplayVerifierRequiresExactArgumentSequence(string csharpCall, string dslCall)
    {
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", $"class W {{ void M() {{ if (A) {{ {csharpCall}; }} }} }}", "M",
            new DslSource("current.seg", $"world game\nafter-action-executed:\n    if A:\n        {dslCall}\n    end\nend\n"));
        Assert.False(report.IsClean);
    }

    [Fact]
    public void GameplayVerifierPreservesNamedArguments()
    {
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", "class W { void M() { if (A) { foo(first: x, second: y); } } }", "M",
            new DslSource("current.seg", "world game\nafter-action-executed:\n    if A:\n        foo first: x second: z\n    end\nend\n"));
        Assert.False(report.IsClean);
    }

    [Fact]
    public void GameplayVerifierReportsUnsupportedCSharpStatementAsUnverifiable()
    {
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", "class W { void M() { if (A) { while (ready) { foo(); } } } }", "M",
            new DslSource("current.seg", "world game\nafter-action-executed:\n    if A:\n        foo\n    end\nend\n"));
        Assert.Contains(report.Findings, x => x.Status == MigrationMatchStatus.Unverifiable && x.Message.Contains("WhileStatementSyntax", StringComparison.Ordinal));
        Assert.False(report.IsClean);
    }

    [Fact]
    public void GameplayVerifierSupportsInterleavedMigratedAndRemainingBranches()
    {
        var csharp = "class W { void M() { if (A) { a(); } else if (B) { b(); } else if (C) { c(); } } }";
        var dsl = "world game\nafter-action-executed:\n    if A:\n        a\n    elif C:\n        c\n    end\nend\n";
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", csharp, "M", new DslSource("current.seg", dsl),
            options: new MigrationVerificationOptions(x => x.Condition is "A" or "C"));
        Assert.True(report.IsClean, report.ToText());
        Assert.Equal(2, report.MigratedCSharpBranches.Count);
        Assert.Single(report.RemainingCSharpBranches);
    }

    [Fact]
    public void GameplayVerifierPreservesNestedCallArgumentOrder()
    {
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", "class W { void M() { if (A) { foo(a(), b()); } } }", "M",
            new DslSource("current.seg", "world game\nafter-action-executed:\n    if A:\n        foo b() a()\n    end\nend\n"));
        Assert.False(report.IsClean);
    }

    [Fact]
    public void GameplayVerifierUsesExactDirectEffectCardinality()
    {
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", "class W { void M() { if (A) { foo(); foo(); } } }", "M",
            new DslSource("current.seg", "world game\nafter-action-executed:\n    if A:\n        foo\n    end\nend\n"));
        Assert.Contains(report.Findings, x => x.Kind == MigrationFindingKind.MissingSideEffect);

        report = GameplayMigrationVerifier.VerifyCSharpToDsl("baseline.cs", "class W { void M() { if (A) { olivia.Aspect += 1; } } }", "M",
            new DslSource("current.seg", "world game\nafter-action-executed:\n    if A:\n        olivia.Aspect = 1\n    end\nend\n"));
        Assert.Contains(report.Findings, x => x.Status == MigrationMatchStatus.ChangedCandidate);
    }

    [Fact]
    public void HandlerRegistrationPreservesPossibleWhenAndMakesNoSense()
    {
        const string csharp = "class W { void C() { addHandlerCombine(a, b, \"say\", isPossibleNow: () => objectiveIsCurrent(goal), handler: i => { i.makesNoSenseAtThisTime = true; }); } }";
        const string dsl = "world game\ncombine a with b:\n    phrase \"say\"\n    possible-when objectiveIsCurrent goal\n    makes-no-sense\nend\n";
        var result = SingleRegistration(csharp, dsl);
        Assert.Equal(EquivalenceStatus.Pass, result.Overall);
    }

    [Fact]
    public void HandlerRegistrationReportsMetadataAndExplanationChanges()
    {
        const string csharp = "class W { void C() { addHandlerCombine(a, b, \"say\", isPossibleNow: () => A && B, handler: i => { }); addHandlerUseFor(item, objective, explanation, handler: e => { }); } }";
        var missing = MigrationVerifier.ExtractDslRegistrations(new DslSource("x.seg", "world game\ncombine a with b:\n    phrase \"say\"\nend\nuse item for objective:\nend\n"));
        var left = MigrationVerifier.ExtractCSharpRegistrations("x.cs", csharp);
        var first = MigrationVerifier.CompareRegistration(left[0], missing[0]);
        var second = MigrationVerifier.CompareRegistration(left[1], missing[1]);
        Assert.Contains(first.Checks, x => x.Detail == "MissingHandlerMetadata");
        Assert.Contains(second.Checks, x => x.Detail == "MissingExplanation");

        var changed = MigrationVerifier.ExtractDslRegistrations(new DslSource("x.seg", "world game\ncombine a with b:\n    phrase \"say\"\n    possible-when A and C\nend\nuse item for objective:\n    exp different\nend\n"));
        Assert.Contains(MigrationVerifier.CompareRegistration(left[0], changed[0]).Checks, x => x.Detail?.StartsWith("ChangedHandlerMetadata", StringComparison.Ordinal) == true);
        Assert.Contains(MigrationVerifier.CompareRegistration(left[1], changed[1]).Checks, x => x.Detail?.StartsWith("ChangedExplanation", StringComparison.Ordinal) == true);
    }

    [Fact]
    public void HandlerRegistrationMapsTextInputMutationAndDetectsValueChange()
    {
        const string csharp = "class W { void C() { addHandlerUseHere(item, handler: e => { e.textInputToShow = tiCustode; }); } }";
        var clean = MigrationVerifier.CompareRegistration(
            MigrationVerifier.ExtractCSharpRegistrations("x.cs", csharp)[0],
            MigrationVerifier.ExtractDslRegistrations(new DslSource("x.seg", "world game\nuse item here:\n    text-input tiCustode\nend\n"))[0]);
        Assert.Equal(EquivalenceStatus.Pass, clean.Overall);
        var changed = MigrationVerifier.CompareRegistration(
            MigrationVerifier.ExtractCSharpRegistrations("x.cs", csharp)[0],
            MigrationVerifier.ExtractDslRegistrations(new DslSource("x.seg", "world game\nuse item here:\n    text-input tiOther\nend\n"))[0]);
        Assert.Contains(changed.Checks, x => x.Name == "body" && x.Status == EquivalenceStatus.Fail);
    }

    [Fact]
    public void UnsupportedDslHandlerStatementCannotProduceCleanResult()
    {
        var result = MigrationVerifier.CompareRegistration(
            new HandlerRegistrationFingerprint("use-here", "item", null, null, null, null, "x.cs", 1, new[] { "unverifiable:FutureStatement" }),
            new HandlerRegistrationFingerprint("use-here", "item", null, null, null, null, "x.seg", 1, new[] { "unverifiable:FutureStatement" }));
        Assert.Equal(EquivalenceStatus.Inconclusive, result.Overall);
    }

    private static HandlerEquivalenceResult SingleRegistration(string csharp, string dsl)
        => MigrationVerifier.CompareRegistration(
            Assert.Single(MigrationVerifier.ExtractCSharpRegistrations("x.cs", csharp)),
            Assert.Single(MigrationVerifier.ExtractDslRegistrations(new DslSource("x.seg", dsl))));

    [Fact]
    public void GameplayVerifierSmokeTestsTheAfterActionBaselineAgainstCurrentLitgirSeg()
    {
        var segusumRoot = FindAncestorWithDirectory(AppContext.BaseDirectory, "Segusum");
        var litgirRoot = segusumRoot is null ? null : Path.Combine(Directory.GetParent(segusumRoot)!.FullName, "litgir");
        if (litgirRoot is null || !Directory.Exists(litgirRoot)) return;

        var baseline = GitShow(litgirRoot, "cc193814258f564b4a00c3ab8f3c7bb77379713a", "WebApiLitGir/worldAfterActionExecuted.cs");
        var segPath = Path.Combine(litgirRoot, "WebApiLitGir", "Gameplay", "AfterActionExecuted.seg");
        var report = GameplayMigrationVerifier.VerifyCSharpToDsl("WebApiLitGir/worldAfterActionExecuted.cs", baseline,
            "afterActionExecutedCSharp", new DslSource("WebApiLitGir/Gameplay/AfterActionExecuted.seg", File.ReadAllText(segPath)),
            options: new MigrationVerificationOptions(x => x.SourceLine < 640));
        Console.WriteLine("REAL AFTER-ACTION MIGRATION REPORT\n" + report.ToText());

        var butler = report.CSharpBranches.FirstOrDefault(x => x.Condition?.Contains("capisciQualcosaDiImportanteSulMaggiordomo", StringComparison.Ordinal) == true);
        Assert.NotNull(butler);
        Assert.Contains(report.DslBranches, x => x.Condition?.Contains("capisciQualcosaDiImportanteSulMaggiordomo", StringComparison.Ordinal) == true);
        Assert.DoesNotContain(report.Findings, x => x.Kind == MigrationFindingKind.MissingBranch && x.CSharpSourceLine == butler!.SourceLine);
    }

    [Fact]
    public void RegistrationAndStringsMatchForTheMigratedMareShape()
    {
        const string csharp = "void Configure() { addHandlerUseFor(mareSpiaggia, puTrovareDente2, e => { if (mareVerde.is_in_world()) { nuotano(); } else { considerano(); } }); }";
        var csharpRegistration = Assert.Single(MigrationVerifier.ExtractCSharpRegistrations("worldActionHandlers.cs", csharp));
        var dsl = new DslSource("Gameplay/Mare.seg", "world game\nuse mareSpiaggia for puTrovareDente2:\n    if mareVerde.is_in_world:\n        nuotano\n    else:\n        considerano\n    end\nend\n");
        var dslRegistration = Assert.Single(MigrationVerifier.ExtractDslRegistrations(dsl));
        var result = MigrationVerifier.CompareRegistration(csharpRegistration, dslRegistration);

        Assert.Equal(EquivalenceStatus.Pass, result.Overall);
        Assert.Equal(EquivalenceStatus.Pass, result.Checks[0].Status);
    }

    [Fact]
    public void RegistrationVerifierFailsOnWrongOperandAndString()
    {
        var csharp = Assert.Single(MigrationVerifier.ExtractCSharpRegistrations("handlers.cs", "void Configure() { addHandlerUseFor(mareVerde, objective, e => { dial(camilla, \"Ciao\"); }); }"));
        var dsl = Assert.Single(MigrationVerifier.ExtractDslRegistrations(new DslSource("handlers.seg", "world game\nuse mareSpiaggia for objective:\n    camilla: Ciao diverso\nend\n")));
        var result = MigrationVerifier.CompareRegistration(csharp, dsl);

        Assert.Equal(EquivalenceStatus.Fail, result.Overall);
        Assert.Contains(result.Checks, x => x.Name == "first operand" && x.Status == EquivalenceStatus.Fail);
        Assert.Contains(result.Checks, x => x.Name == "phrase" && x.Status == EquivalenceStatus.Pass);
        var strings = MigrationVerifier.CompareStrings(new[] { "Ciao" }, MigrationVerifier.ExtractDslStrings(new DslSource("handlers.seg", "world game\nuse mareSpiaggia for objective:\n    camilla: Ciao diverso\nend\n")));
        Assert.Equal(EquivalenceStatus.Fail, strings.Status);
    }

    [Fact]
    public void MissingSideIsInconclusiveAndDslStringsKeepOrder()
    {
        var missing = MigrationVerifier.CompareRegistration(null, null);
        Assert.Equal(EquivalenceStatus.Inconclusive, missing.Overall);

        var source = new DslSource("Gameplay/Test.seg", "world game\nuse olivia here:\n    olivia: Prima\n    nar: Seconda\n    camilla: Terza\nend\n");
        Assert.Equal(new[] { "Prima", "Seconda", "Terza" }, MigrationVerifier.ExtractDslStrings(source));
    }

    [Fact]
    public void OperationFingerprintDetectsChangedInvocationAndLiteral()
    {
        var refs = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!.Split(System.IO.Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        var tree = CSharpSyntaxTree.ParseText("class C { void A() { Foo(1); } void B() { Bar(2); } void Foo(int x) {} void Bar(int x) {} }");
        var compilation = CSharpCompilation.Create("MigrationOps", new[] { tree }, refs);
        var model = compilation.GetSemanticModel(tree);
        var invocations = tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>().ToArray();

        var first = model.GetOperation(invocations[0]);
        var second = model.GetOperation(invocations[1]);
        Assert.NotNull(first);
        Assert.NotNull(second);
        var check = MigrationVerifier.CompareOperations(first, second);
        Assert.Equal(EquivalenceStatus.Fail, check.Status);
        Assert.Contains("invoke:", check.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void OperationFingerprintDetectsAssignmentAndConditionChanges()
    {
        var refs = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!.Split(System.IO.Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        var tree = CSharpSyntaxTree.ParseText("class C { int Value; void A(bool ok) { if (ok) Value = 1; } void B(bool ok) { if (!ok) Value = 2; } }");
        var compilation = CSharpCompilation.Create("MigrationOps2", new[] { tree }, refs);
        var model = compilation.GetSemanticModel(tree);
        var methods = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().Where(x => x.Identifier.ValueText is "A" or "B").ToArray();

        var check = MigrationVerifier.CompareOperations(model.GetOperation(methods.Single(x => x.Identifier.ValueText == "A").Body!), model.GetOperation(methods.Single(x => x.Identifier.ValueText == "B").Body!));
        Assert.Equal(EquivalenceStatus.Fail, check.Status);
        Assert.Contains("assign", check.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistrationStringExtractionDoesNotDuplicateNestedDialogueCalls()
    {
        const string csharp = "void Configure() { addRoomChangedHandler(roomA, e => { var c = startCycle(id, x => x.notSeenRecently(1), x => { dial(camilla, \"One\"); }); execNextInCycle(c); }); }";
        var strings = MigrationVerifier.ExtractCSharpStringsForRegistration("rooms.cs", csharp, "room-changed", "roomA", null);
        Assert.Equal(new[] { "One" }, strings);

        var global = MigrationVerifier.ExtractCSharpStrings("rooms.cs", "void M() { outer(inner(dial(camilla, \"Uno\"))); }");
        Assert.Equal(new[] { "Uno" }, global);
    }

    [Fact]
    public void RegistrationStringExtractionIncludesLocalHelperClosureOnce()
    {
        const string csharp = "class W { void Configure() { addHandlerUseFor(item, objective, e => { helper(); }); } void helper() { dial(camilla, \"One\"); dial(olivia, \"Two\"); } }";
        var strings = MigrationVerifier.ExtractCSharpStringsForRegistration("handlers.cs", csharp, "use-for", "item", "objective");
        Assert.Equal(new[] { "One", "Two" }, strings);
    }

    [Fact]
    public void MarkHappenedOnceMatchesDomainRefCall()
    {
        const string csharp = "void Configure() { setIfNeverHappened(ref ftDatoImodiumADracula); }";
        const string dsl = "world game\nuse imodium here:\n    mark-happened-once ftDatoImodiumADracula\nend\n";
        var left = MigrationVerifier.ExtractCSharpMarkHappenedOnce("handlers.cs", csharp);
        var right = MigrationVerifier.ExtractDslMarkHappenedOnce(new DslSource("handlers.seg", dsl));
        Assert.Equal(EquivalenceStatus.Pass, MigrationVerifier.CompareMarkHappenedOnce(left, right).Status);
    }

    [Theory]
    [InlineData("mark-happened-once ftOther")]
    [InlineData("")]
    public void MarkHappenedOnceFailsWhenTargetOrStatementDiffers(string statement)
    {
        const string csharp = "void Configure() { setIfNeverHappened(ref ftDatoImodiumADracula); }";
        var dsl = $"world game\nuse imodium here:\n    {statement}\nend\n";
        var left = MigrationVerifier.ExtractCSharpMarkHappenedOnce("handlers.cs", csharp);
        var right = MigrationVerifier.ExtractDslMarkHappenedOnce(new DslSource("handlers.seg", dsl));
        Assert.Equal(EquivalenceStatus.Fail, MigrationVerifier.CompareMarkHappenedOnce(left, right).Status);
    }

    [Fact]
    public void MarkHappenedOnceFingerprintRejectsDifferentCSharpTarget()
    {
        const string csharp = "void Configure() { setIfNeverHappened(ref ftOther); }";
        const string dsl = "world game\nuse imodium here:\n    mark-happened-once ftDatoImodiumADracula\nend\n";
        Assert.Equal(EquivalenceStatus.Fail, MigrationVerifier.CompareMarkHappenedOnce(
            MigrationVerifier.ExtractCSharpMarkHappenedOnce("handlers.cs", csharp),
            MigrationVerifier.ExtractDslMarkHappenedOnce(new DslSource("handlers.seg", dsl))).Status);
    }

    [Fact]
    public void MarkHappenedMatchesDateTimeNowAssignment()
    {
        const string csharp = "void Configure() { stamp = DateTime.Now; }";
        const string dsl = "world game\ndef mark:\n    mark-happened stamp\nend\n";
        Assert.Equal(EquivalenceStatus.Pass, MigrationVerifier.CompareMarkHappened(
            MigrationVerifier.ExtractCSharpMarkHappened("handlers.cs", csharp),
            MigrationVerifier.ExtractDslMarkHappened(new DslSource("handlers.seg", dsl))).Status);
    }

    [Theory]
    [InlineData("mark-happened other")]
    [InlineData("")]
    public void MarkHappenedFailsWhenTargetDiffers(string statement)
    {
        const string csharp = "void Configure() { stamp = DateTime.Now; }";
        var dsl = $"world game\ndef mark:\n    {statement}\nend\n";
        Assert.Equal(EquivalenceStatus.Fail, MigrationVerifier.CompareMarkHappened(
            MigrationVerifier.ExtractCSharpMarkHappened("handlers.cs", csharp),
            MigrationVerifier.ExtractDslMarkHappened(new DslSource("handlers.seg", dsl))).Status);
    }

    [Fact]
    public void DomainHandlerRegistrationsKeepTheirKindAndTarget()
    {
        var csharp = MigrationVerifier.ExtractCSharpRegistrations("handlers.cs", "void Configure() { addHandlerPickUp(item, e => { }); addHandlerTalkHere(room, e => { }); addHandlerCancelTextInput(ti, e => { }); }");
        var dsl = MigrationVerifier.ExtractDslRegistrations(new DslSource("handlers.seg", "world game\npickup item:\nend\ntalk-here room:\nend\ncancel-text-input ti:\nend\n"));
        Assert.Equal(new[] { "pickup", "talk-here", "cancel-text-input" }, csharp.Select(x => x.Kind));
        Assert.Equal(csharp.Select(x => x.Kind), dsl.Select(x => x.Kind));
        Assert.Equal(csharp.Select(x => x.First), dsl.Select(x => x.First));
    }

    [Fact]
    public void SubmitTextInputRegistrationAndBodyStringsAreExtracted()
    {
        const string csharp = "void Configure() { addHandlerSubmitTextInput(ti, e => { dial(olivia, \"Risposta\"); }); }";
        const string dslText = "world game\nsubmit-text-input ti:\n    olivia: Risposta\nend\n";
        var csharpRegistration = Assert.Single(MigrationVerifier.ExtractCSharpRegistrations("handlers.cs", csharp));
        var dslRegistration = Assert.Single(MigrationVerifier.ExtractDslRegistrations(new DslSource("handlers.seg", dslText)));
        Assert.Equal("submit-text-input", csharpRegistration.Kind);
        Assert.Equal(EquivalenceStatus.Pass, MigrationVerifier.CompareRegistration(csharpRegistration, dslRegistration).Overall);
        var csharpStrings = MigrationVerifier.ExtractCSharpStringsForRegistration("handlers.cs", csharp, "submit-text-input", "ti", null);
        var dslStrings = MigrationVerifier.ExtractDslStrings(new DslSource("handlers.seg", dslText));
        Assert.Equal(new[] { "Risposta" }, csharpStrings);
        Assert.Equal(csharpStrings, dslStrings);
    }

    [Fact]
    public void ParameterizedDialoguesCompareSpeakerTextAndInstaExpression()
    {
        const string csharp = "void Configure() { dial(olivia, \"{1}, ciao {1}!\", insta: nome()); }";
        const string dsl = "world game\nuse olivia here:\n    olivia: \"{1}, ciao {1}!\" insta: nome\nend\n";
        var left = MigrationVerifier.ExtractCSharpDialogues("handlers.cs", csharp);
        var right = MigrationVerifier.ExtractDslDialogues(new DslSource("handlers.seg", dsl));
        Assert.Equal(EquivalenceStatus.Pass, MigrationVerifier.CompareDialogues(left, right).Status);

        var changed = new DslSource("handlers.seg", dsl.Replace("insta: nome", "insta: altro", StringComparison.Ordinal));
        Assert.Equal(EquivalenceStatus.Fail, MigrationVerifier.CompareDialogues(left, MigrationVerifier.ExtractDslDialogues(changed)).Status);
    }

    [Fact]
    public void MansoMigrationKeepsExplanationAndNamedCutsceneTitle()
    {
        const string csharp = "NamedCutSceneId ncsTrovateMansoDeZuniga = new NamedCutSceneId { serId = \"ncsTrovateMansoDeZuniga\", titleUntranslated = \"Trovate Sir Manso De Zuniga\".translatable() }; void Configure() { addHandlerUseFor(armaturaCriptaDracula, puTrovareMansoDeZuniga, exPercheRicordaLeCrociate, e => { using (namedCutScene(ncsTrovateMansoDeZuniga, curRoom, ilSantoGraal, cliffDeserto, dracula)) { dial(olivia, \"Camilla, hai notato che strana questa armatura?\"); } }); }";
        const string dslText = "world game\nuse armaturaCriptaDracula for puTrovareMansoDeZuniga:\n    exp exPercheRicordaLeCrociate\n    named-cutscene ncsTrovateMansoDeZuniga \"Trovate Sir Manso De Zuniga\" curRoom ilSantoGraal cliffDeserto dracula:\n        olivia: Camilla, hai notato che strana questa armatura?\n    end\nend\n";

        var csharpRegistration = Assert.Single(MigrationVerifier.ExtractCSharpRegistrations("worldActionHandlers.cs", csharp));
        var dslRegistration = Assert.Single(MigrationVerifier.ExtractDslRegistrations(new DslSource("Gameplay/ActionHandlers.seg", dslText)));
        Assert.Equal(EquivalenceStatus.Pass, MigrationVerifier.CompareRegistration(csharpRegistration, dslRegistration).Overall);

        var csharpStrings = MigrationVerifier.ExtractCSharpStringsForRegistration("worldActionHandlers.cs", csharp, "use-for", "armaturaCriptaDracula", "puTrovareMansoDeZuniga");
        var dslStrings = MigrationVerifier.ExtractDslStrings(new DslSource("Gameplay/ActionHandlers.seg", dslText));
        Assert.Equal(new[] { "Camilla, hai notato che strana questa armatura?" }, csharpStrings);
        Assert.Equal("Trovate Sir Manso De Zuniga", dslStrings[0]);
        Assert.Equal(csharpStrings, dslStrings.Skip(1));
        Assert.Equal(EquivalenceStatus.Pass, MigrationVerifier.CompareStrings(csharpStrings, dslStrings.Skip(1).ToArray()).Status);
        Assert.Equal(EquivalenceStatus.Pass, MigrationVerifier.CompareNamedCutscenes(
            MigrationVerifier.ExtractCSharpNamedCutscenes("worldObjects.cs", csharp),
            MigrationVerifier.ExtractDslNamedCutscenes(new DslSource("Gameplay/ActionHandlers.seg", dslText))).Status);
    }

    [Fact]
    public void BeforeRoomChangeFingerprintComparesCSharpPrefixWithDsl()
    {
        const string csharp = "class W { void beforeRoomChangeManual(Room from, Room to, WalkPath segment, WalkPath path, BeforeRoomChangeInput e) { if (to == roomCamilla) { dial(camilla, \"Vieni, Olivia!\"); } if (from == roomLibrary && to == roomMuseumOut) { setIfNeverHappened(ref ftDente); } } }";
        const string dsl = "world game\nbefore-room-change:\n    if to == roomCamilla:\n        camilla: Vieni, Olivia!\n    end\n    if from == roomLibrary and to == roomMuseumOut:\n        mark-happened-once ftDente\n    end\nend\n";
        var left = Assert.Single(MigrationVerifier.ExtractCSharpBeforeRoomChange("worldBeforeRoomChange.cs", csharp));
        var right = Assert.Single(MigrationVerifier.ExtractDslBeforeRoomChange(new DslSource("BeforeRoomChange.seg", dsl)));
        Assert.Equal(EquivalenceStatus.Pass, MigrationVerifier.CompareBeforeRoomChange(left, right).Status);

        var changed = new DslSource("BeforeRoomChange.seg", dsl.Replace("ftDente", "ftOther", StringComparison.Ordinal));
        Assert.Equal(EquivalenceStatus.Fail, MigrationVerifier.CompareBeforeRoomChange(left,
            Assert.Single(MigrationVerifier.ExtractDslBeforeRoomChange(changed))).Status);
    }

    [Theory]
    [InlineData("ncsSBAGLIATO \"Trovate Sir Manso De Zuniga\" curRoom ilSantoGraal cliffDeserto dracula")]
    [InlineData("ncsTrovateMansoDeZuniga \"Titolo sbagliato\" curRoom ilSantoGraal cliffDeserto dracula")]
    [InlineData("ncsTrovateMansoDeZuniga \"Trovate Sir Manso De Zuniga\" curRoom ilSantoGraal dracula")]
    [InlineData("ncsTrovateMansoDeZuniga \"Trovate Sir Manso De Zuniga\" curRoom cliffDeserto ilSantoGraal dracula")]
    public void NamedCutsceneFingerprintFailsWhenOnePartChanges(string header)
    {
        const string csharp = "NamedCutSceneId ncsTrovateMansoDeZuniga = new NamedCutSceneId { serId = \"ncsTrovateMansoDeZuniga\", titleUntranslated = \"Trovate Sir Manso De Zuniga\".translatable() }; using (namedCutScene(ncsTrovateMansoDeZuniga, curRoom, ilSantoGraal, cliffDeserto, dracula)) { dial(olivia, \"Testo\"); }";
        var dsl = new DslSource("changed.seg", $"world game\nuse armaturaCriptaDracula for puTrovareMansoDeZuniga:\n    named-cutscene {header}:\n        olivia: Testo\n    end\nend\n");
        Assert.Equal(EquivalenceStatus.Fail, MigrationVerifier.CompareNamedCutscenes(
            MigrationVerifier.ExtractCSharpNamedCutscenes("original.cs", csharp),
            MigrationVerifier.ExtractDslNamedCutscenes(dsl)).Status);
    }

    [Fact]
    public void NamedCutsceneFingerprintFailsWhenTheStatementIsRemoved()
    {
        const string csharp = "NamedCutSceneId ncsTest = new NamedCutSceneId { serId = \"ncsTest\", titleUntranslated = \"Titolo\".translatable() }; using (namedCutScene(ncsTest, curRoom)) { dial(olivia, \"Testo\"); }";
        const string dsl = "world game\nuse armaturaCriptaDracula for puTrovareMansoDeZuniga:\n    olivia: Testo\nend\n";
        Assert.Equal(EquivalenceStatus.Fail, MigrationVerifier.CompareNamedCutscenes(
            MigrationVerifier.ExtractCSharpNamedCutscenes("original.cs", csharp),
            MigrationVerifier.ExtractDslNamedCutscenes(new DslSource("removed.seg", dsl))).Status);
    }

    [Fact]
    public void SemanticRegistrationExtractionRequiresTheMatchingTree()
    {
        var tree = CSharpSyntaxTree.ParseText("void Configure() { addHandlerUseFor(mareSpiaggia, objective, e => { }); }", path: "handlers.cs");
        var refs = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!.Split(System.IO.Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        var compilation = CSharpCompilation.Create("RegistrationModel", new[] { tree }, refs);
        var registrations = MigrationVerifier.ExtractCSharpRegistrations(tree, compilation.GetSemanticModel(tree));
        Assert.Single(registrations);
        Assert.Equal("handlers.cs", registrations[0].SourcePath);
    }

    [Fact]
    public void SemanticRegistrationStringExtractionFollowsTheResolvedHelperOverload()
    {
        const string csharp = "class W { void Configure() { addHandlerUseFor(item, objective, e => { helper(1); }); } void helper(int value) { dial(camilla, \"Int\"); } void helper(string value) { dial(camilla, \"String\"); } }";
        var tree = CSharpSyntaxTree.ParseText(csharp, path: "handlers.cs");
        var refs = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!.Split(System.IO.Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        var compilation = CSharpCompilation.Create("HelperOverloads", new[] { tree }, refs);
        var model = compilation.GetSemanticModel(tree);

        var strings = MigrationVerifier.ExtractCSharpStringsForRegistration(tree, model, "use-for", "item", "objective");

        Assert.Equal(new[] { "Int" }, strings);
    }

    [Fact]
    public void OperatorFingerprintReportsUnaryBinaryAndAssignmentOperators()
    {
        var (model, methods) = CompileMethods("class C { int x; void Foo() {} void A(bool ok, int a, int b) { if (ok) Foo(); x = a + b; x += 1; x++; } void B(bool ok, int a, int b) { if (!ok) Foo(); x = a - b; x -= 1; x--; } }");
        var check = MigrationVerifier.CompareOperations(model.GetOperation(methods.Single(x => x.Identifier.ValueText == "A").Body!), model.GetOperation(methods.Single(x => x.Identifier.ValueText == "B").Body!));
        Assert.Equal(EquivalenceStatus.Fail, check.Status);
        Assert.Contains("unary:", check.Detail, StringComparison.Ordinal);
        Assert.Contains("binary:", check.Detail, StringComparison.Ordinal);
        Assert.Contains("compound-assign:", check.Detail, StringComparison.Ordinal);
        Assert.Contains("increment:", check.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void PureOperatorChangesFailForTheChangedOperator()
    {
        var (model, methods) = CompileMethods("class C { void Foo() {} void Unary(bool ok) { if (ok) Foo(); } void UnaryChanged(bool ok) { if (!ok) Foo(); } void Binary(int a, int b) { if (a == b) Foo(); } void BinaryChanged(int a, int b) { if (a != b) Foo(); } void Arithmetic(int a, int b) { var x = a + b; } void ArithmeticChanged(int a, int b) { var x = a - b; } }");
        var unary = MigrationVerifier.CompareOperations(model.GetOperation(methods.Single(x => x.Identifier.ValueText == "Unary").Body!), model.GetOperation(methods.Single(x => x.Identifier.ValueText == "UnaryChanged").Body!));
        var binary = MigrationVerifier.CompareOperations(model.GetOperation(methods.Single(x => x.Identifier.ValueText == "Binary").Body!), model.GetOperation(methods.Single(x => x.Identifier.ValueText == "BinaryChanged").Body!));
        var arithmetic = MigrationVerifier.CompareOperations(model.GetOperation(methods.Single(x => x.Identifier.ValueText == "Arithmetic").Body!), model.GetOperation(methods.Single(x => x.Identifier.ValueText == "ArithmeticChanged").Body!));
        Assert.Equal(EquivalenceStatus.Fail, unary.Status);
        Assert.Contains("unary:", unary.Detail, StringComparison.Ordinal);
        Assert.Equal(EquivalenceStatus.Fail, binary.Status);
        Assert.Contains("binary:", binary.Detail, StringComparison.Ordinal);
        Assert.Equal(EquivalenceStatus.Fail, arithmetic.Status);
        Assert.Contains("binary:", arithmetic.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void IncrementAndDecrementHaveDifferentFingerprints()
    {
        var (model, methods) = CompileMethods("class C { int x; void A() { x++; } void B() { x--; } void PrefixA() { ++x; } void PrefixB() { --x; } }");

        Assert.Equal(EquivalenceStatus.Fail, MigrationVerifier.CompareOperations(
            model.GetOperation(methods.Single(x => x.Identifier.ValueText == "A").Body!),
            model.GetOperation(methods.Single(x => x.Identifier.ValueText == "B").Body!)).Status);
        Assert.Equal(EquivalenceStatus.Fail, MigrationVerifier.CompareOperations(
            model.GetOperation(methods.Single(x => x.Identifier.ValueText == "PrefixA").Body!),
            model.GetOperation(methods.Single(x => x.Identifier.ValueText == "PrefixB").Body!)).Status);
    }

    [Fact]
    public void FingerprintPreservesBranchAndStatementOrder()
    {
        var (model, methods) = CompileMethods("class C { void A() {} void B() {} void First(bool ok) { if (ok) A(); else B(); } void Second(bool ok) { if (ok) B(); else A(); } void Third() { A(); B(); } void Fourth() { B(); A(); } }");
        Assert.Equal(EquivalenceStatus.Fail, MigrationVerifier.CompareOperations(model.GetOperation(methods.Single(x => x.Identifier.ValueText == "First").Body!), model.GetOperation(methods.Single(x => x.Identifier.ValueText == "Second").Body!)).Status);
        Assert.Equal(EquivalenceStatus.Fail, MigrationVerifier.CompareOperations(model.GetOperation(methods.Single(x => x.Identifier.ValueText == "Third").Body!), model.GetOperation(methods.Single(x => x.Identifier.ValueText == "Fourth").Body!)).Status);
    }

    [Fact]
    public void FingerprintDistinguishesAssignmentTargetsAndValues()
    {
        var (model, methods) = CompileMethods("class C { int x; int y; void First(int a, int b) { x = a; } void Second(int a, int b) { y = a; } void Third(int a, int b) { x = b; } }");
        Assert.Equal(EquivalenceStatus.Fail, MigrationVerifier.CompareOperations(model.GetOperation(methods[0].Body!), model.GetOperation(methods[1].Body!)).Status);
        Assert.Equal(EquivalenceStatus.Fail, MigrationVerifier.CompareOperations(model.GetOperation(methods[0].Body!), model.GetOperation(methods[2].Body!)).Status);
    }

    private static (SemanticModel Model, MethodDeclarationSyntax[] Methods) CompileMethods(string text)
    {
        var refs = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!.Split(System.IO.Path.PathSeparator).Select(path => MetadataReference.CreateFromFile(path)).ToArray();
        var tree = CSharpSyntaxTree.ParseText(text);
        var compilation = CSharpCompilation.Create("FingerprintFixture", new[] { tree }, refs);
        var methods = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>().ToArray();
        return (compilation.GetSemanticModel(tree), methods);
    }

    private static string? FindAncestorWithDirectory(string start, string name)
    {
        for (var current = new DirectoryInfo(start); current is not null; current = current.Parent)
            if (current.Name.Equals(name, StringComparison.OrdinalIgnoreCase)) return current.FullName;
        return null;
    }

    private static string GitShow(string repository, string commit, string path)
    {
        var start = new ProcessStartInfo("git", $"-C \"{repository}\" show {commit}:{path}")
        {
            RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true
        };
        using var process = Process.Start(start)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();
        Assert.True(process.ExitCode == 0, error);
        return output;
    }
}
