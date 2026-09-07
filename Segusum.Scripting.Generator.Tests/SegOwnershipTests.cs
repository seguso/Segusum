using System;
using System.IO;
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

    private sealed class OwnershipFixture : IDisposable
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), "seg-ownership-" + Guid.NewGuid().ToString("N"));
        private readonly bool includeReferencedRuntimeSymbol;
        private readonly bool duplicateOwnedSymbol;

        public string directoryForTest => directory;

        public OwnershipFixture(bool includeReferencedRuntimeSymbol = true, bool duplicateOwnedSymbol = false)
        {
            this.includeReferencedRuntimeSymbol = includeReferencedRuntimeSymbol;
            this.duplicateOwnedSymbol = duplicateOwnedSymbol;
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
            File.WriteAllText(runtimePath, "using Seg; public partial class World : WorldBase { public CycleElemId ownedCycle { get; set; } = new(); public NamedCutSceneId ownedScene = new(); public CycleElemId unused { get; set; } = new(); " + referenced + duplicate + " }");
            File.WriteAllText(legacyPath, "using Seg; public partial class World : WorldBase { public CycleElemId ownedCycle { get; set; } = new(); public NamedCutSceneId ownedScene = new(); public NamedCutSceneId referencedScene = new(); }");
            return SegOwnership.Analyze(segPath, new[] { runtimePath }, legacyPath);
        }

        public void Dispose() { try { Directory.Delete(directory, recursive: true); } catch { } }
    }
}
