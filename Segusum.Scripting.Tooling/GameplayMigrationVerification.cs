using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Segusum.Scripting.Core;
using DslIfStatement = Segusum.Scripting.Core.IfStatement;

namespace Segusum.Scripting.Tooling;

public enum MigrationFindingKind { MissingBranch, ChangedBranch, MissingSideEffect, AddedSideEffect, MissingString, AddedString, OrderMismatch }
public enum MigrationMatchStatus { ExactMatch, LikelyMatch, MissingCandidate, ChangedCandidate, Unverifiable }

public sealed record MigrationEffect(string Kind, string Value, string SourcePath, int SourceLine)
{
    public string Fingerprint => Kind + ":" + Value;
}

public sealed record MigrationBranch(string Kind, string? Condition, int Nesting, string SourcePath, int SourceLine,
    IReadOnlyList<MigrationEffect> Effects)
{
    public string Fingerprint => $"{Nesting}:{Kind}:{Condition ?? "<else>"}";
}

public sealed record MigrationFinding(MigrationFindingKind Kind, MigrationMatchStatus Status, string Message,
    string? CSharpSourcePath = null, int? CSharpSourceLine = null, string? DslSourcePath = null, int? DslSourceLine = null);

public sealed record MigrationVerificationReport(
    IReadOnlyList<MigrationBranch> CSharpBranches,
    IReadOnlyList<MigrationBranch> DslBranches,
    IReadOnlyList<MigrationEffect> CSharpEffects,
    IReadOnlyList<MigrationEffect> DslEffects,
    IReadOnlyList<string> CSharpStrings,
    IReadOnlyList<string> DslStrings,
    IReadOnlyList<MigrationFinding> Findings)
{
    public bool IsClean => Findings.Count == 0;
    public string ToText() => MigrationVerificationReportFormatter.Format(this);
}

/// <summary>
/// Pragmatic, order-preserving verification for gameplay migrations. It is not
/// a theorem prover: conditions that cannot be normalized conservatively are
/// reported as unverifiable rather than treated as equal.
/// </summary>
public static class GameplayMigrationVerifier
{
    public static MigrationVerificationReport VerifyCSharpToDsl(
        string csharpPath, string csharpText, string methodName, DslSource dslSource,
        string? handlerKind = null)
    {
        var tree = CSharpSyntaxTree.ParseText(csharpText, path: csharpPath);
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(x => x.Identifier.ValueText == methodName);
        if (method?.Body is null)
            return EmptyReport(csharpPath, dslSource.Path, $"C# method '{methodName}' was not found or has no body.");

        var csharpBranches = new List<MigrationBranch>();
        var csharpEffects = new List<MigrationEffect>();
        foreach (var statement in method.Body.Statements)
            CollectCSharpStatement(statement, csharpPath, csharpBranches, csharpEffects, 0);

        var parsed = DslParser.Parse(dslSource);
        if (parsed.Diagnostics.Count != 0)
            return EmptyReport(csharpPath, dslSource.Path, "SEG parse failed: " + string.Join("; ", parsed.Diagnostics.Select(x => x.Message)));

        var declaration = parsed.Document.Declarations.FirstOrDefault(x =>
            handlerKind is null ? x is AfterActionExecutedDeclaration :
            x is HandlerDeclaration handler && handler.Kind == handlerKind);
        var dslBody = declaration switch
        {
            AfterActionExecutedDeclaration after => after.Body,
            HandlerDeclaration handler => handler.Body,
            _ => null
        };
        if (dslBody is null)
            return EmptyReport(csharpPath, dslSource.Path, "Requested SEG handler was not found.");

        var dslBranches = new List<MigrationBranch>();
        var dslEffects = new List<MigrationEffect>();
        foreach (var statement in dslBody)
            CollectDslStatement(statement, dslSource.Path, dslBranches, dslEffects, 0);

        return Compare(csharpBranches, dslBranches, csharpEffects, dslEffects);
    }

