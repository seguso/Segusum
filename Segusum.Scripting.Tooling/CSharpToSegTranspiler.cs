using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Segusum.Scripting.Core;

namespace Segusum.Scripting.Tooling;

public enum MigrationUnitStatus { Translated, Partial, Unsupported, DependsOnCSharpHelper }

public sealed record MigrationDiagnostic(MigrationUnitStatus Status, string Path, int Line, string Reason, string? Original = null);
public sealed record MigrationUnit(string Id, string Path, int Line, int EndLine, string GeneratedSeg, MigrationUnitStatus Status, IReadOnlyList<MigrationDiagnostic> Diagnostics);
public sealed record MigrationOutput(string Text, IReadOnlyList<MigrationDiagnostic> Diagnostics)
{
    public IReadOnlyList<MigrationUnit> Units { get; init; } = Array.Empty<MigrationUnit>();
    public bool IsFullyTranslated => Units.Count != 0 && Units.All(x => x.Status == MigrationUnitStatus.Translated);
}

/// <summary>Deterministic Roslyn-based first-pass C# to SEG emitter.</summary>
public static class CSharpToSegTranspiler
{
    private static readonly Dictionary<string, IReadOnlyDictionary<string, string?>> NamedCutsceneIndexCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object NamedCutsceneIndexLock = new();

