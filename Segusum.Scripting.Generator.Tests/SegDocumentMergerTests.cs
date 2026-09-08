using System;
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
}