    private static MigrationVerificationReport Compare(
        IReadOnlyList<MigrationBranch> csharpBranches, IReadOnlyList<MigrationBranch> dslBranches,
        IReadOnlyList<MigrationEffect> csharpEffects, IReadOnlyList<MigrationEffect> dslEffects)
    {
        var findings = new List<MigrationFinding>();
        for (var i = 0; i < csharpBranches.Count; i++)
        {
            var left = csharpBranches[i];
            var exact = dslBranches.FirstOrDefault(x => x.Nesting == left.Nesting && x.Kind == left.Kind &&
                string.Equals(x.Condition, left.Condition, StringComparison.Ordinal));
            if (exact is null)
            {
                var samePosition = i < dslBranches.Count ? dslBranches[i] : null;
                var status = samePosition is not null && samePosition.Nesting == left.Nesting
                    ? MigrationMatchStatus.ChangedCandidate : MigrationMatchStatus.MissingCandidate;
                var kind = status == MigrationMatchStatus.ChangedCandidate ? MigrationFindingKind.ChangedBranch : MigrationFindingKind.MissingBranch;
                AddFinding(findings, new(kind, status, $"C# branch #{i + 1}: {left.Kind} {left.Condition ?? "<else>"}", left.SourcePath, left.SourceLine));
                continue;
            }

            var exactIndex = IndexOf(dslBranches, exact);
            if (exactIndex != i)
                AddFinding(findings, new(MigrationFindingKind.OrderMismatch, MigrationMatchStatus.LikelyMatch,
                    $"Branch order changed: C# #{i + 1} -> SEG #{exactIndex + 1}", left.SourcePath, left.SourceLine, exact.SourcePath, exact.SourceLine));

            CompareEffects(left, exact, findings);
        }

        foreach (var extra in dslBranches.Where(x => !csharpBranches.Any(c => c.Fingerprint == x.Fingerprint)))
            AddFinding(findings, new(MigrationFindingKind.ChangedBranch, MigrationMatchStatus.LikelyMatch,
                $"SEG-only branch: {extra.Kind} {extra.Condition ?? "<else>"}", null, null, extra.SourcePath, extra.SourceLine));

        CompareEffectSequence(csharpEffects, dslEffects, findings);
        return new(csharpBranches, dslBranches, csharpEffects, dslEffects,
            csharpEffects.Where(IsString).Select(x => StringValue(x)).Distinct(StringComparer.Ordinal).ToArray(),
            dslEffects.Where(IsString).Select(x => StringValue(x)).Distinct(StringComparer.Ordinal).ToArray(), findings);
    }

    private static void CompareEffects(MigrationBranch left, MigrationBranch right, List<MigrationFinding> findings)
    {
        var rightValues = right.Effects.Select(x => x.Fingerprint).ToList();
        foreach (var effect in left.Effects)
            if (!rightValues.Remove(effect.Fingerprint))
                AddFinding(findings, new(MigrationFindingKind.MissingSideEffect, MigrationMatchStatus.MissingCandidate,
                    $"Missing side effect in branch {left.Condition ?? "<else>"}: {effect.Fingerprint}", effect.SourcePath, effect.SourceLine, right.SourcePath, right.SourceLine));
        foreach (var effect in right.Effects.Where(x => !left.Effects.Any(y => y.Fingerprint == x.Fingerprint)))
            AddFinding(findings, new(MigrationFindingKind.AddedSideEffect, MigrationMatchStatus.LikelyMatch,
                $"Added side effect in branch {right.Condition ?? "<else>"}: {effect.Fingerprint}", left.SourcePath, left.SourceLine, effect.SourcePath, effect.SourceLine));
    }

    private static void CompareEffectSequence(IReadOnlyList<MigrationEffect> left, IReadOnlyList<MigrationEffect> right, List<MigrationFinding> findings)
    {
        var rightValues = right.Select(x => x.Fingerprint).ToList();
        foreach (var effect in left)
            if (!rightValues.Remove(effect.Fingerprint))
                AddFinding(findings, new(IsString(effect) ? MigrationFindingKind.MissingString : MigrationFindingKind.MissingSideEffect,
                    MigrationMatchStatus.MissingCandidate, $"Missing {effect.Fingerprint}", effect.SourcePath, effect.SourceLine));
        foreach (var effect in right)
            if (!left.Any(x => x.Fingerprint == effect.Fingerprint))
                AddFinding(findings, new(IsString(effect) ? MigrationFindingKind.AddedString : MigrationFindingKind.AddedSideEffect,
                    MigrationMatchStatus.LikelyMatch, $"Added {effect.Fingerprint}", null, null, effect.SourcePath, effect.SourceLine));
        var commonLeft = left.Where(x => right.Any(y => y.Fingerprint == x.Fingerprint)).Select(x => x.Fingerprint).ToArray();
        var commonRight = right.Where(x => left.Any(y => y.Fingerprint == x.Fingerprint)).Select(x => x.Fingerprint).ToArray();
        if (!commonLeft.SequenceEqual(commonRight, StringComparer.Ordinal))
            AddFinding(findings, new(MigrationFindingKind.OrderMismatch, MigrationMatchStatus.LikelyMatch, "Common side effects or strings occur in a different order."));
    }

