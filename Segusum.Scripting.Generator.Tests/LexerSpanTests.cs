using System;
using System.Collections.Generic;
using System.Linq;
using Segusum.Scripting.Core;

namespace Segusum.Scripting.Generator.Tests;

public sealed class LexerSpanTests
{
    [Fact]
    public void LexerSpansMatchTheLegacySourceSpanReference()
    {
        var corpus = new[]
        {
            "first\nsecond\nthird",
            "first\r\nsecond\r\nthird",
            "\tindented",
            "\"string literal\"",
            "mike: Dialogue text",
            "e.canChangeRoom = false\nltVistoZauroz = DateTime.Now\nquanteVolteVistoZauroz++",
            "call target: value other: \"text\"",
            "if olivia.isHere:\r\n\tif e.canChangeRoom:\r\n\t\tret true\r\n\tend\r\nend"
        };

        foreach (var text in corpus)
        {
            var source = new DslSource("span-test.seg", text);
            var tokens = DslLexer.Lex(source, new List<DslDiagnostic>());
            foreach (var token in tokens)
            {
                var expected = SourceSpan.From(source.Path, source.Text, token.Span.Start, token.Span.Length);
                Assert.Equal(expected, token.Span);
            }
        }
    }

    [Fact]
    public void LexerSpansPreserveImportantTokenPositions()
    {
        var text = "\tfoo\r\n\"text\"\r\nmike: Dialogue\r\ne.canChangeRoom = false\r\nltVistoZauroz = DateTime.Now\r\nquanteVolteVistoZauroz++";
        var tokens = DslLexer.Lex(new DslSource("positions.seg", text), new List<DslDiagnostic>());

        var foo = tokens.Single(x => x.Text == "foo");
        Assert.Equal((1, 2), (foo.Span.Line, foo.Span.Column));
        var literal = tokens.Single(x => x.Text == "\"text\"");
        Assert.Equal((2, 1), (literal.Span.Line, literal.Span.Column));
        var speaker = tokens.Single(x => x.Text == "mike");
        Assert.Equal((3, 1), (speaker.Span.Line, speaker.Span.Column));
        var member = tokens.Single(x => x.Text == "canChangeRoom");
        Assert.Equal((4, 3), (member.Span.Line, member.Span.Column));
        var assignment = tokens.Single(x => x.Text == "ltVistoZauroz");
        Assert.Equal((5, 1), (assignment.Span.Line, assignment.Span.Column));
        var increment = tokens.Single(x => x.Text == "quanteVolteVistoZauroz");
        Assert.Equal((6, 1), (increment.Span.Line, increment.Span.Column));
    }
}
