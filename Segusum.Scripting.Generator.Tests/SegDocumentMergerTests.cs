using System;
using System.Linq;
using Segusum.Scripting.Tooling;

namespace Segusum.Scripting.Generator.Tests;

public sealed class SegDocumentMergerTests
{
    [Fact]
    public void MergingTheSameGeneratedDeclarationsTwiceIsIdempotent()
    {
        const string baseText = "world game\n\ndef existing ret bool:\n    ret true\nend\n";
        const string addition = "world game\n\n// preserved generated comment\ndef helper value: int ret bool:\n    ret value > 0\nend\n";

        var first = SegDocumentMerger.Merge("base.seg", baseText, new[] { ("helper.seg", addition) });
        var second = SegDocumentMerger.Merge("base.seg", first.Text, new[] { ("helper.seg", addition) });

        Assert.True(first.Succeeded, string.Join(Environment.NewLine, first.Diagnostics));
        Assert.True(second.Succeeded, string.Join(Environment.NewLine, second.Diagnostics));
        Assert.Equal(first.Text, second.Text);
        Assert.Contains("preserved generated comment", second.Text, StringComparison.Ordinal);
    }

    [Fact]
    public void ConflictingGeneratedDeclarationIsRejectedInsteadOfOverwritten()
    {
        const string baseText = "world game\n\ndef helper value: int ret bool:\n    ret true\nend\n";
        const string conflicting = "world game\n\ndef helper value: int ret bool:\n    ret false\nend\n";

        var result = SegDocumentMerger.Merge("base.seg", baseText, new[] { ("helper.seg", conflicting) });

        Assert.False(result.Succeeded);
        Assert.Contains(result.Diagnostics, x => x.Contains("collides with a different existing declaration", StringComparison.Ordinal));
    }

    [Fact]
    public void SameKindFunctionsUseSignatureIdentityInsteadOfClrKindFallback()
    {
        const string generated = "world game\n\ndef first ret bool:\n    ret true\nend\n\ndef second ret bool:\n    ret false\nend\n";
        const string existing = "world game\n\ndef first ret bool:\n    ret true\nend\n";

        var result = SegDocumentMerger.Audit("generated.seg", generated, new[] { ("runtime.seg", existing) });

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal(new[] { "AlreadyPresent", "New" }, result.Entries.Select(x => x.Status));
        Assert.NotEqual(result.Entries[0].Identity, result.Entries[1].Identity);
    }

    [Fact]
    public void NewExtractionAssignsLeadingCommentsToFollowingDeclaration()
    {
        const string generated = "world game\r\n\r\n// new declaration\r\n// historical commented-out code\r\ndef newHelper ret bool:\r\n    ret true\r\nend\r\n\r\n// existing declaration\r\ndef existing ret bool:\r\n    ret false\r\nend\r\n";
        const string existing = "world game\r\n\r\ndef existing ret bool:\r\n    ret false\r\nend\r\n";

        var result = SegDocumentMerger.ExtractNewDeclarations("generated.seg", generated, new[] { ("runtime.seg", existing) });

        Assert.True(result.Audit.Succeeded, string.Join(Environment.NewLine, result.Audit.Diagnostics));
        Assert.Contains("// new declaration", result.NewOnlyText, StringComparison.Ordinal);
        Assert.Contains("// historical commented-out code", result.NewOnlyText, StringComparison.Ordinal);
        Assert.DoesNotContain("// existing declaration", result.NewOnlyText, StringComparison.Ordinal);
        Assert.DoesNotContain("def existing", result.NewOnlyText, StringComparison.Ordinal);
        Assert.Contains("\r\n", result.NewOnlyText, StringComparison.Ordinal);
    }

    [Fact]
    public void RuntimeDuplicateSameIdentityAndBodyIsAlreadyPresent()
    {
        const string text = "world game\n\ndef helper ret bool:\n    ret true\nend\n";
        var result = SegDocumentMerger.Audit("generated.seg", text, new[] { ("a.seg", text), ("b.seg", text) });

        Assert.True(result.Succeeded, string.Join(Environment.NewLine, result.Diagnostics));
        Assert.Equal("AlreadyPresent", Assert.Single(result.Entries).Status);
    }

    [Fact]
    public void DifferentBodiesWithSameIdentityAreConflict()
    {
        const string generated = "world game\n\ndef helper ret bool:\n    ret true\nend\n";
        const string existing = "world game\n\ndef helper ret bool:\n    ret false\nend\n";
        var result = SegDocumentMerger.Audit("generated.seg", generated, new[] { ("runtime.seg", existing) });

        Assert.False(result.Succeeded);
        Assert.Equal("Conflict", Assert.Single(result.Entries).Status);
    }
}