    private static void AddFinding(List<MigrationFinding> findings, MigrationFinding finding)
    {
        if (!findings.Any(x => x.Kind == finding.Kind && x.Status == finding.Status &&
            x.Message == finding.Message && x.CSharpSourceLine == finding.CSharpSourceLine &&
            x.DslSourceLine == finding.DslSourceLine))
            findings.Add(finding);
    }

    private static void CollectCSharpStatement(StatementSyntax statement, string path, List<MigrationBranch> branches, List<MigrationEffect> allEffects, int nesting)
    {
        if (statement is IfStatementSyntax conditional)
        {
            CollectCSharpBranch(conditional, path, branches, allEffects, nesting, "if");
            return;
        }
        var effects = CSharpEffects(statement, path);
        allEffects.AddRange(effects);
        foreach (var nested in statement.DescendantNodes().OfType<IfStatementSyntax>())
            CollectCSharpStatement(nested, path, branches, allEffects, nesting + 1);
    }

    private static void CollectCSharpBranch(IfStatementSyntax conditional, string path, List<MigrationBranch> branches, List<MigrationEffect> allEffects, int nesting, string kind)
    {
        var statements = BlockStatements(conditional.Statement);
        var effects = statements.SelectMany(x => CSharpEffectsDeep(x, path)).ToArray();
        branches.Add(new(kind, CanonicalCSharp(conditional.Condition), nesting, path, Line(conditional), effects));
        allEffects.AddRange(effects);
        foreach (var nested in statements) if (nested is IfStatementSyntax nestedIf) CollectCSharpStatement(nestedIf, path, branches, allEffects, nesting + 1);

        if (conditional.Else?.Statement is IfStatementSyntax elseIf)
            CollectCSharpBranch(elseIf, path, branches, allEffects, nesting, "else-if");
        else if (conditional.Else is not null)
        {
            var elseStatements = BlockStatements(conditional.Else.Statement);
            var elseEffects = elseStatements.SelectMany(x => CSharpEffectsDeep(x, path)).ToArray();
            branches.Add(new("else", null, nesting, path, Line(conditional.Else), elseEffects));
            allEffects.AddRange(elseEffects);
            foreach (var nested in elseStatements) if (nested is IfStatementSyntax nestedIf) CollectCSharpStatement(nestedIf, path, branches, allEffects, nesting + 1);
        }
    }

    private static IEnumerable<StatementSyntax> BlockStatements(StatementSyntax statement) => statement is BlockSyntax block ? block.Statements : new[] { statement };
    private static IEnumerable<MigrationEffect> CSharpEffectsDeep(StatementSyntax statement, string path)
    {
        if (statement is IfStatementSyntax conditional)
        {
            foreach (var nested in BlockStatements(conditional.Statement)) foreach (var effect in CSharpEffectsDeep(nested, path)) yield return effect;
            if (conditional.Else is not null) foreach (var nested in BlockStatements(conditional.Else.Statement)) foreach (var effect in CSharpEffectsDeep(nested, path)) yield return effect;
            yield break;
        }
        foreach (var effect in CSharpEffects(statement, path)) yield return effect;
    }

