using System;
using System.IO;
using System.Linq;
using Segusum.Scripting.Tooling;

namespace Segusum.Scripting.Generator.Tests;

public sealed class SegOwnershipTests
{
    [Fact]
    public void OwnershipUsesDeclarationsNotReferences()
    {
        using var fixture = new OwnershipFixture();
        var report = fixture.Analyze("world game\nvar cyc = new-cycle\nadd cyc ownedCycle\nend\nroom-changed roomX:\n    named-cutscene ownedScene \"Owned\" curRoom:\n    end\n    ret namedCutSceneIsSeen referencedScene\nend\n");

        Assert.Contains("ownedCycle", report.RemoveFromCSharp);
        Assert.Contains("ownedScene", report.RemoveFromCSharp);
        Assert.Contains("referencedScene", report.KeepInCSharp);
        Assert.Contains("unused", report.CSharpSymbolsNotUsedBySeg);
        Assert.DoesNotContain("referencedScene", report.RemoveFromCSharp);
    }

    [Fact]
    public void MissingReferenceOnlyRuntimeSymbolIsReportedSeparately()
    {
        using var fixture = new OwnershipFixture(includeReferencedRuntimeSymbol: false);
        var report = fixture.Analyze("world game\nvar cyc = new-cycle\nadd cyc ownedCycle\nend\ndef seen:\n    ret namedCutSceneIsSeen referencedScene\nend\n");

        Assert.Contains("referencedScene", report.MissingReferenceOnlyRuntimeSymbols);
        Assert.DoesNotContain("referencedScene", report.RemoveFromCSharp);
    }

    [Fact]
    public void DuplicateRuntimeDeclarationIsAnExplicitAmbiguity()
    {
        using var fixture = new OwnershipFixture(duplicateOwnedSymbol: true);
        var report = fixture.Analyze("world game\nvar cyc = new-cycle\nadd cyc ownedCycle\nend\n");

        Assert.False(report.IsUnambiguous);
        Assert.Contains(report.Ambiguities, x => x.Contains("ownedCycle", StringComparison.Ordinal));
    }

    [Fact]
    public void ApplyRemovalRemovesOwnedDeclarationsButKeepsReferences()
    {
        using var fixture = new OwnershipFixture();
        var runtimePath = Path.Combine(fixture.directoryForTest, "World.cs");
        var report = fixture.Analyze("world game\nvar cyc = new-cycle\nadd cyc ownedCycle\nend\nroom-changed roomX:\n    named-cutscene ownedScene \"Owned\" curRoom:\n    end\nend\n");

        SegOwnership.ApplyRemoval(report);
        var remaining = File.ReadAllText(runtimePath);
        Assert.DoesNotContain("ownedCycle", remaining, StringComparison.Ordinal);
        Assert.DoesNotContain("ownedScene", remaining, StringComparison.Ordinal);
        Assert.Contains("referencedScene", remaining, StringComparison.Ordinal);
    }

    [Fact]
    public void MethodsDeclaredBySegAreOwnedButReferencedHelpersRemain()
    {
        using var fixture = new OwnershipFixture();
        var report = fixture.Analyze("world game\ndef migrated value: int ret bool:\n    ret true\nend\ndef caller:\n    kept\nend\n");

        Assert.Contains(report.MethodsToRemove, x => x.Name == "migrated" && x.ParameterTypes.SequenceEqual(new[] { "int" }));
        Assert.Contains(report.MethodsKept, x => x.Name == "kept");
    }

    [Fact]
    public void MethodOwnershipMatchesOnlyTheCorrectOverload()
    {
        using var fixture = new OwnershipFixture();
        var report = fixture.Analyze("world game\ndef migrated value: int ret bool:\n    ret true\nend\n");

        Assert.Single(report.MethodsToRemove.Where(x => x.Name == "migrated"));
        Assert.Contains(report.MethodsKept, x => x.Name == "migrated" && x.ParameterTypes.SequenceEqual(new[] { "string" }));
    }

