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

public enum MigrationFindingKind { MissingBranch, ChangedBranch, MissingSideEffect, AddedSideEffect, MissingString, AddedString, OrderMismatch, Unverifiable }
public enum MigrationMatchStatus { ExactMatch, LikelyMatch, MissingCandidate, ChangedCandidate, Unverifiable }

public sealed record MigrationEffect(string Kind, string Value, string SourcePath, int SourceLine)
{
    public string Fingerprint => Kind + ":" + Value;
}

public sealed record MigrationBranch(string Kind, string? Condition, int Nesting, string SourcePath, int SourceLine,
    IReadOnlyList<MigrationEffect> DirectEffects, IReadOnlyList<MigrationBranch> Children)
{
    public IReadOnlyList<MigrationEffect> Effects => DirectEffects;
    public string Fingerprint => $"{Nesting}:{Kind}:{Condition ?? "<else>"}";
}

public sealed record MigrationVerificationOptions(Func<MigrationBranch, bool>? IsMigrated = null, Func<MigrationBranch, bool>? IsDslMigrated = null);

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
    public IReadOnlyList<MigrationBranch> MigratedCSharpBranches { get; init; } = Array.Empty<MigrationBranch>();
    public IReadOnlyList<MigrationBranch> RemainingCSharpBranches { get; init; } = Array.Empty<MigrationBranch>();
    public int MissingCount => Findings.Count(x => x.Kind is MigrationFindingKind.MissingBranch or MigrationFindingKind.MissingSideEffect or MigrationFindingKind.MissingString);
    public int ChangedCount => Findings.Count(x => x.Kind == MigrationFindingKind.ChangedBranch);
    public int OrderMismatchCount => Findings.Count(x => x.Kind == MigrationFindingKind.OrderMismatch);
    public int DirectSideEffectMismatchCount => Findings.Count(x => x.Kind is MigrationFindingKind.MissingSideEffect or MigrationFindingKind.AddedSideEffect);
    public int StringMismatchCount => Findings.Count(x => x.Kind is MigrationFindingKind.MissingString or MigrationFindingKind.AddedString);
    public int UnverifiableCount => Findings.Count(x => x.Status == MigrationMatchStatus.Unverifiable);
    public int CorrespondingDslBranchCount => MigratedCSharpBranches.Count - Findings.Count(x => x.Kind is MigrationFindingKind.MissingBranch or MigrationFindingKind.ChangedBranch);
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
        string? handlerKind = null, MigrationVerificationOptions? options = null)
    {
        var tree = CSharpSyntaxTree.ParseText(csharpText, path: csharpPath);
        var method = tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .FirstOrDefault(x => x.Identifier.ValueText == methodName);
        if (method?.Body is null)
            return EmptyReport(csharpPath, dslSource.Path, $"C# method '{methodName}' was not found or has no body.");

        var csharpBranches = new List<MigrationBranch>();
        var csharpEffects = new List<MigrationEffect>();
        foreach (var statement in method.Body.Statements)
        {
            if (statement is IfStatementSyntax conditional) csharpBranches.AddRange(CollectCSharpChain(conditional, csharpPath, 0, csharpEffects));
            else csharpEffects.AddRange(CSharpEffects(statement, csharpPath));
        }

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
        {
            if (statement is DslIfStatement conditional) dslBranches.AddRange(CollectDslChain(conditional, dslSource.Path, 0, dslEffects));
            else dslEffects.AddRange(DslEffects(statement, dslSource.Path));
        }

        var effectiveOptions = options ?? new();
        var selectedMaxLine = effectiveOptions.IsMigrated is null ? int.MaxValue :
            csharpBranches.SelectMany(x => Flatten(new[] { x })).Where(effectiveOptions.IsMigrated).Select(x => x.SourceLine).DefaultIfEmpty(0).Max();
        var report = Compare(csharpBranches, dslBranches, csharpEffects, dslEffects, effectiveOptions,
            UnsupportedCSharp(method, selectedMaxLine).Concat(UnsupportedDsl(dslBody)).ToArray());
        var allBranches = Flatten(csharpBranches);
        return report with
        {
            MigratedCSharpBranches = effectiveOptions.IsMigrated is null ? allBranches : allBranches.Where(effectiveOptions.IsMigrated).ToArray(),
            RemainingCSharpBranches = effectiveOptions.IsMigrated is null ? Array.Empty<MigrationBranch>() : allBranches.Where(x => !effectiveOptions.IsMigrated(x)).ToArray()
        };
    }

    private static MigrationVerificationReport Compare(
        IReadOnlyList<MigrationBranch> csharpBranches, IReadOnlyList<MigrationBranch> dslBranches,
        IReadOnlyList<MigrationEffect> csharpEffects, IReadOnlyList<MigrationEffect> dslEffects,
        MigrationVerificationOptions options, IReadOnlyList<MigrationFinding> unsupported)
    {
        var findings = new List<MigrationFinding>();
        CompareSiblingLists(csharpBranches, dslBranches, findings, options.IsMigrated, options.IsDslMigrated, "root");
        foreach (var finding in unsupported) AddFinding(findings, finding);

        if (options.IsMigrated is null)
            CompareEffectSequence(csharpEffects, dslEffects, findings);
        return new(Flatten(csharpBranches), Flatten(dslBranches), csharpEffects, dslEffects,
            csharpEffects.Where(IsString).Select(x => StringValue(x)).Distinct(StringComparer.Ordinal).ToArray(),
            dslEffects.Where(IsString).Select(x => StringValue(x)).Distinct(StringComparer.Ordinal).ToArray(), findings);
    }

    private static void CompareSiblingLists(IReadOnlyList<MigrationBranch> left, IReadOnlyList<MigrationBranch> right,
        List<MigrationFinding> findings, Func<MigrationBranch, bool>? isMigrated, Func<MigrationBranch, bool>? isDslMigrated, string location)
    {
        var expected = left.Where(x => isMigrated is null || isMigrated(x)).ToArray();
        var actual = right.Where(x => isDslMigrated is null || isDslMigrated(x)).ToArray();
        for (var i = 0; i < expected.Length; i++)
        {
            var source = expected[i];
            var target = i < actual.Length ? actual[i] : null;
            if (target is null)
            {
                AddFinding(findings, new(MigrationFindingKind.MissingBranch, MigrationMatchStatus.MissingCandidate,
                    $"Missing branch at {location} #{i + 1}: {source.Kind} {source.Condition ?? "<else>"}", source.SourcePath, source.SourceLine));
                continue;
            }
            if (source.Kind != target.Kind || !string.Equals(source.Condition, target.Condition, StringComparison.Ordinal))
            {
                var later = actual.Skip(i + 1).FirstOrDefault(x => x.Kind == source.Kind && x.Condition == source.Condition);
                AddFinding(findings, new(later is null ? MigrationFindingKind.ChangedBranch : MigrationFindingKind.OrderMismatch,
                    later is null ? MigrationMatchStatus.ChangedCandidate : MigrationMatchStatus.LikelyMatch,
                    later is null ? $"Changed branch at {location} #{i + 1}: C# {source.Kind} {source.Condition ?? "<else>"}; SEG {target.Kind} {target.Condition ?? "<else>"}" :
                    $"Branch order changed at {location}: C# #{i + 1} -> SEG #{IndexOf(actual, later) + 1}", source.SourcePath, source.SourceLine, target.SourcePath, target.SourceLine));
                if (later is null) continue;
            }
            CompareEffects(source, target, findings);
            CompareSiblingLists(source.Children, target.Children, findings, isMigrated, isDslMigrated, location + "/" + (i + 1));
        }
        if (actual.Length > expected.Length)
            foreach (var extra in actual.Skip(expected.Length))
                AddFinding(findings, new(MigrationFindingKind.ChangedBranch, MigrationMatchStatus.LikelyMatch,
                    $"SEG-only branch at {location}: {extra.Kind} {extra.Condition ?? "<else>"}", null, null, extra.SourcePath, extra.SourceLine));
    }

    private static IReadOnlyList<MigrationBranch> Flatten(IReadOnlyList<MigrationBranch> roots) =>
        roots.SelectMany(x => new[] { x }.Concat(Flatten(x.Children))).ToArray();

    private static void CompareEffects(MigrationBranch left, MigrationBranch right, List<MigrationFinding> findings)
    {
        if (left.DirectEffects.Count == right.DirectEffects.Count &&
            left.DirectEffects.Select(x => x.Fingerprint).OrderBy(x => x).SequenceEqual(right.DirectEffects.Select(x => x.Fingerprint).OrderBy(x => x)) &&
            !left.DirectEffects.Select(x => x.Fingerprint).SequenceEqual(right.DirectEffects.Select(x => x.Fingerprint)))
        {
            AddFinding(findings, new(MigrationFindingKind.OrderMismatch, MigrationMatchStatus.LikelyMatch,
                $"Direct effect order mismatch in branch {left.Condition ?? "<else>"}: sequences contain the same effects in a different order.",
                left.SourcePath, left.SourceLine, right.SourcePath, right.SourceLine));
            return;
        }
        var common = Math.Min(left.DirectEffects.Count, right.DirectEffects.Count);
        for (var i = 0; i < common; i++)
            if (left.DirectEffects[i].Fingerprint != right.DirectEffects[i].Fingerprint)
                AddFinding(findings, EffectMismatch(left.DirectEffects[i], right.DirectEffects[i], i + 1));
        for (var i = common; i < left.DirectEffects.Count; i++)
            AddFinding(findings, EffectMissing(left.DirectEffects[i], i + 1));
        for (var i = common; i < right.DirectEffects.Count; i++)
            AddFinding(findings, EffectAdded(right.DirectEffects[i], i + 1));
    }

    private static MigrationFinding EffectMismatch(MigrationEffect left, MigrationEffect right, int index) =>
        new(IsString(left) || IsString(right) ? MigrationFindingKind.MissingString : MigrationFindingKind.MissingSideEffect,
            MigrationMatchStatus.ChangedCandidate, $"effect #{index} mismatch: C#: {left.Fingerprint}; SEG: {right.Fingerprint}", left.SourcePath, left.SourceLine, right.SourcePath, right.SourceLine);
    private static MigrationFinding EffectMissing(MigrationEffect effect, int index) =>
        new(IsString(effect) ? MigrationFindingKind.MissingString : MigrationFindingKind.MissingSideEffect,
            MigrationMatchStatus.MissingCandidate, $"effect #{index} missing in SEG: {effect.Fingerprint}", effect.SourcePath, effect.SourceLine);
    private static MigrationFinding EffectAdded(MigrationEffect effect, int index) =>
        new(IsString(effect) ? MigrationFindingKind.AddedString : MigrationFindingKind.AddedSideEffect,
            MigrationMatchStatus.LikelyMatch, $"effect #{index} added in SEG: {effect.Fingerprint}", null, null, effect.SourcePath, effect.SourceLine);

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

    private static IReadOnlyList<MigrationBranch> CollectCSharpChain(IfStatementSyntax conditional, string path, int nesting, List<MigrationEffect> allEffects, string kind = "if")
    {
        var branch = CollectCSharpBranch(conditional, path, nesting, kind, allEffects);
        if (conditional.Else?.Statement is IfStatementSyntax elseIf)
            return new[] { branch }.Concat(CollectCSharpChain(elseIf, path, nesting, allEffects, "else-if")).ToArray();
        if (conditional.Else is not null)
            return new[] { branch, CollectCSharpElse(conditional.Else, path, nesting, allEffects) };
        return new[] { branch };
    }

    private static MigrationBranch CollectCSharpElse(ElseClauseSyntax clause, string path, int nesting, List<MigrationEffect> allEffects)
    {
        var statements = BlockStatements(clause.Statement);
        var direct = new List<MigrationEffect>();
        var children = new List<MigrationBranch>();
        foreach (var statement in TransparentStatements(statements))
            if (statement is IfStatementSyntax nested) children.AddRange(CollectCSharpChain(nested, path, nesting + 1, allEffects));
            else direct.AddRange(CSharpEffects(statement, path));
        allEffects.AddRange(direct);
        return new("else", null, nesting, path, Line(clause), direct, children);
    }

    private static MigrationBranch CollectCSharpBranch(IfStatementSyntax conditional, string path, int nesting, string kind, List<MigrationEffect> allEffects)
    {
        var direct = new List<MigrationEffect>();
        var children = new List<MigrationBranch>();
        foreach (var statement in TransparentStatements(BlockStatements(conditional.Statement)))
            if (statement is IfStatementSyntax nested) children.AddRange(CollectCSharpChain(nested, path, nesting + 1, allEffects));
            else direct.AddRange(CSharpEffects(statement, path));
        allEffects.AddRange(direct);
        return new(kind, CanonicalCSharp(conditional.Condition), nesting, path, Line(conditional), direct, children);
    }

    private static IEnumerable<StatementSyntax> BlockStatements(StatementSyntax statement) => statement is BlockSyntax block ? block.Statements : new[] { statement };
    private static IEnumerable<StatementSyntax> TransparentStatements(IEnumerable<StatementSyntax> statements)
    {
        foreach (var statement in statements)
            if (statement is BlockSyntax block) foreach (var nested in TransparentStatements(block.Statements)) yield return nested;
            else yield return statement;
    }
    private static IReadOnlyList<MigrationEffect> CSharpEffects(StatementSyntax statement, string path)
    {
        var effects = new List<MigrationEffect>();
        if (statement is ReturnStatementSyntax ret) effects.Add(new("return", CanonicalCSharp(ret.Expression), path, Line(ret)));
        foreach (var variable in statement.DescendantNodes().OfType<VariableDeclaratorSyntax>().Where(x => x.Initializer is not null))
            effects.Add(new("assign", variable.Identifier.ValueText + "=" + CanonicalCSharp(variable.Initializer!.Value), path, Line(variable)));
        foreach (var assignment in statement.DescendantNodes().OfType<AssignmentExpressionSyntax>())
            effects.Add(new("assign", CanonicalCSharp(assignment.Left) + assignment.OperatorToken.Text + CanonicalCSharp(assignment.Right), path, Line(assignment)));
        foreach (var increment in statement.DescendantNodes().OfType<PostfixUnaryExpressionSyntax>().Concat<SyntaxNode>(statement.DescendantNodes().OfType<PrefixUnaryExpressionSyntax>()))
            if (increment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PostIncrementExpression) || increment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PreIncrementExpression) || increment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PostDecrementExpression) || increment.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PreDecrementExpression))
                effects.Add(new("increment", CanonicalCSharp(increment), path, Line(increment)));
        foreach (var invocation in statement.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var name = InvocationName(invocation);
            if (name == "getCurTime" && (invocation.Ancestors().OfType<AssignmentExpressionSyntax>().Any() ||
                invocation.Ancestors().OfType<VariableDeclaratorSyntax>().Any())) continue;
            var args = invocation.ArgumentList.Arguments.Select(x =>
                (x.NameColon is null ? "" : x.NameColon.Name.Identifier.ValueText + "=") + CanonicalCSharp(x.Expression)).ToArray();
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
            var value = kind is "dialogue" && args.Length > 1 ? CanonicalCSharp(invocation.ArgumentList.Arguments[0].Expression) + ":" + LiteralText(invocation.ArgumentList.Arguments[1].Expression.ToString()) :
                kind is "narration" && args.Length > 0 ? LiteralText(invocation.ArgumentList.Arguments[0].Expression.ToString()) :
                CanonicalText(CanonicalCSharp(invocation.Expression) + "(" + string.Join(",", args) + ")");
            effects.Add(new(kind, value, path, Line(invocation)));
        }
        return effects;
    }

    private static IReadOnlyList<MigrationBranch> CollectDslChain(DslIfStatement conditional, string path, int nesting, List<MigrationEffect> allEffects)
    {
        var result = new List<MigrationBranch>();
        for (var i = 0; i < conditional.Branches.Count; i++)
            result.Add(CollectDslBranch(conditional.Branches[i], path, nesting, i == 0 ? "if" : "else-if", allEffects));
        if (conditional.ElseBody is not null)
            result.Add(CollectDslElse(conditional, path, nesting, allEffects));
        return result;
    }

    private static MigrationBranch CollectDslBranch((DslExpression Condition, IReadOnlyList<DslStatement> Body) branch,
        string path, int nesting, string kind, List<MigrationEffect> allEffects)
    {
        var direct = new List<MigrationEffect>();
        var children = new List<MigrationBranch>();
        foreach (var statement in branch.Body)
            if (statement is DslIfStatement nested) children.AddRange(CollectDslChain(nested, path, nesting + 1, allEffects));
            else direct.AddRange(DslEffects(statement, path));
        allEffects.AddRange(direct);
        return new(kind, CanonicalDsl(branch.Condition), nesting, path, branch.Condition.Span.Line, direct, children);
    }

    private static MigrationBranch CollectDslElse(DslIfStatement conditional, string path, int nesting, List<MigrationEffect> allEffects)
    {
        var direct = new List<MigrationEffect>();
        var children = new List<MigrationBranch>();
        foreach (var statement in conditional.ElseBody!)
            if (statement is DslIfStatement nested) children.AddRange(CollectDslChain(nested, path, nesting + 1, allEffects));
            else direct.AddRange(DslEffects(statement, path));
        allEffects.AddRange(direct);
        return new("else", null, nesting, path, conditional.Span.Line, direct, children);
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
            AssignmentStatement assignment => DslAssignmentEffects(assignment, path, line),
            VariableDeclaration variable => new[] { new MigrationEffect("assign", variable.Name + "=" + CanonicalDsl(variable.Initializer), path, line) },
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

    private static IReadOnlyList<MigrationEffect> DslAssignmentEffects(AssignmentStatement assignment, string path, int line)
    {
        var receiver = assignment.Receiver is null ? assignment.Name : CanonicalDsl(assignment.Receiver) + "." + assignment.MemberName;
        var effects = new List<MigrationEffect> { new("assign", receiver + assignment.Operator + CanonicalDsl(assignment.Value), path, line) };
        if (assignment.Value is CallExpression call) effects.AddRange(DslCallEffect(call, path, line));
        return effects;
    }

    private static string CallKind(DslExpression expression) => (expression as CallExpression)?.Name switch
    {
        "pickUp" => "pickUp", "putInRoom" => "putInRoom", "changeRoom" => "changeRoom", _ => "call"
    };

    private static IReadOnlyList<MigrationFinding> UnsupportedCSharp(MethodDeclarationSyntax method, int maxLine)
    {
        var nodes = method.DescendantNodes().Where(x => Line(x) <= maxLine && x is
            ForStatementSyntax or ForEachStatementSyntax or WhileStatementSyntax or DoStatementSyntax or
            SwitchStatementSyntax or TryStatementSyntax or UsingStatementSyntax or LockStatementSyntax or
            BreakStatementSyntax or ContinueStatementSyntax or LocalFunctionStatementSyntax or
            ConditionalExpressionSyntax);
        return nodes.Select(x => new MigrationFinding(MigrationFindingKind.Unverifiable, MigrationMatchStatus.Unverifiable,
            $"Unsupported C# construct: {x.GetType().Name} at line {Line(x)}", method.SyntaxTree.FilePath, Line(x))).ToArray();
    }

    private static IReadOnlyList<MigrationFinding> UnsupportedDsl(IReadOnlyList<DslStatement> statements)
    {
        var supported = new[] { typeof(VariableDeclaration), typeof(AssignmentStatement), typeof(IncrementStatement),
            typeof(CallStatement), typeof(ReturnStatement), typeof(IfStatement), typeof(NarStatement),
            typeof(NarRoomStatement), typeof(NarImgStatement), typeof(DialogueStatement), typeof(NamedCutsceneStatement) };
        return FlattenDslStatements(statements).Where(x => !supported.Contains(x.GetType())).Select(x =>
            new MigrationFinding(MigrationFindingKind.Unverifiable, MigrationMatchStatus.Unverifiable,
                $"Unsupported SEG construct: {x.GetType().Name}", "", x.Span.Line)).ToArray();
    }

    private static IEnumerable<DslStatement> FlattenDslStatements(IEnumerable<DslStatement> statements)
    {
        foreach (var statement in statements)
        {
            yield return statement;
            if (statement is IfStatement conditional)
            {
                foreach (var branch in conditional.Branches) foreach (var nested in FlattenDslStatements(branch.Body)) yield return nested;
                if (conditional.ElseBody is not null) foreach (var nested in FlattenDslStatements(conditional.ElseBody)) yield return nested;
            }
            else if (statement is NamedCutsceneStatement cutscene)
                foreach (var nested in FlattenDslStatements(cutscene.Body)) yield return nested;
        }
    }

    private static MigrationVerificationReport EmptyReport(string csharpPath, string dslPath, string message) =>
        new(Array.Empty<MigrationBranch>(), Array.Empty<MigrationBranch>(), Array.Empty<MigrationEffect>(), Array.Empty<MigrationEffect>(),
            Array.Empty<string>(), Array.Empty<string>(), new[] { new MigrationFinding(MigrationFindingKind.ChangedBranch, MigrationMatchStatus.Unverifiable, message, csharpPath, null, dslPath, null) });

    private static string CanonicalCSharp(SyntaxNode? node) => CanonicalText(node is null ? "" : string.Concat(node.DescendantTokens().Select(x => x.Text)));
    private static string CanonicalDsl(DslExpression expression) => CanonicalText(expression switch
    {
        IdentifierExpression id => id.Name,
        LiteralExpression literal => literal.Value,
        UnaryExpression unary => unary.Operator + CanonicalDsl(unary.Operand),
        BinaryExpression binary => CanonicalDsl(binary.Left) + binary.Operator + CanonicalDsl(binary.Right),
        ParenthesizedExpression parenthesized => "(" + CanonicalDsl(parenthesized.Expression) + ")",
        MemberAccessExpression member => CanonicalDsl(member.Receiver) + "." + member.MemberName,
        CallExpression call => (call.Receiver is null ? call.Name : CanonicalDsl(call.Receiver) + "." + call.Name) + "(" + string.Join(",", call.Arguments.Select(x => (x.Name is null ? "" : x.Name + "=") + CanonicalDsl(x.Expression))) + ")",
        _ => expression.ToString() ?? ""
    });
    private static string CanonicalText(string text)
    {
        text = Regex.Replace(text, @"\s+", "");
        text = text.Replace("&&", "and", StringComparison.Ordinal).Replace("||", "or", StringComparison.Ordinal);
        text = text.Replace("!=", "<>PLACEHOLDER", StringComparison.Ordinal).Replace("!", "not", StringComparison.Ordinal).Replace("<>PLACEHOLDER", "!=", StringComparison.Ordinal);
        text = Regex.Replace(text, @"(and|or)\(([^()]*(?:\([^()]*\)[^()]*)*)\)", "$1$2");
        text = Regex.Replace(text, @"([A-Za-z_][A-Za-z0-9_.]*)\(\)", "$1");
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
        builder.AppendLine($"Migrated C# branches: {report.MigratedCSharpBranches.Count}");
        builder.AppendLine($"Remaining C# branches: {report.RemainingCSharpBranches.Count}");
        builder.AppendLine($"Corresponding SEG branches: {report.CorrespondingDslBranchCount}");
        builder.AppendLine($"Missing: {report.MissingCount}");
        builder.AppendLine($"Changed: {report.ChangedCount}");
        builder.AppendLine($"Order mismatch: {report.OrderMismatchCount}");
        builder.AppendLine($"Direct side effect mismatch: {report.DirectSideEffectMismatchCount}");
        builder.AppendLine($"Strings mismatch: {report.StringMismatchCount}");
        builder.AppendLine($"Unverifiable: {report.UnverifiableCount}");
        builder.AppendLine("C# branch inventory:");
        foreach (var branch in report.CSharpBranches)
            builder.AppendLine($"  depth={branch.Nesting} {branch.Kind} {branch.Condition ?? "<else>"} directEffects={branch.DirectEffects.Count} ({branch.SourcePath}:{branch.SourceLine})");
        builder.AppendLine("SEG branch inventory:");
        foreach (var branch in report.DslBranches)
            builder.AppendLine($"  depth={branch.Nesting} {branch.Kind} {branch.Condition ?? "<else>"} directEffects={branch.DirectEffects.Count} ({branch.SourcePath}:{branch.SourceLine})");
        foreach (var finding in report.Findings)
            builder.AppendLine($"{finding.Status}: {finding.Kind}: {finding.Message}");
        if (report.Findings.Count == 0) builder.AppendLine("No differences found.");
        return builder.ToString();
    }
}
