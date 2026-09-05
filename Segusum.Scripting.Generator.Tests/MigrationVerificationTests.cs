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

        var check = MigrationVerifier.CompareOperations(model.GetOperation(methods[0].Body!), model.GetOperation(methods[1].Body!));
        Assert.Equal(EquivalenceStatus.Fail, check.Status);
        Assert.Contains("assign", check.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void RegistrationStringExtractionDoesNotDuplicateNestedDialogueCalls()
    {
        const string csharp = "void Configure() { addRoomChangedHandler(roomA, e => { var c = startCycle(id, x => x.notSeenRecently(1), x => { dial(camilla, \"One\"); }); execNextInCycle(c); }); }";
        var strings = MigrationVerifier.ExtractCSharpStringsForRegistration("rooms.cs", csharp, "room-changed", "roomA", null);
        Assert.Equal(new[] { "One" }, strings);
    }
}
