using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Segusum.Scripting.Core;

namespace Segusum.Scripting.Tooling;

public enum MigrationUnitStatus { Translated, Partial, Unsupported, DependsOnCSharpHelper }

public sealed record MigrationDiagnostic(MigrationUnitStatus Status, string Path, int Line, string Reason, string? Original = null);
public sealed record MigrationOutput(string Text, IReadOnlyList<MigrationDiagnostic> Diagnostics)
{
    public bool IsFullyTranslated => Diagnostics.All(x => x.Status == MigrationUnitStatus.Translated);
}

/// <summary>Deterministic Roslyn-based first-pass C# to SEG emitter.</summary>
public static class CSharpToSegTranspiler
{
    public static MigrationOutput Transpile(string path, string text, bool emitPartial = false, string? methodName = null)
    {
        var tree = CSharpSyntaxTree.ParseText(text, path: path);
        var diagnostics = new List<MigrationDiagnostic>();
        var sb = new StringBuilder("world migrated\n");
        var root = tree.GetRoot();
        foreach (var trivia in root.DescendantTrivia().Where(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineCommentTrivia) || x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineCommentTrivia)))
            if (trivia.Token.Parent?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().Any() != true)
                sb.AppendLine("// " + trivia.ToString().TrimStart('/').Trim());

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(x => RegistrationKind(x) != null))
        {
            if (methodName != null && !invocation.Ancestors().OfType<MethodDeclarationSyntax>().Any(x => x.Identifier.ValueText == methodName)) continue;
            EmitHandler(invocation, sb, diagnostics, emitPartial);
        }
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(x => methodName == null || x.Identifier.ValueText == methodName))
        {
            if (method.Identifier.ValueText is "afterActionExecutedCSharp")
            {
                sb.AppendLine("after-action-executed:");
                EmitStatements(method.Body?.Statements ?? default, sb, diagnostics, 1, emitPartial);
                sb.AppendLine("end");
            }
            else if (method.Identifier.ValueText is "beforeRoomChangeManual" or "beforeRoomChangeSegusum")
            {
                sb.AppendLine("before-room-change:");
                EmitStatements(method.Body?.Statements ?? default, sb, diagnostics, 1, emitPartial);
                sb.AppendLine("end");
            }
            else if (method.Identifier.ValueText is not "Configure" && method.Body != null && IsHelperCandidate(method))
            {
                sb.Append("def ").Append(method.Identifier.ValueText).AppendLine(":");
                EmitStatements(method.Body.Statements, sb, diagnostics, 1, emitPartial);
                sb.AppendLine("end");
            }
        }
        var generated = sb.ToString();
        var parsed = DslParser.Parse(new Segusum.Scripting.Core.DslSource(path + ".generated.seg", generated));
        foreach (var diagnostic in parsed.Diagnostics)
            diagnostics.Add(new(MigrationUnitStatus.Unsupported, path, diagnostic.Span.Line, "generated SEG is not parsable: " + diagnostic.Message));
        return new MigrationOutput(generated, diagnostics);
    }

    private static bool IsHelperCandidate(MethodDeclarationSyntax method)
        => method.Modifiers.Any(x => x.ValueText is "private" or "protected") && method.ParameterList.Parameters.Count == 0;

    private static void EmitHandler(InvocationExpressionSyntax invocation, StringBuilder sb, List<MigrationDiagnostic> diagnostics, bool partial)
    {
        var args = invocation.ArgumentList.Arguments;
        var kind = RegistrationKind(invocation)!;
        var first = Arg(args, 0); var second = kind is "combine" or "use-for" ? Arg(args, 1) : null;
        var header = kind switch
        {
            "combine" => $"combine {first} with {second}",
            "use-for" => $"use {first} for {second}",
            "use-here" => $"use {first} here",
            "pickup" => $"pickup {first}",
            "talk-here" => $"talk-here {first}",
            "cancel-text-input" => $"cancel-text-input {first}",
            "submit-text-input" => $"submit-text-input {first}",
            "room-changed" => $"room-changed {first}",
            _ => throw new InvalidOperationException(kind)
        };
        sb.AppendLine(header + ":");
        var phrase = kind == "combine" ? args.ElementAtOrDefault(2)?.Expression : null;
        if (phrase is not null && phrase is not AnonymousFunctionExpressionSyntax) sb.Append("  phrase ").AppendLine(LiteralOrExpression(phrase));
        var explanation = args.FirstOrDefault(x => x.NameColon?.Name.Identifier.ValueText == "explanation")?.Expression ?? (kind == "use-for" ? args.ElementAtOrDefault(2)?.Expression : null);
        if (explanation is not null && explanation is not AnonymousFunctionExpressionSyntax) sb.Append("  exp ").AppendLine(LiteralOrExpression(explanation));
        var possible = args.FirstOrDefault(x => x.NameColon?.Name.Identifier.ValueText == "isPossibleNow")?.Expression;
        if (possible is AnonymousFunctionExpressionSyntax lambda) sb.Append("  possible-when ").AppendLine(Expression(lambda.Body));
        var handler = args.Select(x => x.Expression).OfType<AnonymousFunctionExpressionSyntax>().LastOrDefault();
        if (handler?.Body is BlockSyntax block) EmitStatements(block.Statements, sb, diagnostics, 1, partial);
        else if (handler != null) Unsupported(handler, diagnostics, "expression-bodied handler", partial, sb, 1);
        sb.AppendLine("end");
    }

    private static void EmitStatements(IEnumerable<StatementSyntax> statements, StringBuilder sb, List<MigrationDiagnostic> diagnostics, int level, bool partial)
    {
        foreach (var statement in statements)
        {
            EmitComments(statement, sb, level);
            var indent = new string(' ', level * 4);
            switch (statement)
            {
                case IfStatementSyntax x:
                    sb.Append(indent).Append("if ").Append(Expression(x.Condition)).AppendLine(":");
                    EmitStatements(x.Statement is BlockSyntax b ? b.Statements : new[] { x.Statement }, sb, diagnostics, level + 1, partial);
                    var e = x.Else;
                    while (e?.Statement is IfStatementSyntax elif)
                    { sb.Append(indent).Append("elif ").Append(Expression(elif.Condition)).AppendLine(":"); EmitStatements(elif.Statement is BlockSyntax eb ? eb.Statements : new[] { elif.Statement }, sb, diagnostics, level + 1, partial); e = elif.Else; }
                    if (e != null) { sb.Append(indent).AppendLine("else:"); EmitStatements(e.Statement is BlockSyntax eb ? eb.Statements : new[] { e.Statement }, sb, diagnostics, level + 1, partial); }
                    sb.Append(indent).AppendLine("end"); break;
                case ExpressionStatementSyntax x when x.Expression is AssignmentExpressionSyntax a:
                    if (a.Left.ToString().EndsWith("makesNoSenseAtThisTime", StringComparison.Ordinal) && a.Right.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.TrueLiteralExpression)) sb.Append(indent).AppendLine("makes-no-sense");
                    else if (a.Left.ToString().EndsWith("textInputToShow", StringComparison.Ordinal)) sb.Append(indent).Append("text-input ").AppendLine(Expression(a.Right));
                    else sb.Append(indent).Append(Expression(a.Left)).Append(' ').Append(a.OperatorToken.Text).Append(' ').AppendLine(Expression(a.Right)); break;
                case ExpressionStatementSyntax x when x.Expression is InvocationExpressionSyntax i:
                    EmitInvocation(i, sb, diagnostics, level, partial); break;
                case LocalDeclarationStatementSyntax x:
                    foreach (var v in x.Declaration.Variables)
                        if (v.Initializer != null) sb.Append(indent).Append("var ").Append(v.Identifier.ValueText).Append(" = ").Append(Expression(v.Initializer.Value)).AppendLine();
                    break;
                case ReturnStatementSyntax x: sb.Append(indent).Append("ret ").AppendLine(Expression(x.Expression)); break;
                default: Unsupported(statement, diagnostics, "unsupported statement " + statement.Kind(), partial, sb, level); break;
            }
        }
    }

    private static void EmitInvocation(InvocationExpressionSyntax invocation, StringBuilder sb, List<MigrationDiagnostic> diagnostics, int level, bool partial)
    {
        var indent = new string(' ', level * 4); var name = invocation.Expression.ToString();
        if (name is "startCycle" or "addToCycle" || name.EndsWith(".addToCycle", StringComparison.Ordinal)) { Unsupported(invocation, diagnostics, "cycle requires structured cycle emission", partial, sb, level); return; }
        if (name is "dial" or "nar" or "narText" or "narRoom" or "narImg")
        {
            if (name == "dial") sb.Append(indent).Append(Arg(invocation.ArgumentList.Arguments, 0)).Append(": ").AppendLine(LiteralOrExpression(invocation.ArgumentList.Arguments.ElementAtOrDefault(1)?.Expression));
            else sb.Append(indent).Append(name == "narText" ? "nar" : name).Append(' ').AppendLine(LiteralOrExpression(invocation.ArgumentList.Arguments.LastOrDefault()?.Expression));
            return;
        }
        if (name is "finishGame" or "finish") { sb.Append(indent).AppendLine("finish-game"); return; }
        if (name == "doNotAdvanceTime") { sb.Append(indent).AppendLine("do-not-advance-time"); return; }
        if (name == "preventRoomChange") { sb.Append(indent).AppendLine("prevent-room-change"); return; }
        if (name == "setIfNeverHappened") { sb.Append(indent).Append("mark-happened-once ").AppendLine(Arg(invocation.ArgumentList.Arguments, 0)); return; }
        sb.Append(indent).AppendLine(Expression(invocation));
    }

    private static void EmitComments(StatementSyntax statement, StringBuilder sb, int level)
    { foreach (var t in statement.GetLeadingTrivia().Where(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineCommentTrivia) || x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineCommentTrivia))) sb.Append(new string(' ', level * 4)).Append("// ").AppendLine(t.ToString().TrimStart('/').Trim()); }
    private static void Unsupported(SyntaxNode node, List<MigrationDiagnostic> diagnostics, string reason, bool partial, StringBuilder sb, int level)
    { var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1; diagnostics.Add(new(MigrationUnitStatus.Unsupported, node.SyntaxTree?.FilePath ?? "", line, reason, node.ToString())); if (partial) { var i = new string(' ', level * 4); sb.Append(i).AppendLine("// C2SEG-MANUAL-BEGIN").Append(i).Append("// source: ").AppendLine((node.SyntaxTree?.FilePath ?? "") + ":" + line).Append(i).Append("// reason: ").AppendLine(reason); foreach (var l in node.ToString().Split('\n')) sb.Append(i).Append("// ").AppendLine(l); sb.Append(i).AppendLine("// C2SEG-MANUAL-END"); } }
    private static string Expression(SyntaxNode? node) => (node?.ToString() ?? "").Replace("&&", "and", StringComparison.Ordinal).Replace("||", "or", StringComparison.Ordinal).Replace("!=", "<>", StringComparison.Ordinal).Replace("!", "not ", StringComparison.Ordinal).Replace("<>", "!=", StringComparison.Ordinal).Trim();
    private static string LiteralOrExpression(SyntaxNode? node) => node is LiteralExpressionSyntax l && l.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression) ? l.Token.Text : Expression(node);
    private static string Arg(SeparatedSyntaxList<ArgumentSyntax> args, int index) => index >= 0 && index < args.Count ? Expression(args[index].Expression) : "";
    private static string? RegistrationKind(InvocationExpressionSyntax x) => x.Expression.ToString() switch { "addHandlerCombine" => "combine", "addHandlerUseFor" => "use-for", "addHandlerUseHere" => "use-here", "addHandlerPickUp" => "pickup", "addHandlerTalkHere" => "talk-here", "addHandlerCancelTextInput" => "cancel-text-input", "addHandlerSubmitTextInput" => "submit-text-input", "addRoomChangedHandler" => "room-changed", _ => null };
}