    public static MigrationOutput Transpile(string path, string text, bool emitPartial = false, string? methodName = null)
    {
        var tree = CSharpSyntaxTree.ParseText(text, path: path);
        var diagnostics = new List<MigrationDiagnostic>();
        var sb = new StringBuilder("world migrated\n");
        var root = (CompilationUnitSyntax)tree.GetRoot();
        var reachableHelpers = ReachableHelpers(root);
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
                EmitTriviaComments(method.GetLeadingTrivia(), sb, 0);
                sb.AppendLine("after-action-executed:");
                EmitTriviaComments(method.Body?.OpenBraceToken.TrailingTrivia ?? default, sb, 1);
                EmitStatements(method.Body?.Statements ?? default, sb, diagnostics, 1, emitPartial);
                EmitTriviaComments(method.Body?.CloseBraceToken.LeadingTrivia ?? default, sb, 1);
                EmitTriviaComments(method.Body?.CloseBraceToken.TrailingTrivia ?? default, sb, 1);
                sb.AppendLine("end");
                diagnostics.Add(new(MigrationUnitStatus.Partial, path, method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    "special handler round-trip is not yet certifiable"));
            }
            else if (method.Identifier.ValueText is "beforeRoomChangeManual" or "beforeRoomChangeSegusum")
            {
                EmitTriviaComments(method.GetLeadingTrivia(), sb, 0);
                sb.AppendLine("before-room-change:");
                EmitTriviaComments(method.Body?.OpenBraceToken.TrailingTrivia ?? default, sb, 1);
                EmitStatements(method.Body?.Statements ?? default, sb, diagnostics, 1, emitPartial);
                EmitTriviaComments(method.Body?.CloseBraceToken.LeadingTrivia ?? default, sb, 1);
                EmitTriviaComments(method.Body?.CloseBraceToken.TrailingTrivia ?? default, sb, 1);
                sb.AppendLine("end");
                diagnostics.Add(new(MigrationUnitStatus.Partial, path, method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    "special handler round-trip is not yet certifiable"));
            }
            else if (method.Identifier.ValueText is not "Configure" && method.Body != null && reachableHelpers.Contains(method))
            {
                EmitTriviaComments(AdjacentLeadingComments(method), sb, 0);
                EmitTriviaComments(method.GetLeadingTrivia(), sb, 0);
                sb.Append("def ").Append(method.Identifier.ValueText);
                if (method.ParameterList.Parameters.Count != 0) sb.Append(' ').Append(string.Join(" ", method.ParameterList.Parameters.Select(x => x.Identifier.ValueText)));
                sb.AppendLine(":");
                EmitTriviaComments(method.Body.OpenBraceToken.TrailingTrivia, sb, 1);
                EmitStatements(method.Body.Statements, sb, diagnostics, 1, emitPartial, true);
                EmitTriviaComments(method.Body.CloseBraceToken.LeadingTrivia, sb, 1);
                EmitTriviaComments(method.Body.CloseBraceToken.TrailingTrivia, sb, 1);
                sb.AppendLine("end");
                diagnostics.Add(new(MigrationUnitStatus.DependsOnCSharpHelper, path, method.GetLocation().GetLineSpan().StartLinePosition.Line + 1,
                    "helper unit round-trip is not yet certifiable"));
            }
        }
        var generated = sb.ToString();
        var parsed = DslParser.Parse(new Segusum.Scripting.Core.DslSource(path + ".generated.seg", generated));
        foreach (var diagnostic in parsed.Diagnostics)
            diagnostics.Add(new(MigrationUnitStatus.Unsupported, path + ".generated.seg", diagnostic.Span.Line, "generated SEG is not parsable: " + diagnostic.Message));
        var units = BuildUnits(path, root, reachableHelpers);
        return new MigrationOutput(generated, diagnostics) { Units = units };
    }

    public static IReadOnlyList<string> CommentInventory(string path, string text)
        => CommentInventory((CompilationUnitSyntax)CSharpSyntaxTree.ParseText(text, path: path).GetRoot());

    private static IReadOnlyList<string> CommentInventory(CompilationUnitSyntax root)
        => root.DescendantTrivia()
            .Where(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineCommentTrivia) || x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineCommentTrivia))
            .Select(x => x.ToString().Trim())
            .ToArray();

    private static IReadOnlyList<MigrationUnit> BuildUnits(string path, CompilationUnitSyntax root, IReadOnlySet<MethodDeclarationSyntax> reachableHelpers)
    {
        var units = new List<MigrationUnit>();
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(x => RegistrationKind(x) != null))
        {
            var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var id = RegistrationKind(invocation) + ":" + Arg(invocation.ArgumentList.Arguments, 0);
            var endLine = invocation.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
            var isolated = IsolateHandler(path, invocation);
            units.Add(CreateUnit(id, path, line, endLine, isolated.Text, isolated.Diagnostics));
        }
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(x => x.Body != null && (x.Identifier.ValueText is "afterActionExecutedCSharp" or "beforeRoomChangeManual" or "beforeRoomChangeSegusum" || reachableHelpers.Contains(x))))
        {
            var line = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var endLine = method.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
            var isolated = IsolateMethod(path, method);
            units.Add(CreateUnit(method.Identifier.ValueText, path, line, endLine, isolated.Text, isolated.Diagnostics));
        }
        var helpers = reachableHelpers
            .Where(x => x.Body != null)
            .ToDictionary(x => x.Identifier.ValueText, StringComparer.Ordinal);
        var helperUnits = units.Where(x => helpers.ContainsKey(x.Id)).ToDictionary(x => x.Id, StringComparer.Ordinal);
        var dependencyCache = new Dictionary<string, string[]>(StringComparer.Ordinal);
        string[] Dependencies(MethodDeclarationSyntax helper) => dependencyCache.TryGetValue(helper.Identifier.ValueText, out var cached)
            ? cached
            : dependencyCache[helper.Identifier.ValueText] = helper.DescendantNodes().OfType<InvocationExpressionSyntax>()
                .Select(CallName).Where(helpers.ContainsKey).Distinct(StringComparer.Ordinal).ToArray();
        bool DependsOnNonTranslatable(string name, HashSet<string> visiting)
        {
            if (!visiting.Add(name)) return true;
            return Dependencies(helpers[name]).Any(x => helperUnits[x].Status != MigrationUnitStatus.Translated || DependsOnNonTranslatable(x, visiting));
        }
        foreach (var helper in helpers.Values)
        {
            if (!DependsOnNonTranslatable(helper.Identifier.ValueText, new(StringComparer.Ordinal))) continue;
            var unit = helperUnits[helper.Identifier.ValueText];
            if (unit.Status != MigrationUnitStatus.Translated || unit.Diagnostics.Any(x => x.Status == MigrationUnitStatus.DependsOnCSharpHelper)) continue;
            var dependencies = Dependencies(helper).Where(x => helperUnits[x].Status != MigrationUnitStatus.Translated).ToArray();
            var diagnostic = new MigrationDiagnostic(MigrationUnitStatus.DependsOnCSharpHelper, path, StartLine(helper),
                "depends on non-translatable local helper: " + string.Join(", ", dependencies));
            var updated = unit with { Status = MigrationUnitStatus.DependsOnCSharpHelper, Diagnostics = unit.Diagnostics.Concat(new[] { diagnostic }).ToArray() };
            var index = units.IndexOf(unit);
            units[index] = updated;
            helperUnits[helper.Identifier.ValueText] = updated;
        }
        return units;
    }

    private sealed record IsolatedUnit(string Text, IReadOnlyList<MigrationDiagnostic> Diagnostics);

    private static MigrationUnit CreateUnit(string id, string path, int line, int endLine, string generated, IReadOnlyList<MigrationDiagnostic> diagnostics)
    {
        var status = diagnostics.Any(x => x.Status == MigrationUnitStatus.Unsupported) ? MigrationUnitStatus.Unsupported
            : diagnostics.Any(x => x.Status == MigrationUnitStatus.Partial) ? MigrationUnitStatus.Partial
            : diagnostics.Any(x => x.Status == MigrationUnitStatus.DependsOnCSharpHelper) ? MigrationUnitStatus.DependsOnCSharpHelper
            : MigrationUnitStatus.Translated;
        return new MigrationUnit(id, path, line, endLine, generated, status, diagnostics);
    }

    private static IsolatedUnit IsolateHandler(string path, InvocationExpressionSyntax invocation)
    {
        var diagnostics = new List<MigrationDiagnostic>();
        var sb = new StringBuilder("world migrated\n");
        EmitHandler(invocation, sb, diagnostics, false);
        var text = EnsureComments(invocation, sb.ToString());
        VerifyIsolated(path, invocation, text, diagnostics, true);
        return new(text, diagnostics);
    }

    private static IsolatedUnit IsolateMethod(string path, MethodDeclarationSyntax method)
    {
        var diagnostics = new List<MigrationDiagnostic>();
        var sb = new StringBuilder("world migrated\n");
        if (method.Identifier.ValueText == "afterActionExecutedCSharp")
        {
            EmitTriviaComments(method.GetLeadingTrivia(), sb, 0);
            sb.AppendLine("after-action-executed:");
            EmitTriviaComments(method.Body!.OpenBraceToken.TrailingTrivia, sb, 1);
            EmitStatements(method.Body.Statements, sb, diagnostics, 1, false);
            EmitTriviaComments(method.Body.CloseBraceToken.LeadingTrivia, sb, 1);
            EmitTriviaComments(method.Body.CloseBraceToken.TrailingTrivia, sb, 1);
            sb.AppendLine("end");
            diagnostics.Add(new(MigrationUnitStatus.Partial, path, StartLine(method), "special handler round-trip is not yet certifiable"));
        }
        else if (method.Identifier.ValueText is "beforeRoomChangeManual" or "beforeRoomChangeSegusum")
        {
            EmitTriviaComments(method.GetLeadingTrivia(), sb, 0);
            sb.AppendLine("before-room-change:");
            EmitTriviaComments(method.Body!.OpenBraceToken.TrailingTrivia, sb, 1);
            EmitStatements(method.Body.Statements, sb, diagnostics, 1, false);
            EmitTriviaComments(method.Body.CloseBraceToken.LeadingTrivia, sb, 1);
            EmitTriviaComments(method.Body.CloseBraceToken.TrailingTrivia, sb, 1);
            sb.AppendLine("end");
            diagnostics.Add(new(MigrationUnitStatus.Partial, path, StartLine(method), "special handler round-trip is not yet certifiable"));
        }
        else
        {
            EmitTriviaComments(AdjacentLeadingComments(method), sb, 0);
            sb.Append("def ").Append(method.Identifier.ValueText);
            if (method.ParameterList.Parameters.Count != 0) sb.Append(' ').Append(string.Join(" ", method.ParameterList.Parameters.Select(x => x.Identifier.ValueText)));
            sb.AppendLine(":");
            EmitTriviaComments(method.GetLeadingTrivia(), sb, 0);
            EmitTriviaComments(method.Body!.OpenBraceToken.TrailingTrivia, sb, 1);
            EmitStatements(method.Body.Statements, sb, diagnostics, 1, false);
            EmitTriviaComments(method.Body.CloseBraceToken.LeadingTrivia, sb, 1);
            EmitTriviaComments(method.Body.CloseBraceToken.TrailingTrivia, sb, 1);
            sb.AppendLine("end");
        }
        var text = EnsureComments(method, sb.ToString());
        VerifyIsolated(path, method, text, diagnostics, false);
        return new(text, diagnostics);
    }

    private static void VerifyIsolated(string path, SyntaxNode source, string generated, List<MigrationDiagnostic> diagnostics, bool handler)
    {
        var parsed = DslParser.Parse(new DslSource(path + ".generated.seg", generated));
        foreach (var diagnostic in parsed.Diagnostics)
            diagnostics.Add(new(MigrationUnitStatus.Unsupported, path + ".generated.seg", diagnostic.Span.Line, "generated SEG is not parsable: " + diagnostic.Message));
        var sourceComments = CommentInventory(source);
        var generatedComments = CommentInventory(generated);
        foreach (var comment in sourceComments.Distinct(StringComparer.Ordinal))
        {
            var expected = sourceComments.Count(x => x == comment);
            var actual = generatedComments.Count(x => x == comment);
            if (expected != actual)
                diagnostics.Add(new(MigrationUnitStatus.Unsupported, path, StartLine(source),
                    $"comment was not preserved with exact cardinality: '{comment}' expected {expected}, actual {actual}"));
        }
        if (parsed.Diagnostics.Count != 0) return;
        if (handler && source is InvocationExpressionSyntax invocation)
        {
            var cs = MigrationVerifier.ExtractCSharpRegistrations(path, "class W { void M() { " + invocation.ToFullString() + " } }").FirstOrDefault();
            var dsl = MigrationVerifier.ExtractDslRegistrations(new DslSource(path + ".generated.seg", generated)).FirstOrDefault();
            var comparison = cs is null || dsl is null ? null : MigrationVerifier.CompareRegistration(cs, dsl);
            if (comparison is null || comparison.Overall != EquivalenceStatus.Pass)
            {
                var detail = comparison is null
                    ? "handler fingerprint could not be extracted"
                    : comparison.Checks.FirstOrDefault(x => x.Status != EquivalenceStatus.Pass)?.Detail ?? "handler fingerprint differs";
                diagnostics.Add(new(MigrationUnitStatus.Unsupported, path, StartLine(source), "semantic round-trip mismatch: " + detail));
            }
        }
        if (source is MethodDeclarationSyntax method)
        {
            if (method.Identifier.ValueText is not ("afterActionExecutedCSharp" or "beforeRoomChangeManual" or "beforeRoomChangeSegusum"))
            {
                var helperCheck = MigrationVerifier.CompareHelperMethod(method, new DslSource(path + ".generated.seg", generated));
                if (helperCheck.Status != EquivalenceStatus.Pass)
                    diagnostics.Add(new(MigrationUnitStatus.Unsupported, path, StartLine(method),
                        "helper semantic round-trip mismatch: " + helperCheck.Detail));
            }
            var csCycles = MigrationVerifier.ExtractCSharpCycles(path, "class W { " + method.ToFullString() + " }");
            var dslCycles = MigrationVerifier.ExtractDslCycles(new DslSource(path + ".generated.seg", generated));
            foreach (var cycle in csCycles)
            {
                var dslCycle = dslCycles.FirstOrDefault(x => x.Id == cycle.Id);
                if (dslCycle is null || MigrationVerifier.CompareCycles(new[] { cycle }, new[] { dslCycle }).Status != EquivalenceStatus.Pass)
                    diagnostics.Add(new(MigrationUnitStatus.Unsupported, path, cycle.SourceLine, "semantic round-trip mismatch: cycle fingerprint differs"));
            }
        }
    }

    private static int StartLine(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static IReadOnlyList<string> CommentInventory(SyntaxNode node)
        => CommentsForNode(node).Select(x => NormalizeComment(x.ToString())).ToArray();

    private static IReadOnlyList<string> CommentInventory(string generated)
        => generated.Split('\n').Select(x => x.Trim()).Where(x => x.StartsWith("//", StringComparison.Ordinal)).Select(NormalizeComment).ToArray();

    private static string EnsureComments(SyntaxNode source, string generated)
    {
        var expected = CommentsForNode(source).Select(x => NormalizeComment(x.ToString())).ToArray();
        var actual = CommentInventory(generated).ToList();
        var missing = new List<string>();
        foreach (var comment in expected)
        {
            var index = actual.IndexOf(comment);
            if (index >= 0) actual.RemoveAt(index);
            else missing.Add(comment);
        }
        if (missing.Count == 0) return generated;
        var insertion = generated.LastIndexOf("\nend", StringComparison.Ordinal);
        if (insertion < 0) return generated + string.Concat(missing.Select(x => "// " + x[2..] + Environment.NewLine));
        return generated.Insert(insertion, string.Concat(missing.Select(x => "\n// " + x[2..])));
    }

    private static string NormalizeComment(string comment)
    {
        var value = comment.Trim();
        return value.StartsWith("//", StringComparison.Ordinal) ? "//" + value[2..].TrimStart() : value;
    }

    private static bool IsComment(SyntaxTrivia x)
        => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineCommentTrivia) || x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineCommentTrivia);

    private static IEnumerable<SyntaxTrivia> CommentsForNode(SyntaxNode node)
    {
        var trivia = node.GetLeadingTrivia().Concat(node.DescendantTrivia()).Concat(node.GetTrailingTrivia());
        // Leading/trailing trivia can belong to the previous/next sibling when
        // Roslyn computes FullSpan.  Only trivia structurally inside this node
        // belongs to its unit; adjacent header trivia is emitted separately.
        return trivia.Where(IsComment)
            .Where(x => x.SpanStart >= node.SpanStart && x.Span.End <= node.Span.End)
            .GroupBy(x => x.SpanStart).Select(x => x.First()).OrderBy(x => x.SpanStart);
    }

    private static IEnumerable<SyntaxTrivia> AdjacentLeadingComments(SyntaxNode node)
    {
        var siblings = node.Parent?.ChildNodes().ToArray();
        if (siblings is null) return Enumerable.Empty<SyntaxTrivia>();
        var index = Array.IndexOf(siblings, node);
        return index > 0 ? siblings[index - 1].GetTrailingTrivia().Where(IsComment) : Enumerable.Empty<SyntaxTrivia>();
    }

    private static bool IsHelperCandidate(MethodDeclarationSyntax method)
        => method.Modifiers.Any(x => x.ValueText is "private" or "protected") && method.ParameterList.Parameters.All(x => x.Type != null);

    private static bool IsRegistrationContainer(MethodDeclarationSyntax method)
        => method.Body?.DescendantNodes().OfType<InvocationExpressionSyntax>().Any(x => RegistrationKind(x) != null) == true;

    private static IReadOnlySet<MethodDeclarationSyntax> ReachableHelpers(CompilationUnitSyntax root)
    {
        var methods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(x => x.Body != null && IsHelperCandidate(x) && !IsRegistrationContainer(x))
            .ToDictionary(x => x.Identifier.ValueText, StringComparer.Ordinal);
        var work = new Stack<MethodDeclarationSyntax>();
        var registrationCalls = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(x => RegistrationKind(x) != null).ToArray();
        foreach (var helper in methods.Values)
            if (registrationCalls.SelectMany(x => x.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
                .Any(x => CallName(x) == helper.Identifier.ValueText)) work.Push(helper);
        foreach (var special in root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                     .Where(x => x.Identifier.ValueText is "afterActionExecutedCSharp" or "beforeRoomChangeManual" or "beforeRoomChangeSegusum"))
            foreach (var invocation in special.DescendantNodes().OfType<InvocationExpressionSyntax>())
                if (methods.TryGetValue(CallName(invocation), out var helper)) work.Push(helper);
        var reachable = new HashSet<MethodDeclarationSyntax>();
        while (work.Count != 0)
        {
            var helper = work.Pop();
            if (!reachable.Add(helper)) continue;
            foreach (var invocation in helper.DescendantNodes().OfType<InvocationExpressionSyntax>())
                if (methods.TryGetValue(CallName(invocation), out var dependency)) work.Push(dependency);
        }
        return reachable;
    }

    private static void EmitHandler(InvocationExpressionSyntax invocation, StringBuilder sb, List<MigrationDiagnostic> diagnostics, bool partial)
    {
        var args = invocation.ArgumentList.Arguments;
        var kind = RegistrationKind(invocation)!;
        var first = Arg(args, 0); var second = kind is "combine" or "use-for" ? Arg(args, 1) : null;
        EmitTriviaComments(HandlerHeaderComments(invocation), sb, 0);
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
        if (possible is AnonymousFunctionExpressionSyntax lambda)
        {
            if (lambda.Body is BlockSyntax) Unsupported(lambda, diagnostics, "block-bodied possible-when", partial, sb, 1);
            else sb.Append("  possible-when ").AppendLine(Expression(lambda.Body));
        }
        var handler = args.Select(x => x.Expression).OfType<AnonymousFunctionExpressionSyntax>().LastOrDefault();
        if (handler?.Body is BlockSyntax block)
        {
            EmitTriviaComments(block.OpenBraceToken.TrailingTrivia, sb, 1);
            EmitStatements(block.Statements, sb, diagnostics, 1, partial);
            EmitTriviaComments(block.CloseBraceToken.LeadingTrivia, sb, 1);
            EmitTriviaComments(block.CloseBraceToken.TrailingTrivia, sb, 1);
        }
        else if (handler != null) Unsupported(handler, diagnostics, "expression-bodied handler", partial, sb, 1);
        sb.AppendLine("end");
    }

    private static IEnumerable<SyntaxTrivia> HandlerHeaderComments(InvocationExpressionSyntax invocation)
    {
        var statement = invocation.AncestorsAndSelf().OfType<ExpressionStatementSyntax>().FirstOrDefault();
        var trivia = (statement?.GetLeadingTrivia() ?? default)
            .Concat(invocation.GetLeadingTrivia())
            .Concat(invocation.DescendantTrivia())
            .Concat(invocation.GetTrailingTrivia())
            .Concat(AdjacentStatementTrivia(statement))
            .Where(IsComment)
            .GroupBy(x => x.SpanStart)
            .Select(x => x.First())
            .OrderBy(x => x.SpanStart);
        var lambdaBody = invocation.ArgumentList.Arguments.Select(x => x.Expression)
            .OfType<AnonymousFunctionExpressionSyntax>().LastOrDefault()?.Body;
        if (lambdaBody is null) return trivia;
        return trivia.Where(x => !lambdaBody.FullSpan.Contains(x.SpanStart));
    }

    private static IEnumerable<SyntaxTrivia> AdjacentStatementTrivia(ExpressionStatementSyntax? statement)
    {
        if (statement?.Parent is not BlockSyntax block) return Enumerable.Empty<SyntaxTrivia>();
        var siblings = block.Statements;
        var index = siblings.IndexOf(statement);
        return index == 0
            ? block.OpenBraceToken.TrailingTrivia
            : index > 0 ? siblings[index - 1].GetTrailingTrivia() : Enumerable.Empty<SyntaxTrivia>();
    }

    private static void EmitStatements(IEnumerable<StatementSyntax> statements, StringBuilder sb, List<MigrationDiagnostic> diagnostics, int level, bool partial, bool includeComments = true)
    {
        foreach (var statement in statements)
        {
            try
            {
            if (includeComments) EmitComments(statement, sb, level);
            var indent = new string(' ', level * 4);
            switch (statement)
            {
                case IfStatementSyntax x:
                    sb.Append(indent).Append("if ").Append(Expression(x.Condition)).AppendLine(":");
                    EmitStatements(x.Statement is BlockSyntax b ? b.Statements : new[] { x.Statement }, sb, diagnostics, level + 1, partial, includeComments);
                    var e = x.Else;
                    while (e?.Statement is IfStatementSyntax elif)
                    { sb.Append(indent).Append("elif ").Append(Expression(elif.Condition)).AppendLine(":"); EmitStatements(elif.Statement is BlockSyntax eb ? eb.Statements : new[] { elif.Statement }, sb, diagnostics, level + 1, partial, includeComments); e = elif.Else; }
                    if (e != null) { sb.Append(indent).AppendLine("else:"); EmitStatements(e.Statement is BlockSyntax eb ? eb.Statements : new[] { e.Statement }, sb, diagnostics, level + 1, partial, includeComments); }
                    sb.Append(indent).AppendLine("end"); break;
                case ExpressionStatementSyntax x when x.Expression is AssignmentExpressionSyntax a:
                    if (a.Right is InvocationExpressionSyntax add && CallName(add) == "addToCycle") EmitCycleChain(add, sb, diagnostics, level, partial);
                    else if (a.Left.ToString().EndsWith("makesNoSenseAtThisTime", StringComparison.Ordinal) && a.Right.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.TrueLiteralExpression)) sb.Append(indent).AppendLine("makes-no-sense");
                    else if (a.Left.ToString().EndsWith("textInputToShow", StringComparison.Ordinal)) sb.Append(indent).Append("text-input ").AppendLine(Expression(a.Right));
                    else sb.Append(indent).Append(Expression(a.Left)).Append(' ').Append(a.OperatorToken.Text).Append(' ').AppendLine(Expression(a.Right)); break;
                case ExpressionStatementSyntax x when x.Expression is InvocationExpressionSyntax i:
                    EmitInvocation(i, sb, diagnostics, level, partial); break;
                case ExpressionStatementSyntax x when x.Expression is PostfixUnaryExpressionSyntax p && p.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PostIncrementExpression):
                    sb.Append(indent).Append(Expression(p.Operand)).AppendLine("++"); break;
                case ExpressionStatementSyntax x when x.Expression is PrefixUnaryExpressionSyntax p && p.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PreIncrementExpression):
                    sb.Append(indent).Append(Expression(p.Operand)).AppendLine("++"); break;
                case ExpressionStatementSyntax x:
                    try { sb.Append(indent).AppendLine(Expression(x.Expression)); }
                    catch (InvalidOperationException ex) { Unsupported(x.Expression, diagnostics, ex.Message, partial, sb, level); }
                    break;
                case BlockSyntax x:
                    EmitStatements(x.Statements, sb, diagnostics, level, partial, includeComments); break;
                case LocalDeclarationStatementSyntax x:
                    foreach (var v in x.Declaration.Variables)
                    {
                        try
                        {
                            if (v.Initializer?.Value is InvocationExpressionSyntax start && FindStartCycle(start) != null)
                                EmitCycle(start, v.Identifier.ValueText, sb, diagnostics, level, partial);
                            else if (v.Initializer != null) sb.Append(indent).Append("var ").Append(v.Identifier.ValueText).Append(" = ").Append(Expression(v.Initializer.Value)).AppendLine();
                        }
                        catch (InvalidOperationException ex) { Unsupported(v.Initializer ?? (SyntaxNode)v, diagnostics, ex.Message, partial, sb, level); }
                    }
                    break;
                case UsingStatementSyntax x when x.Expression is InvocationExpressionSyntax call && CallName(call) == "namedCutScene":
                    EmitNamedCutscene(call, x.Statement, sb, diagnostics, level, partial); break;
                case ReturnStatementSyntax x when x.Expression is InvocationExpressionSyntax cycleReturn && FindStartCycle(cycleReturn) != null:
                    EmitCycle(cycleReturn, "cyc", sb, diagnostics, level, partial);
                    sb.Append(indent).AppendLine("ret cyc");
                    break;
                case EmptyStatementSyntax: break;
                case ReturnStatementSyntax x: sb.Append(indent).Append("ret ").AppendLine(Expression(x.Expression)); break;
                default: Unsupported(statement, diagnostics, "unsupported statement " + statement.Kind(), partial, sb, level); break;
            }
            foreach (var t in includeComments ? statement.GetTrailingTrivia().Where(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineCommentTrivia) || x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineCommentTrivia)) : Enumerable.Empty<SyntaxTrivia>())
                sb.Append(indent).Append("// ").AppendLine(t.ToString().TrimStart('/').Trim());
            }
            catch (InvalidOperationException ex) { Unsupported(statement, diagnostics, ex.Message, partial, sb, level); }
        }
    }

    private static void EmitInvocation(InvocationExpressionSyntax invocation, StringBuilder sb, List<MigrationDiagnostic> diagnostics, int level, bool partial)
    {
        var indent = new string(' ', level * 4); var name = invocation.Expression.ToString();
        if (CallName(invocation) == "addToCycle") { EmitCycleChain(invocation, sb, diagnostics, level, partial); return; }
        if (CallName(invocation) == "execNextInCycle") { sb.Append(indent).Append("next ").AppendLine(Arg(invocation.ArgumentList.Arguments, 0)); return; }
        if (CallName(invocation) == "startCycle") { Unsupported(invocation, diagnostics, "startCycle must be assigned to a cycle variable", partial, sb, level); return; }
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
        try { sb.Append(indent).AppendLine(Expression(invocation)); }
        catch (InvalidOperationException ex) { Unsupported(invocation, diagnostics, ex.Message, partial, sb, level); }
    }

    private static InvocationExpressionSyntax? FindStartCycle(InvocationExpressionSyntax invocation)
    {
        var current = invocation;
        while (true)
        {
            if (CallName(current) == "startCycle") return current;
            if (current.Expression is not MemberAccessExpressionSyntax member || member.Expression is not InvocationExpressionSyntax next) return null;
            current = next;
        }
    }

    private static void EmitCycle(InvocationExpressionSyntax initializer, string variable, StringBuilder sb, List<MigrationDiagnostic> diagnostics, int level, bool partial)
    {
        var start = FindStartCycle(initializer)!;
        sb.Append(new string(' ', level * 4)).Append("var ").Append(variable).AppendLine(" = new-cycle");
        EmitCycleElementCore(variable, start.ArgumentList.Arguments, start, sb, diagnostics, level, partial, true);
        foreach (var add in initializer.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>().Where(x => CallName(x) == "addToCycle").OrderBy(x => x.Span.End))
            EmitCycleElementCore(variable, add.ArgumentList.Arguments, add, sb, diagnostics, level, partial, false);
    }

    private static void EmitCycleElement(InvocationExpressionSyntax invocation, StringBuilder sb, List<MigrationDiagnostic> diagnostics, int level, bool partial)
        => EmitCycleElementCore(invocation.Expression is MemberAccessExpressionSyntax member ? Expression(member.Expression) : "cyc", invocation.ArgumentList.Arguments, invocation, sb, diagnostics, level, partial, false);

    private static void EmitCycleChain(InvocationExpressionSyntax outer, StringBuilder sb, List<MigrationDiagnostic> diagnostics, int level, bool partial)
    {
        var current = outer;
        var elements = new List<InvocationExpressionSyntax>();
        string? cycle = null;
        while (CallName(current) == "addToCycle" && current.Expression is MemberAccessExpressionSyntax member)
        {
            elements.Add(current);
            if (member.Expression is InvocationExpressionSyntax nested) current = nested;
            else { cycle = Expression(member.Expression); break; }
        }
        if (cycle is null) { Unsupported(outer, diagnostics, "addToCycle receiver is not a cycle expression", partial, sb, level); return; }
        foreach (var element in elements.AsEnumerable().Reverse())
            EmitCycleElementCore(cycle, element.ArgumentList.Arguments, element, sb, diagnostics, level, partial, false);
    }

    private static void EmitCycleElementCore(string cycle, SeparatedSyntaxList<ArgumentSyntax> args, InvocationExpressionSyntax invocation, StringBuilder sb, List<MigrationDiagnostic> diagnostics, int level, bool partial, bool start)
    {
        var indent = new string(' ', level * 4);
        var id = Arg(args, 0);
        var important = EnumValue(args, "Importance", "Important");
        var repeat = EnumValue(args, "Repeat", "OnlyOnce", "Forever");
        var lambdas = args.Select(x => x.Expression).OfType<AnonymousFunctionExpressionSyntax>().ToArray();
        var predicate = lambdas.Length > 1 ? CycleExpression(lambdas[0]) : null;
        var body = lambdas.Length == 0 ? null : lambdas[^1].Body as BlockSyntax;
        sb.Append(indent).Append("add ").Append(cycle).Append(' ').Append(id);
        if (important == "Important") sb.Append(" important");
        if (repeat == "OnlyOnce") sb.Append(" once"); else if (repeat == "Forever") sb.Append(" forever");
        if (predicate != null) sb.AppendLine().Append(indent).Append(" when ").AppendLine(predicate);
        else sb.AppendLine();
        if (body != null) EmitStatements(body.Statements, sb, diagnostics, level + 1, partial);
        sb.Append(indent).AppendLine("end");
        if (important?.StartsWith("unsupported:", StringComparison.Ordinal) == true || repeat?.StartsWith("unsupported:", StringComparison.Ordinal) == true)
            Unsupported(invocation, diagnostics, "unsupported cycle metadata", partial, sb, level);
    }

    private static void EmitNamedCutscene(InvocationExpressionSyntax invocation, StatementSyntax statement, StringBuilder sb, List<MigrationDiagnostic> diagnostics, int level, bool partial)
    {
        var indent = new string(' ', level * 4);
        var id = Arg(invocation.ArgumentList.Arguments, 0);
        var title = NamedCutsceneTitle(invocation, id);
        if (title is null) Unsupported(invocation, diagnostics, "NamedCutSceneId title cannot be resolved structurally", partial, sb, level);
        sb.Append(indent).Append("named-cutscene ").Append(id).Append(' ').Append(title ?? "\"\"");
        foreach (var arg in invocation.ArgumentList.Arguments.Skip(1)) sb.Append(' ').Append(Expression(arg.Expression));
        sb.AppendLine(":");
        if (statement is BlockSyntax block) EmitStatements(block.Statements, sb, diagnostics, level + 1, partial);
        sb.Append(indent).AppendLine("end");
    }

    private static string? NamedCutsceneTitle(InvocationExpressionSyntax invocation, string id)
    {
        var sourcePath = invocation.SyntaxTree.FilePath;
        var directory = string.IsNullOrEmpty(sourcePath) ? "" : Path.GetDirectoryName(sourcePath) ?? "";
        if (directory.Length == 0)
        {
            return FindNamedCutsceneTitle(invocation.SyntaxTree.GetRoot(), id);
        }
        IReadOnlyDictionary<string, string?> index;
        lock (NamedCutsceneIndexLock)
        {
            if (!NamedCutsceneIndexCache.TryGetValue(directory, out index!))
            {
                var map = new Dictionary<string, string?>(StringComparer.Ordinal);
                foreach (var candidatePath in Directory.EnumerateFiles(directory, "*.cs", SearchOption.TopDirectoryOnly))
                {
                    try
                    {
                        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(candidatePath), path: candidatePath).GetRoot();
                        foreach (var declaration in root.DescendantNodes().OfType<VariableDeclaratorSyntax>().Where(x => x.Initializer?.Value is ObjectCreationExpressionSyntax creation && creation.Type.ToString().EndsWith("NamedCutSceneId", StringComparison.Ordinal)))
                        {
                            var title = FindNamedCutsceneTitle(root, declaration.Identifier.ValueText);
                            if (map.ContainsKey(declaration.Identifier.ValueText)) map[declaration.Identifier.ValueText] = null;
                            else map[declaration.Identifier.ValueText] = title;
                        }
                    }
                    catch (IOException) { }
                }
                NamedCutsceneIndexCache[directory] = index = map;
            }
        }
        return index.TryGetValue(id, out var result) ? result : FindNamedCutsceneTitle(invocation.SyntaxTree.GetRoot(), id);
    }

    private static string? FindNamedCutsceneTitle(SyntaxNode root, string id)
    {
        var declaration = root.DescendantNodes().OfType<VariableDeclaratorSyntax>().FirstOrDefault(x =>
            x.Identifier.ValueText == id && x.Initializer?.Value is ObjectCreationExpressionSyntax creation
            && creation.Type.ToString().EndsWith("NamedCutSceneId", StringComparison.Ordinal));
        return declaration?.Initializer?.Value.DescendantNodes().OfType<LiteralExpressionSyntax>()
            .FirstOrDefault(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression))?.Token.Text;
    }

    private static string? EnumValue(SeparatedSyntaxList<ArgumentSyntax> args, string type, params string[] known)
    {
        var member = args.Select(x => x.Expression).OfType<MemberAccessExpressionSyntax>().FirstOrDefault(x => x.Expression.ToString() == type);
        if (member is null) return null;
        return known.Contains(member.Name.Identifier.ValueText, StringComparer.Ordinal) ? member.Name.Identifier.ValueText : "unsupported:" + type + "." + member.Name.Identifier.ValueText;
    }

    private static string CycleExpression(AnonymousFunctionExpressionSyntax lambda)
    {
        var parameter = lambda switch { SimpleLambdaExpressionSyntax x => x.Parameter.Identifier.ValueText, ParenthesizedLambdaExpressionSyntax x => x.ParameterList.Parameters.FirstOrDefault()?.Identifier.ValueText, _ => null };
        var body = string.IsNullOrEmpty(parameter) ? lambda.Body : new CycleParameterRewriter(parameter!).Visit(lambda.Body);
        return Expression(body);
    }

    private sealed class CycleParameterRewriter : CSharpSyntaxRewriter
    {
        private readonly string parameter;
        public CycleParameterRewriter(string parameter) => this.parameter = parameter;
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (node.Identifier.ValueText == parameter && !(node.Parent is MemberAccessExpressionSyntax member && member.Name == node))
                return SyntaxFactory.IdentifierName("it").WithTriviaFrom(node);
            return base.VisitIdentifierName(node);
        }
    }

    private static string CallName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        IdentifierNameSyntax x => x.Identifier.ValueText,
        MemberAccessExpressionSyntax x => x.Name.Identifier.ValueText,
        _ => ""
    };

    private static void EmitComments(StatementSyntax statement, StringBuilder sb, int level)
    { EmitTriviaComments(statement.GetLeadingTrivia(), sb, level); }
    private static void EmitTriviaComments(IEnumerable<SyntaxTrivia> trivia, StringBuilder sb, int level)
    { foreach (var t in trivia.Where(IsComment)) sb.Append(new string(' ', level * 4)).Append("// ").AppendLine(CommentPayload(t.ToString())); }
    private static string CommentPayload(string comment)
    {
        var value = comment.Trim();
        return value.StartsWith("//", StringComparison.Ordinal) ? value[2..].TrimStart() : value;
    }
    private static void Unsupported(SyntaxNode node, List<MigrationDiagnostic> diagnostics, string reason, bool partial, StringBuilder sb, int level)
    { var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1; diagnostics.Add(new(MigrationUnitStatus.Unsupported, node.SyntaxTree?.FilePath ?? "", line, reason, node.ToString())); if (partial) { var i = new string(' ', level * 4); sb.Append(i).AppendLine("// C2SEG-MANUAL-BEGIN").Append(i).Append("// source: ").AppendLine((node.SyntaxTree?.FilePath ?? "") + ":" + line).Append(i).Append("// reason: ").AppendLine(reason); foreach (var l in node.ToString().Split('\n')) sb.Append(i).Append("// ").AppendLine(l); sb.Append(i).AppendLine("// C2SEG-MANUAL-END"); } }
    private static string Expression(SyntaxNode? node)
    {
        if (node is null) return "";
        return node switch
        {
            IdentifierNameSyntax x => x.Identifier.ValueText,
            LiteralExpressionSyntax x => x.Token.Text,
            ParenthesizedExpressionSyntax x => "(" + Expression(x.Expression) + ")",
            PrefixUnaryExpressionSyntax x when x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalNotExpression) => "not " + Expression(x.Operand),
            PrefixUnaryExpressionSyntax x when x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.UnaryMinusExpression) => "-" + Expression(x.Operand),
            PrefixUnaryExpressionSyntax x when x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.UnaryPlusExpression) => "+" + Expression(x.Operand),
            BinaryExpressionSyntax x when TryEmitRandomModulo(x, out var random) => random,
            BinaryExpressionSyntax x => Expression(x.Left) + " " + BinaryOperator(x.Kind()) + " " + Expression(x.Right),
            MemberAccessExpressionSyntax x => Expression(x.Expression) + "." + x.Name.Identifier.ValueText,
            InvocationExpressionSyntax x => EmitCallExpression(x),
            CollectionExpressionSyntax x => "[" + string.Join(", ", x.Elements.Select(EmitCollectionElement)) + "]",
            ConditionalExpressionSyntax x => "if " + Expression(x.Condition) + " then " + Expression(x.WhenTrue) + " else " + Expression(x.WhenFalse),
            ArgumentSyntax x => (x.NameColon is null ? "" : x.NameColon.Name.Identifier.ValueText + ": ") + Expression(x.Expression),
            _ => throw new InvalidOperationException("Unsupported C# expression: " + node.Kind())
        };
    }

    private static bool TryEmitRandomModulo(BinaryExpressionSyntax expression, out string result)
    {
        result = "";
        if (!expression.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.EqualsExpression)
            || expression.Right is not LiteralExpressionSyntax zero
            || !zero.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NumericLiteralExpression)
            || zero.Token.ValueText != "0"
            || expression.Left is not BinaryExpressionSyntax modulo
            || !modulo.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.ModuloExpression)
            || modulo.Left is not InvocationExpressionSyntax next
            || next.Expression is not MemberAccessExpressionSyntax member
            || member.Name.Identifier.ValueText != "Next"
            || next.ArgumentList.Arguments.Count != 0
            || modulo.Right is not LiteralExpressionSyntax bound
            || !bound.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.NumericLiteralExpression)
            || !int.TryParse(bound.Token.ValueText, out var n)
            || n <= 0) return false;
        result = "random " + n + " == 0";
        return true;
    }

    private static string EmitCollectionElement(CollectionElementSyntax element)
        => element is ExpressionElementSyntax expression
            ? Expression(expression.Expression)
            : throw new InvalidOperationException("Unsupported C# collection element: " + element.Kind());

    private static string EmitCallExpression(InvocationExpressionSyntax invocation)
    {
        var name = invocation.Expression is MemberAccessExpressionSyntax member ? member.Name.Identifier.ValueText : invocation.Expression.ToString();
        if (name == "Any" && invocation.ArgumentList.Arguments.Count == 1)
        {
            if (invocation.Expression is not MemberAccessExpressionSyntax anyMember
                || invocation.ArgumentList.Arguments[0].Expression is not LambdaExpressionSyntax lambda)
                throw new InvalidOperationException("Unsupported Any predicate shape");
            return EmitAnyQuery(anyMember.Expression, lambda);
        }
        if (invocation.ArgumentList.Arguments.Any(x => x.Expression is AnonymousFunctionExpressionSyntax or LambdaExpressionSyntax || x.RefKindKeyword.RawKind != 0))
            throw new InvalidOperationException("Unsupported C# call: " + name);
        // SEG calls are whitespace-delimited (foo a b / receiver.foo a),
        // not C# parenthesized calls. Emitting the C# spelling here can make
        // the parser consume a malformed expression indefinitely, besides
        // losing the actual SEG call structure.
        var receiver = invocation.Expression is MemberAccessExpressionSyntax memberAccess
            ? Expression(memberAccess.Expression) + "." + memberAccess.Name.Identifier.ValueText
            : name;
        return invocation.ArgumentList.Arguments.Count == 0
            ? receiver
            : receiver + " " + string.Join(" ", invocation.ArgumentList.Arguments.Select(x => ExpressionForCallArgument(x.Expression)));
    }

    private static string ExpressionForCallArgument(ExpressionSyntax expression)
        => expression is ConditionalExpressionSyntax ? "(" + Expression(expression) + ")" : Expression(expression);

    private static string EmitAnyQuery(ExpressionSyntax collection, LambdaExpressionSyntax lambda)
    {
        var parameter = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax parenthesized when parenthesized.ParameterList.Parameters.Count == 1
                => parenthesized.ParameterList.Parameters[0].Identifier.ValueText,
            _ => throw new InvalidOperationException("Unsupported Any predicate lambda")
        };
        if (lambda.Body is BlockSyntax)
            throw new InvalidOperationException("Unsupported Any predicate block lambda");
        return "exists [from " + Expression(collection) + " " + parameter + " where " + Expression(lambda.Body) + "]";
    }

    private static string BinaryOperator(Microsoft.CodeAnalysis.CSharp.SyntaxKind kind) => kind switch
    {
        Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalAndExpression => "and",
        Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalOrExpression => "or",
        Microsoft.CodeAnalysis.CSharp.SyntaxKind.EqualsExpression => "==",
        Microsoft.CodeAnalysis.CSharp.SyntaxKind.NotEqualsExpression => "!=",
        _ when kind.ToString().EndsWith("Expression", StringComparison.Ordinal) => kind switch
        {
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.AddExpression => "+",
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.SubtractExpression => "-",
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiplyExpression => "*",
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.DivideExpression => "/",
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.LessThanExpression => "<",
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.LessThanOrEqualExpression => "<=",
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.GreaterThanExpression => ">",
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.GreaterThanOrEqualExpression => ">=",
            _ => throw new InvalidOperationException("Unsupported C# binary expression: " + kind)
        },
        _ => throw new InvalidOperationException("Unsupported C# binary expression: " + kind)
    };
    private static string LiteralOrExpression(SyntaxNode? node) => node is LiteralExpressionSyntax l && l.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression) ? l.Token.Text : Expression(node);
    private static string Arg(SeparatedSyntaxList<ArgumentSyntax> args, int index) => index >= 0 && index < args.Count ? Expression(args[index].Expression) : "";
    private static string? RegistrationKind(InvocationExpressionSyntax x) => x.Expression.ToString() switch { "addHandlerCombine" => "combine", "addHandlerUseFor" => "use-for", "addHandlerUseHere" => "use-here", "addHandlerPickUp" => "pickup", "addHandlerTalkHere" => "talk-here", "addHandlerCancelTextInput" => "cancel-text-input", "addHandlerSubmitTextInput" => "submit-text-input", "addRoomChangedHandler" => "room-changed", _ => null };
}
