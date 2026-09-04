using Segusum.Scripting.Core;

namespace Segusum.Translator.Core.Tests;

public sealed class DslParserStructureTests
{
    [Fact]
    public void MultilineExpressionsContinueOnlyThroughOperatorsOrParentheses()
    {
        const string text = "world game\ndef first a: bool ret bool:\n ret a\n     and a\n     and a\nend\ndef second a: bool ret bool:\n ret a and\n     a\nend\ndef third a: bool ret bool:\n ret (a\n     and a)\nend";
        var result = DslParser.Parse(new DslSource("multi.seg", text));
        Assert.Empty(result.Diagnostics);
        var functions = result.Document.Declarations.OfType<FunctionDeclaration>().ToArray();
        Assert.All(functions, f =>
        {
            var expression = ((ReturnStatement)f.Body.Single()).Expression;
            if (expression is ParenthesizedExpression parenthesized) expression = parenthesized.Expression;
            Assert.IsType<BinaryExpression>(expression);
        });
    }

    [Fact]
    public void NestedIfAndAddHaveStructuredNodesAndClauses()
    {
        const string text = "world game\ncombine a with b:\n phrase \"p\"\n exp ex\n possible-when true\n if true:\n  nar \"one\"\n else:\n  nar \"two\"\n end\n add cyc x important\n  when true\n  nar \"cycle\"\n end\nend";
        var result = DslParser.Parse(new DslSource("nested.seg", text));
        Assert.Empty(result.Diagnostics);
        var handler = Assert.IsType<HandlerDeclaration>(result.Document.Declarations.Single());
        Assert.IsType<LiteralExpression>(handler.Phrase);
        Assert.IsType<IdentifierExpression>(handler.Explanation);
        Assert.IsType<LiteralExpression>(handler.Condition);
        var add = Assert.IsType<AddCycleElementStatement>(handler.Body.OfType<AddCycleElementStatement>().Single());
        Assert.NotNull(add.Condition);
        Assert.IsType<IfStatement>(handler.Body.OfType<IfStatement>().Single());
        Assert.DoesNotContain(handler.Body, x => x is DialogueStatement { Character: "__phrase" or "__exp" or "__possible" or "__when" });
    }

    [Fact]
    public void SemicolonSeparatesStatementsAndNextIsNotReturn()
    {
        var result = DslParser.Parse(new DslSource("cycle.seg", "world game\nvar c1 = new-cycle; var c2 = new-cycle\nadd c1 one\nend\nadd c2 two\nend\nnext c1"));
        Assert.Empty(result.Diagnostics);
        Assert.Equal(2, result.Document.Declarations.OfType<CycleDeclaration>().Count());
        Assert.IsType<NextCycleDeclaration>(result.Document.Declarations.Last());
    }

    [Fact]
    public void RepeatModifierIsPreservedAndInvalidValuesAreDiagnosed()
    {
        var valid = DslParser.Parse(new DslSource("repeat.seg", "world game\nadd c id1 once\nend\nadd c id2 forever\nend"));
        Assert.Empty(valid.Diagnostics);
        Assert.Equal(new[] { "once", "forever" }, valid.Document.Declarations.OfType<CycleElementDeclaration>().Select(x => x.Repeat));

        var invalid = DslParser.Parse(new DslSource("repeat.seg", "world game\nadd c id sometimes\nend"));
        Assert.Contains(invalid.Diagnostics, x => x.Id == "SEGDSL102");
    }

    [Fact]
    public void NotBindsBetweenDomainComparisonsAndLogicalOperators()
    {
        const string text = """
world game
def check a: bool, b: bool, c: bool ret bool:
 ret not a == b
end
def check2 a: bool, b: bool ret bool:
 ret a and not b == a
end
def check3 a: bool, b: bool ret bool:
 ret not a and b
end
def check4 a: bool, b: bool, c: bool ret bool:
 ret a or not b and c
end
def check5 a: bool, b: bool ret bool:
 ret not (a and b)
end
""";
        var result = DslParser.Parse(new DslSource("precedence.seg", text));
        Assert.Empty(result.Diagnostics);
        var functions = result.Document.Declarations.OfType<FunctionDeclaration>().ToArray();

        var first = Assert.IsType<UnaryExpression>(((ReturnStatement)functions[0].Body.Single()).Expression);
        Assert.IsType<BinaryExpression>(first.Operand);

        var second = Assert.IsType<BinaryExpression>(((ReturnStatement)functions[1].Body.Single()).Expression);
        var secondNot = Assert.IsType<UnaryExpression>(second.Right);
        Assert.IsType<BinaryExpression>(secondNot.Operand);

        var third = Assert.IsType<BinaryExpression>(((ReturnStatement)functions[2].Body.Single()).Expression);
        Assert.IsType<UnaryExpression>(third.Left);

        var fourth = Assert.IsType<BinaryExpression>(((ReturnStatement)functions[3].Body.Single()).Expression);
        Assert.Equal("or", fourth.Operator);
        Assert.Equal("and", Assert.IsType<BinaryExpression>(fourth.Right).Operator);
        Assert.IsType<UnaryExpression>(Assert.IsType<BinaryExpression>(fourth.Right).Left);

        var fifth = Assert.IsType<UnaryExpression>(((ReturnStatement)functions[4].Body.Single()).Expression);
        Assert.IsType<ParenthesizedExpression>(fifth.Operand);
    }

    [Fact]
    public void DomainPredicatesAreInsideNotOperand()
    {
        const string text = "world game\ndef check ret bool:\n ret not x was-seen-at-least-once\nend\ndef check2 ret bool:\n ret not it not-seen-recently 5\nend";
        var result = DslParser.Parse(new DslSource("domain-precedence.seg", text));
        Assert.Empty(result.Diagnostics);
        foreach (var function in result.Document.Declarations.OfType<FunctionDeclaration>())
        {
            var not = Assert.IsType<UnaryExpression>(((ReturnStatement)function.Body.Single()).Expression);
            Assert.IsType<CallExpression>(not.Operand);
        }
    }

    [Fact]
    public void ChainedComparisonsAreRejected()
    {
        var result = DslParser.Parse(new DslSource("chained.seg", "world game\ndef check a: int, b: int, c: int ret bool:\n ret a < b < c\nend"));
        Assert.Contains(result.Diagnostics, x => x.Id == "SEGDSL105");
    }
}