    [Fact]
    public void SegDefinedMethodStillReferencedByActiveCSharpIsStillOwnedBySeg()
    {
        using var fixture = new OwnershipFixture(addActiveCaller: true);
        var report = fixture.Analyze("world game\ndef migrated value: int ret bool:\n    ret true\nend\n");

        Assert.Contains(report.MethodsToRemove, x => x.Name == "migrated");
        Assert.Contains(report.MethodsReferencedByActiveCSharp, x => x.Name == "migrated");
    }

    [Fact]
    public void ReferencesAcrossPartialWorldFilesAreFoundSemantically()
    {
        using var fixture = new OwnershipFixture(addActiveCaller: true, callerInSecondPartial: true);
        var report = fixture.Analyze("world game\ndef migrated value: int ret bool:\n    ret true\nend\n");

        Assert.Contains(report.MethodsReferencedByActiveCSharp, x => x.Name == "migrated");
        Assert.Contains(report.MethodsToRemove, x => x.Name == "migrated");
    }

    [Fact]
    public void MethodOwnershipRecognizesStaticInstanceSignatureCollision()
    {
        using var fixture = new OwnershipFixture(migratedMethodStatic: true);
        var report = fixture.Analyze("world game\ndef migrated value: int ret bool:\n    ret true\nend\n");

        Assert.Contains(report.MethodsToRemove, x => x.Name == "migrated" && x.IsStatic);
    }

    private sealed class OwnershipFixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "seg-ownership-" + Guid.NewGuid().ToString("N"));
        private readonly bool includeReferencedRuntimeSymbol;
        private readonly bool duplicateOwnedSymbol;
        private readonly bool addActiveCaller;
        private readonly bool callerInSecondPartial;
        private readonly bool migratedMethodStatic;

        public string directoryForTest => directory;

        public OwnershipFixture(bool includeReferencedRuntimeSymbol = true, bool duplicateOwnedSymbol = false, bool addActiveCaller = false, bool callerInSecondPartial = false, bool migratedMethodStatic = false)
        {
            this.includeReferencedRuntimeSymbol = includeReferencedRuntimeSymbol;
            this.duplicateOwnedSymbol = duplicateOwnedSymbol;
            this.addActiveCaller = addActiveCaller;
            this.callerInSecondPartial = callerInSecondPartial;
            this.migratedMethodStatic = migratedMethodStatic;
            Directory.CreateDirectory(directory);
        }

        public SegOwnershipReport Analyze(string seg)
        {
            var segPath = Path.Combine(directory, "test.seg");
            var runtimePath = Path.Combine(directory, "World.cs");
            var legacyPath = Path.Combine(directory, "legacy.cs");
            File.WriteAllText(segPath, seg);
            var referenced = includeReferencedRuntimeSymbol ? "public NamedCutSceneId referencedScene = new();" : "";
            var duplicate = duplicateOwnedSymbol ? "public CycleElemId ownedCycle = new();" : "";
            var migratedModifier = migratedMethodStatic ? "private static" : "private";
            File.WriteAllText(runtimePath, "using Seg; public partial class World : WorldBase { public CycleElemId ownedCycle { get; set; } = new(); public NamedCutSceneId ownedScene = new(); public CycleElemId unused { get; set; } = new(); " + referenced + duplicate + " " + migratedModifier + " bool migrated(int value) => true; private bool migrated(string value) => true; private void kept() { } " + (addActiveCaller && !callerInSecondPartial ? "private void caller() { migrated(1); }" : "") + " }");
            if (addActiveCaller && callerInSecondPartial)
                File.WriteAllText(Path.Combine(directory, "World.Partial.cs"), "public partial class World { private void caller() { migrated(1); } }");
            File.WriteAllText(legacyPath, "using Seg; public partial class World : WorldBase { public CycleElemId ownedCycle { get; set; } = new(); public NamedCutSceneId ownedScene = new(); public NamedCutSceneId referencedScene = new(); private bool migrated(int value) => true; private bool migrated(string value) => true; private void kept() { } }");
            return SegOwnership.Analyze(segPath, Directory.EnumerateFiles(directory, "*.cs")
                .Where(x => !string.Equals(x, legacyPath, StringComparison.OrdinalIgnoreCase)), legacyPath);
        }

        public void Dispose() { try { Directory.Delete(directory, recursive: true); } catch { } }
    }
}