    private static IReadOnlyList<MigrationEffect> CSharpEffects(StatementSyntax statement, string path)
    {
        var effects = new List<MigrationEffect>();
        if (statement is ReturnStatementSyntax ret) effects.Add(new("return", CanonicalCSharp(ret.Expression), path, Line(ret)));
        foreach (var assignment in statement.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            effects.Add(new("assign", CanonicalCSharp(assignment.Left) + assignment.OperatorToken.Text + CanonicalCSharp(assignment.Right), path, Line(assignment)));
        foreach (var increment in statement.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>().Concat<SyntaxNode>(statement.DescendantNodes().OfType<PrefixUnaryExpressionSyntax>()))
            if (increment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PostIncrementExpression) || increment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PreIncrementExpression) || increment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PostDecrementExpression) || increment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PreDecrementExpression))
                effects.Add(new("increment", CanonicalCSharp(increment), path, Line(increment)));
        foreach (var invocation in statement.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var name = InvocationName(invocation);
            var args = invocation.ArgumentList.Arguments.Select(x => CanonicalCSharp(x.Expression)).ToArray();
            var kind = name switch
            {
                "dial" => "dialogue",
                "nar" or "narText" or "narImg" or "narRoom" => "narration",
                "pickUp" => "pickUp",
                "putInRoom" => "putInRoom",
                "changeRoom" => "changeRoom",
                "setIfNeverHappened" or "markHappened" => "mark-happened",
                _ when name.Contains("objective", StringComparison.OrdinalIgnoreCase) => "objective",
                _ when name.Contains("Cycle", StringComparison.OrdinalIgnoreCase) => "cycle",
                _ => "call"
            };
            var value = kind is "dialogue" && args.Length > 1 ? CanonicalCSharp(invocation.ArgumentList.Arguments[0].Expression) + ":" + LiteralText(args[1]) :
                kind is "narration" && args.Length > 0 ? LiteralText(args[0]) :
                CanonicalCSharp(invocation.Expression) + "(" + string.Join(",", args) + ")";
            effects.Add(new(kind, value, path, Line(invocation)));
        }
        return effects;
    }

    private static void CollectDslStatement(DslStatement statement, string path, List<MigrationBranch> branches, List<MigrationEffect> allEffects, int nesting)
    {
        if (statement is DslIfStatement conditional)
        {
            for (var i = 0; i < conditional.Branches.Count; i++)
            {
                var branch = conditional.Branches[i];
                var effects = branch.Body.SelectMany(x => DslEffectsDeep(x, path)).ToArray();
                branches.Add(new(i == 0 ? "if" : "else-if", CanonicalDsl(branch.Condition), nesting, path, branch.Condition.Span.Line, effects));
                allEffects.AddRange(effects);
                foreach (var nested in branch.Body) if (nested is IfStatement nestedIf) CollectDslStatement(nestedIf, path, branches, allEffects, nesting + 1);
            }
            if (conditional.ElseBody is not null)
            {
                var effects = conditional.ElseBody.SelectMany(x => DslEffectsDeep(x, path)).ToArray();
                branches.Add(new("else", null, nesting, path, conditional.Span.Line, effects));
                allEffects.AddRange(effects);
            }
            return;
        }
        var direct = DslEffects(statement, path);
        allEffects.AddRange(direct);
        if (statement is NamedCutsceneStatement cutscene)
            foreach (var nested in cutscene.Body) CollectDslStatement(nested, path, branches, allEffects, nesting + 1);
    }

    private static IEnumerable<MigrationEffect> DslEffectsDeep(DslStatement statement, string path)
    {
        if (statement is DslIfStatement conditional)
        {
            foreach (var branch in conditional.Branches) foreach (var nested in branch.Body) foreach (var effect in DslEffectsDeep(nested, path)) yield return effect;
            if (conditional.ElseBody is not null) foreach (var nested in conditional.ElseBody) foreach (var effect in DslEffectsDeep(nested, path)) yield return effect;
            yield break;
        }
        foreach (var effect in DslEffects(statement, path)) yield return effect;
    }

    private static IReadOnlyList<MigrationEffect> DslEffects(DslStatement statement, string path)
    {
        var line = statement.Span.Line;
        return statement switch
        {
            DialogueStatement dialogue => new[] { new MigrationEffect("dialogue", dialogue.Character + ":" + LiteralDsl(dialogue.Text), path, line) },
            NarStatement nar => new[] { new MigrationEffect("narration", LiteralDsl(nar.Text), path, line) },
            NarRoomStatement narRoom => new[] { new MigrationEffect("narration", LiteralDsl(narRoom.Text), path, line) },
            NarImgStatement narImg => new[] { new MigrationEffect("narration", LiteralDsl(narImg.Text), path, line) },
            AssignmentStatement assignment => new[] { new MigrationEffect("assign", (assignment.Receiver is null ? assignment.Name : CanonicalDsl(assignment.Receiver) + "." + assignment.MemberName) + assignment.Operator + CanonicalDsl(assignment.Value), path, line) },
            IncrementStatement increment => new[] { new MigrationEffect("increment", increment.Name + "++", path, line) },
            CallStatement call => DslCallEffect(call.Expression, path, line),
            ReturnStatement ret => new[] { new MigrationEffect("return", CanonicalDsl(ret.Expression), path, line) },
            _ => Array.Empty<MigrationEffect>()
        };
    }

    private static IReadOnlyList<MigrationEffect> DslCallEffect(DslExpression expression, string path, int line)
    {
        if (expression is not CallExpression call) return new[] { new MigrationEffect("call", CanonicalDsl(expression), path, line) };
        var kind = call.Name switch
        {
            "nar" or "narText" or "narImg" or "narRoom" => "narration",
            "pickUp" => "pickUp", "putInRoom" => "putInRoom", "changeRoom" => "changeRoom", _ => "call"
        };
        var value = kind == "narration" && call.Arguments.Count > 0 ? LiteralDsl(call.Arguments[0].Expression) : CanonicalDsl(expression);
        return new[] { new MigrationEffect(kind, value, path, line) };
    }

    private static string CallKind(DslExpression expression) => (expression as CallExpression)?.Name switch
    {
        "pickUp" => "pickUp", "putInRoom" => "putInRoom", "changeRoom" => "changeRoom", _ => "call"
    };

    private static MigrationVerificationReport EmptyReport(string csharpPath, string dslPath, string message) =>
        new(Array.Empty<MigrationBranch>(), Array.Empty<MigrationBranch>(), Array.Empty<MigrationEffect>(), Array.Empty<MigrationEffect>(),
            Array.Empty<string>(), Array.Empty<string>(), new[] { new MigrationFinding(MigrationFindingKind.ChangedBranch, MigrationMatchStatus.Unverifiable, message, csharpPath, null, dslPath, null) });

    private static string CanonicalCSharp(SyntaxNode? node) => CanonicalText(node?.ToString() ?? "");
    private static string CanonicalDsl(DslExpression expression) => CanonicalText(expression switch
    {
        IdentifierExpression id => id.Name,
        LiteralExpression literal => literal.Value,
        UnaryExpression unary => unary.Operator + CanonicalDsl(unary.Operand),
        BinaryExpression binary => CanonicalDsl(binary.Left) + binary.Operator + CanonicalDsl(binary.Right),
        ParenthesizedExpression parenthesized => "(" + CanonicalDsl(parenthesized.Expression) + ")",
        MemberAccessExpression member => CanonicalDsl(member.Receiver) + "." + member.MemberName,
        CallExpression call => (call.Receiver is null ? call.Name : CanonicalDsl(call.Receiver) + "." + call.Name) + "(" + string.Join(",", call.Arguments.Select(x => CanonicalDsl(x.Expression))) + ")",
        _ => expression.ToString() ?? ""
    });
    private static string CanonicalText(string text)
    {
        text = Regex.Replace(text, @"\s+", "");
        text = text.Replace("&&", "and", StringComparison.Ordinal).Replace("||", "or", StringComparison.Ordinal);
        text = text.Replace("!=", "<>PLACEHOLDER", StringComparison.Ordinal).Replace("!", "not", StringComparison.Ordinal).Replace("<>PLACEHOLDER", "!=", StringComparison.Ordinal);
        while (text.Length > 1 && text[0] == '(' && text[^1] == ')' && BalancedOuter(text)) text = text[1..^1];
        return text;
    }
    private static bool BalancedOuter(string text)
    {
        var depth = 0;
        for (var i = 0; i < text.Length; i++) { if (text[i] == '(') depth++; else if (text[i] == ')' && --depth == 0 && i != text.Length - 1) return false; }
        return depth == 0;
    }
    private static string LiteralText(string text) => text.Length >= 2 && text[0] == '"' && text[^1] == '"' ? text[1..^1] : text;
    private static string LiteralDsl(DslExpression expression) => expression is LiteralExpression literal ? literal.Value.Trim('"') : CanonicalDsl(expression);
    private static string StringValue(MigrationEffect effect) => effect.Kind is "dialogue" or "narration" ? effect.Value[(effect.Value.IndexOf(':') + 1)..] : effect.Value;
    private static bool IsString(MigrationEffect effect) => effect.Kind is "dialogue" or "narration";
    private static int Line(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
    private static int IndexOf<T>(IReadOnlyList<T> values, T value) => values is IList<T> list ? list.IndexOf(value) : Enumerable.Range(0, values.Count).First(i => EqualityComparer<T>.Default.Equals(values[i], value));
    private static string InvocationName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        _ => ""
    };
}

public static class MigrationVerificationReportFormatter
{
    public static string Format(MigrationVerificationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"C# branches: {report.CSharpBranches.Count}");
        builder.AppendLine($"SEG branches: {report.DslBranches.Count}");
        builder.AppendLine($"C# effects: {report.CSharpEffects.Count}");
        builder.AppendLine($"SEG effects: {report.DslEffects.Count}");
        foreach (var finding in report.Findings)
            builder.AppendLine($"{finding.Status}: {finding.Kind}: {finding.Message}");
        if (report.Findings.Count == 0) builder.AppendLine("No differences found.");
        return builder.ToString();
    }
}
