using System;
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
}
