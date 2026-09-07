using System;
using System.IO;
using Segusum.Scripting.Tooling;

namespace Segusum.Scripting.Generator.Tests;

public sealed class SegOwnershipWorkflowTests
{
    [Fact]
    public void FullHistoryIsTheOnlyArchiveAndApplyIsIdempotent()
    {
        var root = Path.Combine(Path.GetTempPath(), "seg-ownership-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var history = Path.Combine(root, "worldOnRoomChanged.cs");
            var active = Path.Combine(root, "worldRuntime.cs");
            var seg = Path.Combine(root, "OnRoomChanged.seg");
            File.WriteAllText(history, """
                using Seg;
                namespace WebApiLitGir;
                public partial class World : WorldBase
                {
                    private CycleElemId ownedId = new("ownedId");
                    private CycleElemId referencedOnlyId = new("referencedOnlyId");
                    private bool OwnedHelper(bool value) => value;
                    private bool KeptHelper() => true;
                }
                """);
            File.Copy(history, active);
            File.WriteAllText(seg, """
                world game
                room-changed room:
                    var cyc = new-cycle
                    add cyc ownedId
                        when it.notSeenRecently 1
                    end
                    var value = OwnedHelper true
                    KeptHelper
                end
                def OwnedHelper value: bool ret bool:
                    ret value
                end
                """);

            var runtimeFiles = new[] { active };
            var first = SegOwnership.Analyze(seg, runtimeFiles, history);
            Assert.Contains("ownedId", first.RemoveFromCSharp);
            Assert.DoesNotContain("referencedOnlyId", first.RemoveFromCSharp);
            Assert.Single(first.MethodsToRemove, x => x.Name == "OwnedHelper");
            Assert.DoesNotContain(first.MethodsToRemove, x => x.Name == "KeptHelper");
            SegOwnership.ApplyRemoval(first);
            var afterFirst = File.ReadAllText(active);

            var second = SegOwnership.Analyze(seg, runtimeFiles, history);
            Assert.Empty(second.MethodsToRemove);
            SegOwnership.ApplyRemoval(second);
            Assert.Equal(afterFirst, File.ReadAllText(active));

            // A fresh pre-migration runtime reconstructed from the frozen
            // source produces exactly the same residual runtime.
            var reconstructed = Path.Combine(root, "reconstructed.cs");
            File.Copy(history, reconstructed);
            var third = SegOwnership.Analyze(seg, new[] { reconstructed }, history);
            SegOwnership.ApplyRemoval(third);
            Assert.Equal(afterFirst, File.ReadAllText(reconstructed));
            Assert.Equal(File.ReadAllText(history), File.ReadAllText(history));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
