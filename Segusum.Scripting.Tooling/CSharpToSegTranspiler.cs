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
    public IReadOnlyList<string> SourceComments { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> GeneratedComments { get; init; } = Array.Empty<string>();
    public bool CommentsPreserved => SourceComments.OrderBy(x => x, StringComparer.Ordinal)
        .SequenceEqual(GeneratedComments.OrderBy(x => x, StringComparer.Ordinal), StringComparer.Ordinal);
    public bool IsFullyTranslated => Units.Count != 0 && Units.All(x => x.Status == MigrationUnitStatus.Translated);
}

/// <summary>Deterministic Roslyn-based first-pass C# to SEG emitter.</summary>
public static class CSharpToSegTranspiler
{
    private sealed record NamedCutsceneContextValue(
        bool HasCSharpDeclaration,
        string? CSharpTitle,
        bool HasSegDeclaration,
        string? SegTitle,
        bool Conflict);
    private static readonly Dictionary<string, IReadOnlyDictionary<string, NamedCutsceneContextValue>> NamedCutsceneIndexCache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly object NamedCutsceneIndexLock = new();
    private sealed record LocalFunctionCapture(string Name, TypeSyntax? Type);
    private sealed record SelectedMethod(
        MethodDeclarationSyntax Syntax,
        IReadOnlyDictionary<string, IReadOnlyList<LocalFunctionCapture>> LocalFunctionCaptures);

    private static string Indent(int level) => new(' ', level * 4);

    public static MigrationOutput Transpile(string path, string text, bool emitPartial = false, string? methodName = null)
        => Transpile(path, text, emitPartial, methodName, "game", null);

    public static MigrationOutput Transpile(string path, string text, bool emitPartial, string? methodName, string worldId, string? contextRoot = null)
    {
        var tree = CSharpSyntaxTree.ParseText(text, path: path);
        var diagnostics = new List<MigrationDiagnostic>();
        var sb = new StringBuilder().Append("world ").Append(worldId).Append('\n');
        var root = (CompilationUnitSyntax)tree.GetRoot();
        var reachableHelpers = ReachableHelpers(root, methodName);
        var selectedMethods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(x => methodName == null || x.Identifier.ValueText == methodName || reachableHelpers.Contains(x)).ToArray();
        var selectedMethodContexts = selectedMethods.Select(method =>
        {
            var captures = BuildLocalFunctionCaptures(method);
            return new SelectedMethod(RewriteLocalFunctionCalls(method, captures), captures);
        }).ToArray();
        foreach (var trivia in root.DescendantTrivia().Where(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineCommentTrivia) || x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineCommentTrivia)))
            if (trivia.Token.Parent?.AncestorsAndSelf().OfType<MethodDeclarationSyntax>().Any() != true)
                sb.AppendLine("// " + trivia.ToString().TrimStart('/').Trim());

        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(x => RegistrationKind(x) != null))
        {
            if (methodName != null && !invocation.Ancestors().OfType<MethodDeclarationSyntax>().Any(x => x.Identifier.ValueText == methodName)) continue;
            EnsureExactlyOneBlankLine(sb);
            EmitHandler(invocation, sb, diagnostics, emitPartial, contextRoot);
        }
        foreach (var methodContext in selectedMethodContexts)
        {
            var method = methodContext.Syntax;
            EnsureExactlyOneBlankLine(sb);
            if (method.Identifier.ValueText is "afterActionExecutedCSharp")
            {
                EmitTriviaComments(method.GetLeadingTrivia(), sb, 0);
                sb.AppendLine("after-action-executed:");
                EmitTriviaComments(method.Body?.OpenBraceToken.TrailingTrivia ?? default, sb, 1);
                EmitStatements(method.Body?.Statements ?? default, sb, diagnostics, 1, emitPartial, true, contextRoot);
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
                EmitStatements(method.Body?.Statements ?? default, sb, diagnostics, 1, emitPartial, true, contextRoot);
                EmitTriviaComments(method.Body?.CloseBraceToken.LeadingTrivia ?? default, sb, 1);
                EmitTriviaComments(method.Body?.CloseBraceToken.TrailingTrivia ?? default, sb, 1);
                sb.AppendLine("end");
            }
            else if (method.Identifier.ValueText is not "Configure" && method.Body != null && reachableHelpers.Contains(method))
            {
                EmitTriviaComments(AdjacentLeadingComments(method), sb, 0);
                EmitTriviaComments(method.GetLeadingTrivia(), sb, 0);
                AppendFunctionHeader(sb, method, diagnostics);
                sb.AppendLine(":");
                EmitTriviaComments(method.Body.OpenBraceToken.TrailingTrivia, sb, 1);
                EmitStatements(method.Body.Statements, sb, diagnostics, 1, emitPartial, true, contextRoot);
                EmitTriviaComments(method.Body.CloseBraceToken.LeadingTrivia, sb, 1);
                EmitTriviaComments(method.Body.CloseBraceToken.TrailingTrivia, sb, 1);
                sb.AppendLine("end");
                EnsureExactlyOneBlankLine(sb);
            }
        }
        var emittedLocalFunctions = new HashSet<int>();
        foreach (var methodContext in selectedMethodContexts)
        {
            var method = methodContext.Syntax;
            foreach (var localFunction in method.Body?.DescendantNodes().OfType<LocalFunctionStatementSyntax>() ?? Enumerable.Empty<LocalFunctionStatementSyntax>())
                if (emittedLocalFunctions.Add(localFunction.SpanStart))
                    EmitLocalFunction(localFunction, sb, diagnostics, emitPartial, contextRoot,
                        methodContext.LocalFunctionCaptures.TryGetValue(localFunction.Identifier.ValueText, out var captures) ? captures : Array.Empty<LocalFunctionCapture>());
        }
        // Comments are preserved mechanically.  If a trivia item was not
        // attached to an emitted construct, retain it at the nearest safe
        // document location instead of turning that fact into a semantic
        // translation failure.
        var generated = EnsureComments(root, sb.ToString());
        var parsed = DslParser.Parse(new Segusum.Scripting.Core.DslSource(path + ".generated.seg", generated));
        foreach (var diagnostic in parsed.Diagnostics)
            diagnostics.Add(new(MigrationUnitStatus.Unsupported, path + ".generated.seg", diagnostic.Span.Line, "generated SEG is not parsable: " + diagnostic.Message));
        var units = BuildUnits(path, root, reachableHelpers, methodName, contextRoot);
        var output = new MigrationOutput(generated, diagnostics)
        {
            Units = units,
            SourceComments = CommentInventory(root),
            GeneratedComments = CommentInventory(generated)
        };
        return output;
    }


    public static IReadOnlyList<string> CommentInventory(string path, string text)
        => CommentInventory((CompilationUnitSyntax)CSharpSyntaxTree.ParseText(text, path: path).GetRoot());

    private static IReadOnlyList<string> CommentInventory(CompilationUnitSyntax root)
        => root.DescendantTrivia()
            .Where(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineCommentTrivia) || x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineCommentTrivia))
            .Select(x => x.ToString().Trim())
            .ToArray();

    private static IReadOnlyList<MigrationUnit> BuildUnits(string path, CompilationUnitSyntax root, IReadOnlySet<MethodDeclarationSyntax> reachableHelpers, string? methodName, string? contextRoot)
    {
        var units = new List<MigrationUnit>();
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(x => RegistrationKind(x) != null))
        {
            if (methodName != null && !invocation.Ancestors().OfType<MethodDeclarationSyntax>().Any(x => x.Identifier.ValueText == methodName)) continue;
            var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var id = RegistrationKind(invocation) + ":" + Arg(invocation.ArgumentList.Arguments, 0);
            var endLine = invocation.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
            var isolated = IsolateHandler(path, invocation);
            units.Add(CreateUnit(id, path, line, endLine, isolated.Text, isolated.Diagnostics));
        }
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(x => x.Body != null && (x.Identifier.ValueText is "afterActionExecutedCSharp" or "beforeRoomChangeManual" or "beforeRoomChangeSegusum" || reachableHelpers.Contains(x)) && (methodName == null || x.Identifier.ValueText == methodName || reachableHelpers.Contains(x))))
        {
            var line = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var endLine = method.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
            var isolated = IsolateMethod(path, method, contextRoot);
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
        var dependencyResultCache = new Dictionary<string, bool>(StringComparer.Ordinal);
        bool DependsOnNonTranslatable(string name, HashSet<string> visiting)
        {
            if (dependencyResultCache.TryGetValue(name, out var cached)) return cached;
            if (!visiting.Add(name)) return true;
            var result = Dependencies(helpers[name]).Any(x =>
                helperUnits[x].Status != MigrationUnitStatus.Translated || DependsOnNonTranslatable(x, visiting));
            visiting.Remove(name);
            dependencyResultCache[name] = result;
            return result;
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
        var sb = new StringBuilder("world game\n");
        EmitHandler(invocation, sb, diagnostics, false);
        var text = EnsureComments(invocation, sb.ToString());
        VerifyIsolated(path, invocation, text, diagnostics, true);
        return new(text, diagnostics);
    }

    private static IsolatedUnit IsolateMethod(string path, MethodDeclarationSyntax method, string? contextRoot)
    {
        var diagnostics = new List<MigrationDiagnostic>();
        var sb = new StringBuilder("world game\n");
        if (method.Identifier.ValueText == "afterActionExecutedCSharp")
        {
            EmitTriviaComments(method.GetLeadingTrivia(), sb, 0);
            sb.AppendLine("after-action-executed:");
            EmitTriviaComments(method.Body!.OpenBraceToken.TrailingTrivia, sb, 1);
            EmitStatements(method.Body.Statements, sb, diagnostics, 1, false, true, contextRoot);
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
            EmitStatements(method.Body.Statements, sb, diagnostics, 1, false, true, contextRoot);
            EmitTriviaComments(method.Body.CloseBraceToken.LeadingTrivia, sb, 1);
            EmitTriviaComments(method.Body.CloseBraceToken.TrailingTrivia, sb, 1);
            sb.AppendLine("end");
        }
        else
        {
            EmitTriviaComments(AdjacentLeadingComments(method), sb, 0);
            AppendFunctionHeader(sb, method, diagnostics);
            sb.AppendLine(":");
            EmitTriviaComments(method.GetLeadingTrivia(), sb, 0);
            EmitTriviaComments(method.Body!.OpenBraceToken.TrailingTrivia, sb, 1);
            EmitStatements(method.Body.Statements, sb, diagnostics, 1, false, true, contextRoot);
            EmitTriviaComments(method.Body.CloseBraceToken.LeadingTrivia, sb, 1);
            EmitTriviaComments(method.Body.CloseBraceToken.TrailingTrivia, sb, 1);
            sb.AppendLine("end");
        }
        var text = EnsureComments(method, sb.ToString());
        VerifyIsolated(path, method, text, diagnostics, false);
        return new(text, diagnostics);
    }

    private static void AppendFunctionHeader(StringBuilder sb, MethodDeclarationSyntax method, List<MigrationDiagnostic> diagnostics)
    {
        sb.Append("def ").Append(method.Identifier.ValueText);
        foreach (var parameter in method.ParameterList.Parameters)
        {
            sb.Append(' ').Append(parameter.Identifier.ValueText).Append(": ");
            sb.Append(MapType(parameter.Type, method, diagnostics, "parameter"));
        }
        if (method.ReturnType is not PredefinedTypeSyntax { Keyword.RawKind: (int)SyntaxKind.VoidKeyword })
            sb.Append(" ret ").Append(MapType(method.ReturnType, method, diagnostics, "return"));
    }

    private static string MapType(TypeSyntax? type, SyntaxNode source, List<MigrationDiagnostic> diagnostics, string role)
    {
        if (type == null)
        {
            diagnostics.Add(new(MigrationUnitStatus.Unsupported, source.SyntaxTree?.FilePath ?? "<source>", StartLine(source), $"helper {role} type is missing"));
            return "<missing-type>";
        }

        var mapped = type switch
        {
            PredefinedTypeSyntax predefined when predefined.Keyword.IsKind(SyntaxKind.BoolKeyword) => "bool",
            PredefinedTypeSyntax predefined when predefined.Keyword.IsKind(SyntaxKind.IntKeyword) => "int",
            PredefinedTypeSyntax predefined when predefined.Keyword.IsKind(SyntaxKind.StringKeyword) => "string",
            PredefinedTypeSyntax predefined when predefined.Keyword.IsKind(SyntaxKind.DoubleKeyword) => "double",
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.ToString(),
            _ => null
        };

        if (mapped != null) return mapped;

        diagnostics.Add(new(MigrationUnitStatus.Unsupported, source.SyntaxTree?.FilePath ?? "<source>", StartLine(source),
            $"helper {role} type '{type}' is not representable by the SEG type mapping"));
        return type.ToString();
    }

    private static void EmitLocalFunction(LocalFunctionStatementSyntax localFunction, StringBuilder sb, List<MigrationDiagnostic> diagnostics, bool partial, string? contextRoot, IReadOnlyList<LocalFunctionCapture> captures)
    {
        EnsureExactlyOneBlankLine(sb);
        EmitTriviaComments(localFunction.GetLeadingTrivia(), sb, 0);
        sb.Append("def ").Append(localFunction.Identifier.ValueText);
        foreach (var parameter in localFunction.ParameterList.Parameters)
        {
            sb.Append(' ').Append(parameter.Identifier.ValueText).Append(": ");
            sb.Append(MapType(parameter.Type, localFunction, diagnostics, "parameter"));
        }
        foreach (var capture in captures)
        {
            sb.Append(' ').Append(capture.Name).Append(": ");
            sb.Append(MapType(capture.Type, localFunction, diagnostics, "captured local"));
        }
        if (!localFunction.ReturnType.IsKind(SyntaxKind.VoidKeyword))
            sb.Append(" ret ").Append(MapType(localFunction.ReturnType, localFunction, diagnostics, "return"));
        sb.AppendLine(":");
        if (localFunction.Body is BlockSyntax body)
            EmitStatements(body.Statements, sb, diagnostics, 1, partial, true, contextRoot);
        else
            Unsupported(localFunction, diagnostics, "local function expression bodies are not supported", partial, sb, 1);
        sb.AppendLine("end");
    }

    private static IReadOnlyDictionary<string, IReadOnlyList<LocalFunctionCapture>> BuildLocalFunctionCaptures(MethodDeclarationSyntax method)
    {
        var result = new Dictionary<string, IReadOnlyList<LocalFunctionCapture>>(StringComparer.Ordinal);
        foreach (var localFunction in method.Body?.DescendantNodes().OfType<LocalFunctionStatementSyntax>() ?? Enumerable.Empty<LocalFunctionStatementSyntax>())
        {
            var localFunctions = localFunction.Body?.DescendantNodes().OfType<LocalFunctionStatementSyntax>().ToHashSet() ?? new HashSet<LocalFunctionStatementSyntax>();
            var localNames = localFunction.ParameterList.Parameters.Select(x => x.Identifier.ValueText)
                .Concat(localFunction.Body?.DescendantNodes().OfType<VariableDeclaratorSyntax>().Select(x => x.Identifier.ValueText) ?? Enumerable.Empty<string>())
                .ToHashSet(StringComparer.Ordinal);
            var candidates = method.ParameterList.Parameters
                .Select(x => new LocalFunctionCapture(x.Identifier.ValueText, x.Type))
                .Concat(method.Body?.DescendantNodes().OfType<VariableDeclarationSyntax>()
                    .Where(x => x.Ancestors().OfType<LocalFunctionStatementSyntax>().All(x => !localFunctions.Contains(x)))
                    .SelectMany(x => x.Variables.Select(v => new LocalFunctionCapture(v.Identifier.ValueText, x.Type))) ?? Enumerable.Empty<LocalFunctionCapture>())
                .Where(x => !localNames.Contains(x.Name))
                .GroupBy(x => x.Name, StringComparer.Ordinal)
                .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
            var captures = localFunction.Body?.DescendantNodes().OfType<IdentifierNameSyntax>()
                .Where(x => x.Parent is not MemberAccessExpressionSyntax member || member.Name != x)
                .Select(x => x.Identifier.ValueText)
                .Where(candidates.ContainsKey)
                .Distinct(StringComparer.Ordinal)
                .Select(x => candidates[x])
                .ToArray() ?? Array.Empty<LocalFunctionCapture>();
            result[localFunction.Identifier.ValueText] = captures;
        }
        return result;
    }

    private static MethodDeclarationSyntax RewriteLocalFunctionCalls(
        MethodDeclarationSyntax method,
        IReadOnlyDictionary<string, IReadOnlyList<LocalFunctionCapture>> captures)
        => captures.Count == 0
            ? method
            : (MethodDeclarationSyntax)new LocalFunctionCallRewriter(captures).Visit(method)!;

    private sealed class LocalFunctionCallRewriter : CSharpSyntaxRewriter
    {
        private readonly IReadOnlyDictionary<string, IReadOnlyList<LocalFunctionCapture>> captures;

        public LocalFunctionCallRewriter(IReadOnlyDictionary<string, IReadOnlyList<LocalFunctionCapture>> captures)
            => this.captures = captures;

        public override SyntaxNode? VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            var visited = (InvocationExpressionSyntax)base.VisitInvocationExpression(node)!;
            if (visited.Expression is not IdentifierNameSyntax name
                || !captures.TryGetValue(name.Identifier.ValueText, out var parameters)
                || parameters.Count == 0)
                return visited;

            var arguments = visited.ArgumentList.Arguments;
            var appended = parameters.Select(x =>
                SyntaxFactory.Argument(SyntaxFactory.IdentifierName(x.Name))).ToArray();
            return visited.WithArgumentList(
                visited.ArgumentList.WithArguments(arguments.AddRange(appended)));
        }
    }

    private static void VerifyIsolated(string path, SyntaxNode source, string generated, List<MigrationDiagnostic> diagnostics, bool handler)
    {
        var parsed = DslParser.Parse(new DslSource(path + ".generated.seg", generated));
        foreach (var diagnostic in parsed.Diagnostics)
            diagnostics.Add(new(MigrationUnitStatus.Unsupported, path + ".generated.seg", diagnostic.Span.Line, "generated SEG is not parsable: " + diagnostic.Message));
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
            if (method.Identifier.ValueText is "beforeRoomChangeManual" or "beforeRoomChangeSegusum")
            {
                var csharp = MigrationVerifier.ExtractCSharpBeforeRoomChange(path, "class W { " + method.ToFullString() + " }").FirstOrDefault();
                var dsl = MigrationVerifier.ExtractDslBeforeRoomChange(new DslSource(path + ".generated.seg", generated)).FirstOrDefault();
                if (csharp is null || dsl is null)
                    diagnostics.Add(new(MigrationUnitStatus.Unsupported, path, StartLine(method), "before-room-change certification could not extract both representations"));
                else
                {
                    var comparison = MigrationVerifier.CompareBeforeRoomChange(csharp, dsl);
                    if (comparison.Status != EquivalenceStatus.Pass)
                        diagnostics.Add(new(MigrationUnitStatus.Unsupported, path, StartLine(method), "before-room-change semantic round-trip mismatch: " + comparison.Detail));
                }
            }
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
                var comparison = dslCycle is null
                    ? null
                    : MigrationVerifier.CompareCycles(new[] { cycle }, new[] { dslCycle });
                if (comparison is null || comparison.Status != EquivalenceStatus.Pass)
                    diagnostics.Add(new(MigrationUnitStatus.Unsupported, path, cycle.SourceLine,
                        "semantic round-trip mismatch: cycle fingerprint differs" + (comparison?.Detail is null ? "" : ": " + comparison.Detail)));
            }
        }
    }

    private static int StartLine(SyntaxNode node) => node.GetLocation().GetLineSpan().StartLinePosition.Line + 1;

    private static IReadOnlyList<string> CommentInventory(SyntaxNode node)
        => CommentsForNode(node).Select(x => NormalizeComment(x.ToString())).ToArray();

    private static IReadOnlyList<string> CommentInventory(string generated)
        => ExtractGeneratedComments(generated).Select(NormalizeComment).ToArray();

    private static IEnumerable<string> ExtractGeneratedComments(string generated)
    {
        var lines = generated.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var block = new StringBuilder();
        var inBlock = false;
        foreach (var line in lines)
        {
            var text = line.TrimStart();
            if (inBlock)
            {
                block.AppendLine(text);
                if (text.Contains("*/", StringComparison.Ordinal))
                {
                    inBlock = false;
                    yield return block.ToString().TrimEnd('\r', '\n');
                    block.Clear();
                }
                continue;
            }

            var blockStart = text.IndexOf("/*", StringComparison.Ordinal);
            if (blockStart >= 0)
            {
                var tail = text[blockStart..];
                block.Append(tail);
                if (tail.Contains("*/", StringComparison.Ordinal))
                {
                    yield return block.ToString();
                    block.Clear();
                }
                else inBlock = true;
                continue;
            }

            var lineStart = text.IndexOf("//", StringComparison.Ordinal);
            if (lineStart >= 0) yield return text[lineStart..];
        }
    }

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
        var fallback = string.Concat(missing.Select(x => Environment.NewLine + x + Environment.NewLine));
        if (insertion < 0) return generated + fallback;
        return generated.Insert(insertion, fallback);
    }

    private static string NormalizeComment(string comment)
    {
        return comment.Replace("\r\n", "\n").Replace('\r', '\n').Trim();
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

    private static IReadOnlySet<MethodDeclarationSyntax> ReachableHelpers(CompilationUnitSyntax root, string? methodName)
    {
        var allMethods = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(x => x.Body != null).ToArray();
        if (methodName != null && !allMethods.Any(x => x.Identifier.ValueText == methodName))
            return new HashSet<MethodDeclarationSyntax>();

        var methodInvocations = new Dictionary<MethodDeclarationSyntax, InvocationExpressionSyntax[]>();
        InvocationExpressionSyntax[] Invocations(MethodDeclarationSyntax method)
            => methodInvocations.TryGetValue(method, out var cached)
                ? cached
                : methodInvocations[method] = method.Body!.DescendantNodes().OfType<InvocationExpressionSyntax>().ToArray();
        var methods = allMethods
            .Where(x => IsHelperCandidate(x) && !Invocations(x).Any(y => RegistrationKind(y) != null))
            .GroupBy(x => x.Identifier.ValueText, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var work = new Stack<MethodDeclarationSyntax>();
        var registrationCalls = root.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(x => RegistrationKind(x) != null)
            .Where(x => methodName == null || x.Ancestors().OfType<MethodDeclarationSyntax>().Any(m => m.Identifier.ValueText == methodName))
            .ToArray();
        // Build the call-name index once.  The previous implementation walked
        // every registration subtree once per helper candidate, which made a
        // large registration container effectively quadratic (and very
        // memory-intensive) to transpile.
        var registrationCallNames = registrationCalls
            .SelectMany(x => x.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>())
            .Select(CallName)
            .Where(x => x.Length != 0)
            .ToHashSet(StringComparer.Ordinal);
        foreach (var helper in methods.Values)
            if (registrationCallNames.Contains(helper.Identifier.ValueText)) work.Push(helper);
        foreach (var special in root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                     .Where(x => x.Identifier.ValueText is "afterActionExecutedCSharp" or "beforeRoomChangeManual" or "beforeRoomChangeSegusum"))
            foreach (var invocation in Invocations(special))
                if (methods.TryGetValue(CallName(invocation), out var helper)) work.Push(helper);
        var reachable = new HashSet<MethodDeclarationSyntax>();
        while (work.Count != 0)
        {
            var helper = work.Pop();
            if (!reachable.Add(helper)) continue;
            foreach (var invocation in Invocations(helper))
                if (methods.TryGetValue(CallName(invocation), out var dependency)) work.Push(dependency);
        }
        return reachable;
    }

    private static void EmitHandler(InvocationExpressionSyntax invocation, StringBuilder sb, List<MigrationDiagnostic> diagnostics, bool partial, string? contextRoot = null)
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
        var phrase = kind == "combine"
            ? args.FirstOrDefault(x => x.NameColon?.Name.Identifier.ValueText == "fullSentenceUntransl")?.Expression
                ?? (args.ElementAtOrDefault(2) is { NameColon: null } positionalPhrase ? positionalPhrase.Expression : null)
            : null;
        if (phrase is not null && phrase is not AnonymousFunctionExpressionSyntax) sb.Append("  phrase ").AppendLine(LiteralOrExpression(phrase));
        var explanation = args.FirstOrDefault(x => x.NameColon?.Name.Identifier.ValueText == "explanation")?.Expression ?? (kind == "use-for" ? args.ElementAtOrDefault(2)?.Expression : null);
        if (explanation is not null && explanation is not AnonymousFunctionExpressionSyntax) sb.Append("  exp ").AppendLine(LiteralOrExpression(explanation));
        var possible = args.FirstOrDefault(x => x.NameColon?.Name.Identifier.ValueText == "isPossibleNow")?.Expression;
        if (possible is AnonymousFunctionExpressionSyntax lambda)
        {
            if (lambda.Body is BlockSyntax) Unsupported(lambda, diagnostics, "block-bodied possible-when", partial, sb, 1);
            else if (lambda.Body is ExpressionSyntax body)
            {
                sb.AppendLine(FormatCondition(body, Indent(1) + "possible-when ", Indent(2), ""));
                EnsureOneBlankLineAfterHeader(sb);
            }
        }
        var handler = args.Select(x => x.Expression).OfType<AnonymousFunctionExpressionSyntax>().LastOrDefault();
        if (handler?.Body is BlockSyntax block)
        {
            // Handler input has a canonical SEG name.  C# callers are free to
            // choose any lambda parameter name (often `i`), but the generated
            // C# handler lambda uses `i` only for room-changed and `e` for all
            // other handlers.  Normalize references structurally here so the
            // emitted SEG does not depend on the incidental C# parameter name.
            var parameterName = handler switch
            {
                SimpleLambdaExpressionSyntax simple => simple.Parameter.Identifier.ValueText,
                ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters.FirstOrDefault()?.Identifier.ValueText,
                _ => null
            };
            var canonicalName = kind == "room-changed" ? "i" : "e";
            if (!string.IsNullOrEmpty(parameterName) && !string.Equals(parameterName, canonicalName, StringComparison.Ordinal))
                block = (BlockSyntax)new HandlerParameterRewriter(parameterName!, canonicalName).Visit(block)!;
            EmitTriviaComments(block.OpenBraceToken.TrailingTrivia, sb, 1);
            EmitStatements(block.Statements, sb, diagnostics, 1, partial, true, contextRoot);
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

    private static void EmitStatements(IEnumerable<StatementSyntax> statements, StringBuilder sb, List<MigrationDiagnostic> diagnostics, int level, bool partial, bool includeComments = true, string? contextRoot = null)
    {
        var statementArray = statements.ToArray();
        for (var statementIndex = 0; statementIndex < statementArray.Length; statementIndex++)
        {
            var statement = statementArray[statementIndex];
            try
            {
            if (includeComments) EmitComments(statement, sb, level);
            var indent = Indent(level);
            switch (statement)
            {
                case IfStatementSyntax x:
                    sb.AppendLine(FormatCondition(x.Condition, indent + "if ", Indent(level + 1), ":"));
                    EnsureOneBlankLineAfterHeader(sb);
                    EmitStatements(x.Statement is BlockSyntax b ? b.Statements : new[] { x.Statement }, sb, diagnostics, level + 1, partial, includeComments, contextRoot);
                    var e = x.Else;
                    while (e?.Statement is IfStatementSyntax elif)
                    { sb.AppendLine(FormatCondition(elif.Condition, indent + "elif ", Indent(level + 1), ":")); EnsureOneBlankLineAfterHeader(sb); EmitStatements(elif.Statement is BlockSyntax eb ? eb.Statements : new[] { elif.Statement }, sb, diagnostics, level + 1, partial, includeComments, contextRoot); e = elif.Else; }
                    if (e != null) { sb.Append(indent).AppendLine("else:"); EnsureOneBlankLineAfterHeader(sb); EmitStatements(e.Statement is BlockSyntax eb ? eb.Statements : new[] { e.Statement }, sb, diagnostics, level + 1, partial, includeComments, contextRoot); }
                    sb.Append(indent).AppendLine("end"); break;
                case ExpressionStatementSyntax x when x.Expression is AssignmentExpressionSyntax a:
                    if (a.Right is InvocationExpressionSyntax add && CallName(add) == "addToCycle") EmitCycleChain(add, sb, diagnostics, level, partial, true, contextRoot);
                    else if (a.Left.ToString().EndsWith("makesNoSenseAtThisTime", StringComparison.Ordinal) && a.Right.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.TrueLiteralExpression)) sb.Append(indent).AppendLine("makes-no-sense");
                    else if (a.Left.ToString().EndsWith("textInputToShow", StringComparison.Ordinal)) sb.Append(indent).Append("text-input ").AppendLine(Expression(a.Right));
                    else AppendFormattedExpression(sb, indent + Expression(a.Left) + " " + a.OperatorToken.Text + " ", a.Right, level); break;
                case ExpressionStatementSyntax x when x.Expression is InvocationExpressionSyntax i:
                    EmitInvocation(i, sb, diagnostics, level, partial, IsConsecutiveAdd(statementArray, statementIndex), contextRoot); break;
                case ExpressionStatementSyntax x when x.Expression is PostfixUnaryExpressionSyntax p && p.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PostIncrementExpression):
                    sb.Append(indent).Append(Expression(p.Operand)).AppendLine("++"); break;
                case ExpressionStatementSyntax x when x.Expression is PrefixUnaryExpressionSyntax p && p.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PreIncrementExpression):
                    sb.Append(indent).Append(Expression(p.Operand)).AppendLine("++"); break;
                case ExpressionStatementSyntax x:
                    try { AppendFormattedExpression(sb, indent, x.Expression, level); }
                    catch (InvalidOperationException ex) { Unsupported(x.Expression, diagnostics, ex.Message, partial, sb, level); }
                    break;
                case BlockSyntax x:
                    EmitStatements(x.Statements, sb, diagnostics, level, partial, includeComments, contextRoot); break;
                case LocalDeclarationStatementSyntax x:
                    foreach (var v in x.Declaration.Variables)
                    {
                        try
                        {
                            if (v.Initializer?.Value is ObjectCreationExpressionSyntax creation
                                && creation.Type.ToString() == "Cycle"
                                && creation.ArgumentList.Arguments.Count == 0
                                && creation.Initializer is null)
                                sb.Append(indent).Append("var ").Append(v.Identifier.ValueText).AppendLine(" = new-cycle");
                            else if (v.Initializer?.Value is InvocationExpressionSyntax start && FindStartCycle(start) != null)
                                EmitCycle(start, v.Identifier.ValueText, sb, diagnostics, level, partial, contextRoot);
                            else if (v.Initializer != null) AppendFormattedExpression(sb, indent + "var " + v.Identifier.ValueText + " = ", v.Initializer.Value, level);
                            else
                            {
                                var type = MapCSharpType(x.Declaration.Type, diagnostics, x);
                                if (type == null) throw new InvalidOperationException($"unsupported local variable type '{x.Declaration.Type}'");
                                sb.Append(indent).Append("var ").Append(v.Identifier.ValueText).Append(": ").Append(type).Append(" = ").Append(DefaultLocalValue(x.Declaration.Type, type)).AppendLine();
                            }
                        }
                        catch (InvalidOperationException ex) { Unsupported(v.Initializer ?? (SyntaxNode)v, diagnostics, ex.Message, partial, sb, level); }
                    }
                    break;
                case UsingStatementSyntax x when x.Expression is InvocationExpressionSyntax call && CallName(call) == "namedCutScene":
                    EmitNamedCutscene(call, x.Statement, sb, diagnostics, level, partial, contextRoot); break;
                case ReturnStatementSyntax x when x.Expression is InvocationExpressionSyntax cycleReturn && FindStartCycle(cycleReturn) != null:
                    EmitCycle(cycleReturn, "cyc", sb, diagnostics, level, partial, contextRoot);
                    sb.Append(indent).AppendLine("ret cyc");
                    break;
                case EmptyStatementSyntax: break;
                case LocalFunctionStatementSyntax: break;
                case ReturnStatementSyntax x:
                    if (x.Expression != null) AppendFormattedExpression(sb, indent + "ret ", x.Expression, level);
                    else sb.Append(indent).AppendLine("ret");
                    break;
                default: Unsupported(statement, diagnostics, "unsupported statement " + statement.Kind(), partial, sb, level); break;
            }
            foreach (var t in includeComments ? statement.GetTrailingTrivia().Where(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineCommentTrivia) || x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineCommentTrivia)) : Enumerable.Empty<SyntaxTrivia>())
                sb.Append(indent).Append("// ").AppendLine(t.ToString().TrimStart('/').Trim());
            if (statement is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax dialogue }
                && CallName(dialogue) == "dial")
                EnsureOneBlankLineAfterDialogue(sb);
            }
            catch (InvalidOperationException ex) { Unsupported(statement, diagnostics, ex.Message, partial, sb, level); }
        }
    }

    private static string? MapCSharpType(TypeSyntax type, List<MigrationDiagnostic> diagnostics, SyntaxNode source)
    {
        var mapped = type switch
        {
            PredefinedTypeSyntax predefined when predefined.Keyword.IsKind(SyntaxKind.BoolKeyword) => "bool",
            PredefinedTypeSyntax predefined when predefined.Keyword.IsKind(SyntaxKind.IntKeyword) => "int",
            PredefinedTypeSyntax predefined when predefined.Keyword.IsKind(SyntaxKind.StringKeyword) => "string",
            PredefinedTypeSyntax predefined when predefined.Keyword.IsKind(SyntaxKind.DoubleKeyword) => "double",
            PredefinedTypeSyntax predefined when predefined.Keyword.IsKind(SyntaxKind.FloatKeyword) => "float",
            PredefinedTypeSyntax predefined when predefined.Keyword.IsKind(SyntaxKind.ObjectKeyword) => "object",
            IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
            QualifiedNameSyntax qualified => qualified.ToString(),
            _ => null
        };
        if (mapped != null) return mapped;
        diagnostics.Add(new(MigrationUnitStatus.Unsupported, source.SyntaxTree?.FilePath ?? "<source>", StartLine(source),
            $"local variable type '{type}' is not representable by the SEG type mapping"));
        return null;
    }

    private static string DefaultLocalValue(TypeSyntax sourceType, string mappedType)
    {
        if (sourceType is NullableTypeSyntax || mappedType.EndsWith("?", StringComparison.Ordinal)) return "null";
        if (sourceType is PredefinedTypeSyntax predefined)
        {
            if (predefined.Keyword.IsKind(SyntaxKind.BoolKeyword)) return "false";
            if (predefined.Keyword.IsKind(SyntaxKind.IntKeyword)) return "0";
            if (predefined.Keyword.IsKind(SyntaxKind.DoubleKeyword)) return "0.0";
            if (predefined.Keyword.IsKind(SyntaxKind.FloatKeyword)) return "0.0";
        }
        // Nominal types, strings, arrays and generic collections are reference
        // types in the migration corpus and retain C#'s null default.
        return "null";
    }

    private static void EmitInvocation(InvocationExpressionSyntax invocation, StringBuilder sb, List<MigrationDiagnostic> diagnostics, int level, bool partial, bool omitConsecutiveCycleEnd = false, string? contextRoot = null)
    {
        var indent = Indent(level); var name = invocation.Expression.ToString();
        if (CallName(invocation) == "addToCycle") { EmitCycleChain(invocation, sb, diagnostics, level, partial, !omitConsecutiveCycleEnd, contextRoot); return; }
        if (CallName(invocation) == "execNextInCycle") { sb.Append(indent).Append("next ").AppendLine(Arg(invocation.ArgumentList.Arguments, 0)); return; }
        if (CallName(invocation) == "startCycle") { Unsupported(invocation, diagnostics, "startCycle must be assigned to a cycle variable", partial, sb, level); return; }
        if (name is "dial" or "nar" or "narText" or "narRoom" or "narImg")
        {
            if (name == "dial")
            {
                EnsureOneBlankLineBeforeDialogue(sb);
                sb.Append(indent).Append(Arg(invocation.ArgumentList.Arguments, 0)).Append(": ").AppendLine(DialogueText(invocation.ArgumentList.Arguments.ElementAtOrDefault(1)?.Expression));
            }
            else if (name is "nar" or "narText")
            {
                EnsureOneBlankLineBeforeDialogue(sb);
                sb.Append(indent).Append("nar: ").AppendLine(DialogueText(invocation.ArgumentList.Arguments.LastOrDefault()?.Expression));
                EnsureOneBlankLineAfterDialogue(sb);
            }
            else
            {
                EnsureOneBlankLineBeforeDialogue(sb);
                sb.Append(indent).AppendLine(Expression(invocation));
                EnsureOneBlankLineAfterDialogue(sb);
            }
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

    private static void EmitCycle(InvocationExpressionSyntax initializer, string variable, StringBuilder sb, List<MigrationDiagnostic> diagnostics, int level, bool partial, string? contextRoot = null)
    {
        var start = FindStartCycle(initializer)!;
        var adds = initializer.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
            .Where(x => CallName(x) == "addToCycle").OrderBy(x => x.Span.End).ToArray();
        sb.Append(Indent(level)).Append("var ").Append(variable).AppendLine(" = new-cycle");
        EmitCycleElementCore(variable, start.ArgumentList.Arguments, start, sb, diagnostics, level, partial, true, adds.Length == 0, contextRoot);
        for (var i = 0; i < adds.Length; i++)
            EmitCycleElementCore(variable, adds[i].ArgumentList.Arguments, adds[i], sb, diagnostics, level, partial, false, i == adds.Length - 1, contextRoot);
    }

    private static void EmitCycleElement(InvocationExpressionSyntax invocation, StringBuilder sb, List<MigrationDiagnostic> diagnostics, int level, bool partial, string? contextRoot = null)
        => EmitCycleElementCore(invocation.Expression is MemberAccessExpressionSyntax member ? Expression(member.Expression) : "cyc", invocation.ArgumentList.Arguments, invocation, sb, diagnostics, level, partial, false, true, contextRoot);

    private static void EmitCycleChain(InvocationExpressionSyntax outer, StringBuilder sb, List<MigrationDiagnostic> diagnostics, int level, bool partial, bool emitFinalEnd = true, string? contextRoot = null)
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
        var ordered = elements.AsEnumerable().Reverse().ToArray();
        for (var i = 0; i < ordered.Length; i++)
            EmitCycleElementCore(cycle, ordered[i].ArgumentList.Arguments, ordered[i], sb, diagnostics, level, partial, false, emitFinalEnd && i == ordered.Length - 1, contextRoot);
    }

    private static bool IsConsecutiveAdd(IReadOnlyList<StatementSyntax> statements, int index)
        => index + 1 < statements.Count
            && statements[index + 1] is ExpressionStatementSyntax { Expression: InvocationExpressionSyntax next }
            && CallName(next) == "addToCycle";

    private static void EmitCycleElementCore(string cycle, SeparatedSyntaxList<ArgumentSyntax> args, InvocationExpressionSyntax invocation, StringBuilder sb, List<MigrationDiagnostic> diagnostics, int level, bool partial, bool start, bool emitEnd = true, string? contextRoot = null)
    {
        var indent = Indent(level);
        var id = Arg(args, 0);
        var important = EnumValue(args, "Importance", "Important");
        var repeat = EnumValue(args, "Repeat", "OnlyOnce", "Forever");
        var lambdas = args.Select(x => x.Expression).OfType<AnonymousFunctionExpressionSyntax>().ToArray();
        var predicateSyntax = lambdas.Length > 1 ? CycleExpressionSyntax(lambdas[0]) : null;
        var predicate = predicateSyntax is null ? null : Expression(predicateSyntax);
        var body = lambdas.Length == 0 ? null : lambdas[^1].Body as BlockSyntax;
        sb.Append(indent).Append("add ").Append(cycle).Append(' ').Append(id);
        if (important == "Important") sb.Append(" important");
        if (repeat == "OnlyOnce") sb.Append(" once"); else if (repeat == "Forever") sb.Append(" forever");
        if (predicate != null)
        {
            sb.AppendLine().AppendLine(FormatConditionText(Indent(level + 1) + "when ", Indent(level + 2), predicate, "", predicateSyntax));
            EnsureOneBlankLineAfterHeader(sb);
        }
        else sb.AppendLine();
        if (body != null) EmitStatements(body.Statements, sb, diagnostics, level + 1, partial, true, contextRoot);
        if (emitEnd) sb.Append(indent).AppendLine("end");
        if (important?.StartsWith("unsupported:", StringComparison.Ordinal) == true || repeat?.StartsWith("unsupported:", StringComparison.Ordinal) == true)
            Unsupported(invocation, diagnostics, "unsupported cycle metadata", partial, sb, level);
    }

    private static void EmitNamedCutscene(InvocationExpressionSyntax invocation, StatementSyntax statement, StringBuilder sb, List<MigrationDiagnostic> diagnostics, int level, bool partial, string? contextRoot = null)
    {
        var indent = Indent(level);
        var id = Arg(invocation.ArgumentList.Arguments, 0);
        var title = NamedCutsceneTitle(invocation, id, contextRoot, out var conflict, out var invalid);
        if (conflict)
            Unsupported(invocation, diagnostics, $"NamedCutSceneId title conflict for '{id}' in migration context", partial, sb, level);
        else if (invalid)
            Unsupported(invocation, diagnostics, $"NamedCutSceneId declaration for '{id}' has no resolvable title", partial, sb, level);
        else if (title is null)
            Unsupported(invocation, diagnostics, "NamedCutSceneId title cannot be resolved structurally", partial, sb, level);
        sb.Append(indent).Append("named-cutscene ").Append(id).Append(' ').Append(title ?? "\"\"");
        foreach (var arg in invocation.ArgumentList.Arguments.Skip(1)) sb.Append(' ').Append(Expression(arg.Expression));
        sb.AppendLine(":");
        if (statement is BlockSyntax block) EmitStatements(block.Statements, sb, diagnostics, level + 1, partial, true, contextRoot);
        sb.Append(indent).AppendLine("end");
    }

    private static string? NamedCutsceneTitle(InvocationExpressionSyntax invocation, string id, string? contextRoot, out bool conflict, out bool invalid)
    {
        conflict = false;
        invalid = false;
        var sourcePath = invocation.SyntaxTree.FilePath;
        var directory = !string.IsNullOrWhiteSpace(contextRoot)
            ? Path.GetFullPath(contextRoot)
            : string.IsNullOrEmpty(sourcePath) ? "" : Path.GetDirectoryName(sourcePath) ?? "";
        if (directory.Length == 0)
        {
            var local = FindNamedCutsceneContext(invocation.SyntaxTree.GetRoot(), id);
            return ResolveNamedCutsceneContext(local, ref conflict, ref invalid);
        }
        var cacheKey = directory + (string.IsNullOrWhiteSpace(contextRoot) ? "|top-directory" : "|all-directories");
        IReadOnlyDictionary<string, NamedCutsceneContextValue> index;
        lock (NamedCutsceneIndexLock)
        {
            if (!NamedCutsceneIndexCache.TryGetValue(cacheKey, out index!))
            {
                var map = new Dictionary<string, NamedCutsceneContextValue>(StringComparer.Ordinal);
                static void Add(Dictionary<string, NamedCutsceneContextValue> target, string id, string? title, bool isCSharp)
                {
                    if (!target.TryGetValue(id, out var previous))
                    {
                        target[id] = isCSharp
                            ? new(true, title, false, null, false)
                            : new(false, null, true, title, false);
                        return;
                    }
                    if (isCSharp)
                    {
                        if (previous.HasCSharpDeclaration && !string.Equals(previous.CSharpTitle, title, StringComparison.Ordinal))
                            target[id] = previous with { Conflict = true };
                        else
                            target[id] = previous with { HasCSharpDeclaration = true, CSharpTitle = title };
                    }
                    else
                    {
                        if (previous.HasSegDeclaration && !string.Equals(previous.SegTitle, title, StringComparison.Ordinal))
                            target[id] = previous with { Conflict = true };
                        else
                            target[id] = previous with { HasSegDeclaration = true, SegTitle = title };
                    }
                }
                var searchOption = !string.IsNullOrWhiteSpace(contextRoot) ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
                foreach (var candidatePath in Directory.EnumerateFiles(directory, "*.cs", searchOption))
                {
                    try
                    {
                        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(candidatePath), path: candidatePath).GetRoot();
                        foreach (var declaration in FindNamedCutsceneDeclarations(root))
                        {
                            var identifier = DeclarationIdentifier(declaration);
                            if (identifier is null) continue;
                            var title = NamedCutsceneTitleFromDeclaration(declaration);
                            Add(map, identifier, title, isCSharp: true);
                        }
                    }
                    catch (IOException) { }
                }
                foreach (var candidatePath in Directory.EnumerateFiles(directory, "*.seg", searchOption))
                {
                    try
                    {
                        var source = new DslSource(candidatePath, File.ReadAllText(candidatePath));
                        var parsed = DslParser.Parse(source);
                        foreach (var named in FindNamedCutscenes(parsed.Document.Declarations))
                            Add(map, named.Id, DslNamedCutsceneTitle(named), isCSharp: false);
                    }
                    catch (IOException) { }
                }
                NamedCutsceneIndexCache[cacheKey] = index = map;
            }
        }
        if (index.TryGetValue(id, out var result))
        {
            return ResolveNamedCutsceneContext(result, ref conflict, ref invalid);
        }
        var fallback = FindNamedCutsceneContext(invocation.SyntaxTree.GetRoot(), id);
        return ResolveNamedCutsceneContext(fallback, ref conflict, ref invalid);
    }

    private static IEnumerable<NamedCutsceneStatement> FindNamedCutscenes(IEnumerable<DslDeclaration> declarations)
        => declarations.SelectMany(declaration => declaration switch
        {
            HandlerDeclaration handler => FindNamedCutscenes(handler.Body),
            FunctionDeclaration function => FindNamedCutscenes(function.Body),
            BeforeRoomChangeDeclaration before => FindNamedCutscenes(before.Body),
            AfterActionExecutedDeclaration after => FindNamedCutscenes(after.Body),
            CycleElementDeclaration cycle => FindNamedCutscenes(cycle.Body),
            _ => Enumerable.Empty<NamedCutsceneStatement>()
        });

    private static IEnumerable<NamedCutsceneStatement> FindNamedCutscenes(IEnumerable<DslStatement> statements)
        => statements.SelectMany(statement => statement switch
        {
            NamedCutsceneStatement named => new[] { named }.Concat(FindNamedCutscenes(named.Body)),
            IfStatement conditional => conditional.Branches.SelectMany(x => FindNamedCutscenes(x.Body)).Concat(conditional.ElseBody is null ? Enumerable.Empty<NamedCutsceneStatement>() : FindNamedCutscenes(conditional.ElseBody)),
            AddCycleElementStatement add => FindNamedCutscenes(add.Body),
            _ => Enumerable.Empty<NamedCutsceneStatement>()
        });

    private static string? DslNamedCutsceneTitle(NamedCutsceneStatement statement)
        => statement.Title is LiteralExpression literal && literal.Kind is "string" or "raw-string" ? literal.Value : null;

    private static IEnumerable<SyntaxNode> FindNamedCutsceneDeclarations(SyntaxNode root)
    {
        foreach (var variable in root.DescendantNodes().OfType<VariableDeclaratorSyntax>())
        {
            if (variable.Initializer is null) continue;
            if (variable.Parent is VariableDeclarationSyntax declaration
                && IsNamedCutsceneType(declaration.Type)
                && IsNamedCutsceneCreation(variable.Initializer.Value, declaration.Type))
                yield return variable;
        }

        foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            if (property.Initializer is not null
                && IsNamedCutsceneType(property.Type)
                && IsNamedCutsceneCreation(property.Initializer.Value, property.Type))
                yield return property;
        }

        foreach (var assignment in root.DescendantNodes().OfType<AssignmentExpressionSyntax>())
        {
            if (assignment.Left is IdentifierNameSyntax
                && IsNamedCutsceneCreation(assignment.Right, null))
                yield return assignment;
        }
    }

    private static NamedCutsceneContextValue? FindNamedCutsceneContext(SyntaxNode root, string id)
    {
        var declaration = FindNamedCutsceneDeclarations(root)
            .FirstOrDefault(x => DeclarationIdentifier(x) == id);
        return declaration is null
            ? null
            : new(true, NamedCutsceneTitleFromDeclaration(declaration), false, null, false);
    }

    private static string? ResolveNamedCutsceneContext(NamedCutsceneContextValue? value, ref bool conflict, ref bool invalid)
    {
        if (value is null) return null;
        if (value.Conflict)
        {
            conflict = true;
            return null;
        }
        if ((value.HasCSharpDeclaration && value.CSharpTitle is null)
            || (value.HasSegDeclaration && value.SegTitle is null))
        {
            invalid = true;
            return null;
        }
        if (value.CSharpTitle is not null && value.SegTitle is not null
            && !string.Equals(value.CSharpTitle, value.SegTitle, StringComparison.Ordinal))
        {
            conflict = true;
            return null;
        }
        return value.CSharpTitle ?? value.SegTitle;
    }

    private static string? DeclarationIdentifier(SyntaxNode declaration) => declaration switch
    {
        VariableDeclaratorSyntax variable => variable.Identifier.ValueText,
        PropertyDeclarationSyntax property => property.Identifier.ValueText,
        AssignmentExpressionSyntax assignment when assignment.Left is IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
        _ => null
    };

    private static ExpressionSyntax? DeclarationInitializer(SyntaxNode declaration) => declaration switch
    {
        VariableDeclaratorSyntax variable => variable.Initializer?.Value,
        PropertyDeclarationSyntax property => property.Initializer?.Value,
        AssignmentExpressionSyntax assignment => assignment.Right,
        _ => null
    };

    private static string? NamedCutsceneTitleFromDeclaration(SyntaxNode declaration)
    {
        var initializer = DeclarationInitializer(declaration);
        var objectInitializer = initializer switch
        {
            ObjectCreationExpressionSyntax creation => creation.Initializer,
            ImplicitObjectCreationExpressionSyntax creation => creation.Initializer,
            _ => null
        };
        var title = objectInitializer?.Expressions.OfType<AssignmentExpressionSyntax>()
            .FirstOrDefault(x => x.Left is IdentifierNameSyntax identifier
                && identifier.Identifier.ValueText == "titleUntranslated")?.Right;
        return NamedCutsceneTitleLiteral(title);
    }

    private static bool IsNamedCutsceneType(TypeSyntax? type) =>
        type is not null && type.ToString().EndsWith("NamedCutSceneId", StringComparison.Ordinal);

    private static bool IsNamedCutsceneCreation(ExpressionSyntax expression, TypeSyntax? declaredType)
    {
        return expression switch
        {
            ObjectCreationExpressionSyntax creation => IsNamedCutsceneType(creation.Type)
                || (declaredType is not null && IsNamedCutsceneType(declaredType)),
            ImplicitObjectCreationExpressionSyntax => declaredType is not null && IsNamedCutsceneType(declaredType),
            _ => false
        };
    }

    private static string? NamedCutsceneTitleLiteral(ExpressionSyntax? expression)
    {
        if (expression is InvocationExpressionSyntax invocation)
        {
            if (invocation.Expression is MemberAccessExpressionSyntax member
                && member.Name.Identifier.ValueText == "translatable"
                && member.Expression is LiteralExpressionSyntax extensionLiteral
                && extensionLiteral.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression))
                return extensionLiteral.Token.Text;

            var argument = invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression;
            if (invocation.Expression is IdentifierNameSyntax identifier
                && identifier.Identifier.ValueText == "translatable"
                && argument is LiteralExpressionSyntax directLiteral
                && directLiteral.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression))
                return directLiteral.Token.Text;
        }
        return expression is LiteralExpressionSyntax literal
            && literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression)
            ? literal.Token.Text
            : null;
    }

    private static string? EnumValue(SeparatedSyntaxList<ArgumentSyntax> args, string type, params string[] known)
    {
        var member = args.Select(x => x.Expression).OfType<MemberAccessExpressionSyntax>().FirstOrDefault(x => x.Expression.ToString() == type);
        if (member is null) return null;
        return known.Contains(member.Name.Identifier.ValueText, StringComparer.Ordinal) ? member.Name.Identifier.ValueText : "unsupported:" + type + "." + member.Name.Identifier.ValueText;
    }

    private static string CycleExpression(AnonymousFunctionExpressionSyntax lambda)
        => Expression(CycleExpressionSyntax(lambda));

    private static ExpressionSyntax CycleExpressionSyntax(AnonymousFunctionExpressionSyntax lambda)
    {
        var parameter = lambda switch { SimpleLambdaExpressionSyntax x => x.Parameter.Identifier.ValueText, ParenthesizedLambdaExpressionSyntax x => x.ParameterList.Parameters.FirstOrDefault()?.Identifier.ValueText, _ => null };
        var body = string.IsNullOrEmpty(parameter) ? lambda.Body : new CycleParameterRewriter(parameter!).Visit(lambda.Body);
        return body as ExpressionSyntax ?? throw new InvalidOperationException("Unsupported cycle predicate");
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

    private sealed class HandlerParameterRewriter : CSharpSyntaxRewriter
    {
        private readonly string parameter;
        private readonly string canonicalName;

        public HandlerParameterRewriter(string parameter, string canonicalName)
        {
            this.parameter = parameter;
            this.canonicalName = canonicalName;
        }

        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            // Do not rewrite a member name (`obj.parameter`) or a declaration
            // of a nested local that shadows the handler parameter.
            if (node.Identifier.ValueText == parameter
                && !(node.Parent is MemberAccessExpressionSyntax member && member.Name == node))
                return SyntaxFactory.IdentifierName(canonicalName).WithTriviaFrom(node);
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
    {
        foreach (var t in trivia.Where(IsComment))
        {
            var raw = t.ToString().Replace("\r\n", "\n").Replace('\r', '\n');
            foreach (var line in raw.Split('\n'))
                sb.Append(Indent(level)).AppendLine(line);
        }
    }
    private static void Unsupported(SyntaxNode node, List<MigrationDiagnostic> diagnostics, string reason, bool partial, StringBuilder sb, int level)
    { var line = node.GetLocation().GetLineSpan().StartLinePosition.Line + 1; diagnostics.Add(new(MigrationUnitStatus.Unsupported, node.SyntaxTree?.FilePath ?? "", line, reason, node.ToString())); if (partial) { var i = Indent(level); sb.Append(i).AppendLine("// C2SEG-MANUAL-BEGIN").Append(i).Append("// source: ").AppendLine((node.SyntaxTree?.FilePath ?? "") + ":" + line).Append(i).Append("// reason: ").AppendLine(reason); foreach (var l in node.ToString().Split('\n')) sb.Append(i).Append("// ").AppendLine(l); sb.Append(i).AppendLine("// C2SEG-MANUAL-END"); } }
    private static string Expression(SyntaxNode? node)
    {
        if (node is null) return "";
        return node switch
        {
            IdentifierNameSyntax x => x.Identifier.ValueText,
            ThisExpressionSyntax => "this",
            LiteralExpressionSyntax x => EmitLiteral(x),
            ParenthesizedExpressionSyntax x => "(" + Expression(x.Expression) + ")",
            PrefixUnaryExpressionSyntax x when x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalNotExpression) => "not " + Expression(x.Operand),
            PrefixUnaryExpressionSyntax x when x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.UnaryMinusExpression) => "-" + Expression(x.Operand),
            PrefixUnaryExpressionSyntax x when x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.UnaryPlusExpression) => "+" + Expression(x.Operand),
            BinaryExpressionSyntax x when TryEmitRandomModulo(x, out var random) => random,
            BinaryExpressionSyntax x => Expression(x.Left) + " " + BinaryOperator(x.Kind()) + " " + Expression(x.Right),
            InvocationExpressionSyntax x when TryEmitListComprehension(x, out var comprehension) => comprehension,
            MemberAccessExpressionSyntax x => Expression(x.Expression) + "." + x.Name.Identifier.ValueText,
            InvocationExpressionSyntax x => EmitCallExpression(x),
            CollectionExpressionSyntax x => "[" + string.Join(", ", x.Elements.Select(EmitCollectionElement)) + "]",
            ArrayCreationExpressionSyntax x when x.Initializer is not null => EmitArrayInitializer(x.Initializer),
            ImplicitArrayCreationExpressionSyntax x => EmitArrayInitializer(x.Initializer),
            ConditionalExpressionSyntax x => "if " + Expression(x.Condition) + " then " + Expression(x.WhenTrue) + " else " + Expression(x.WhenFalse),
            ArgumentSyntax x => (x.NameColon is null ? "" : x.NameColon.Name.Identifier.ValueText + ": ") + Expression(x.Expression),
            _ => throw new InvalidOperationException("Unsupported C# expression: " + node.Kind())
        };
    }

    private static bool TryEmitListComprehension(InvocationExpressionSyntax invocation, out string result)
    {
        result = "";
        InvocationExpressionSyntax source = invocation;
        var materialized = false;
        if ((CallName(source) == "ToList" || CallName(source) == "ToArray") && source.ArgumentList.Arguments.Count == 0
            && source.Expression is MemberAccessExpressionSyntax toListMember
            && toListMember.Expression is InvocationExpressionSyntax nested)
        {
            source = nested;
            materialized = true;
        }

        if (!materialized) return false;

        if (CallName(source) != "Where" || source.ArgumentList.Arguments.Count != 1
            || source.Expression is not MemberAccessExpressionSyntax whereMember
            || whereMember.Expression is not ExpressionSyntax collection
            || source.ArgumentList.Arguments[0].Expression is not SimpleLambdaExpressionSyntax predicateLambda
            || predicateLambda.Body is not ExpressionSyntax predicate)
            return false;

        var item = predicateLambda.Parameter.Identifier.ValueText;
        result = "[from " + Expression(collection) + " " + item + " where " + Expression(predicate) + " select " + item + "]";
        return true;
    }

    private static void AppendFormattedExpression(StringBuilder sb, string prefix, ExpressionSyntax expression, int level, bool appendNewline = true)
    {
        var rendered = Expression(expression);
        var clauses = new List<(string? Operator, ExpressionSyntax Expression)>();
        CollectLogicalClauses(expression, clauses);
        var multiline = clauses.Count >= 3 || rendered.Length > 100;
        if (!multiline || clauses.Count == 0)
        {
            sb.Append(prefix).Append(rendered);
            if (appendNewline) sb.AppendLine();
            return;
        }

        sb.Append(prefix).Append(Expression(clauses[0].Expression));
        foreach (var clause in clauses.Skip(1))
            sb.AppendLine().Append(Indent(level + 1)).Append(clause.Operator).Append(' ').Append(Expression(clause.Expression));
        sb.AppendLine();
    }

    private static void CollectLogicalClauses(ExpressionSyntax expression, List<(string? Operator, ExpressionSyntax Expression)> clauses, string? incoming = null)
    {
        if (expression is BinaryExpressionSyntax binary
            && (binary.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalAndExpression)
                || binary.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalOrExpression)))
        {
            CollectLogicalClauses(binary.Left, clauses, incoming);
            CollectLogicalClauses(binary.Right, clauses, binary.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalAndExpression) ? "and" : "or");
            return;
        }
        clauses.Add((incoming, expression));
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

    private static string EmitArrayInitializer(InitializerExpressionSyntax initializer)
        => "[" + string.Join(", ", initializer.Expressions.Select(Expression)) + "]";

    private static string EmitCallExpression(InvocationExpressionSyntax invocation)
    {
        var name = invocation.Expression is MemberAccessExpressionSyntax member ? member.Name.Identifier.ValueText : invocation.Expression.ToString();
        if (name == "translatable"
            && invocation.ArgumentList.Arguments.Count == 0
            && invocation.Expression is MemberAccessExpressionSyntax translatableMember
            && translatableMember.Expression is LiteralExpressionSyntax translatableLiteral
            && translatableLiteral.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression))
            return EmitLiteral(translatableLiteral);
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
        var callArguments = invocation.ArgumentList.Arguments.Select(ExpressionForCallArgument).ToList();
        return callArguments.Count == 0
            ? receiver
            : receiver + " " + string.Join(" ", callArguments);
    }

    private static string ExpressionForCallArgument(ArgumentSyntax argument)
    {
        var expression = argument.Expression;
        var value = expression is InvocationExpressionSyntax or ConditionalExpressionSyntax
            ? "(" + Expression(expression) + ")"
            : Expression(expression);
        return argument.NameColon is null
            ? value
            : argument.NameColon.Name.Identifier.ValueText + ": " + value;
    }

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
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.ModuloExpression => "%",
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.LessThanExpression => "<",
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.LessThanOrEqualExpression => "<=",
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.GreaterThanExpression => ">",
            Microsoft.CodeAnalysis.CSharp.SyntaxKind.GreaterThanOrEqualExpression => ">=",
            _ => throw new InvalidOperationException("Unsupported C# binary expression: " + kind)
        },
        _ => throw new InvalidOperationException("Unsupported C# binary expression: " + kind)
    };
    private static string EmitLiteral(LiteralExpressionSyntax literal)
    {
        if (!literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression)) return literal.Token.Text;
        // C# verbatim literals are not SEG literals.  ValueText is the
        // decoded semantic content; re-escape only when the source token used
        // the verbatim form.  Ordinary literals retain their original token
        // byte-for-byte, including [[translations]].
        if (!literal.Token.Text.StartsWith("@\"", StringComparison.Ordinal)) return literal.Token.Text;
        var value = literal.Token.ValueText.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal);
        return "\"" + value + "\"";
    }

    private static string LiteralOrExpression(SyntaxNode? node) => node is LiteralExpressionSyntax l && l.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression) ? EmitLiteral(l) : Expression(node);
    private static string DialogueText(SyntaxNode? node)
    {
        if (node is not LiteralExpressionSyntax literal || !literal.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression))
            return Expression(node);
        var value = literal.Token.ValueText;
        // Raw SEG narrative lines trim layout whitespace.  Quote only when
        // leading/trailing content whitespace is part of the C# literal; the
        // literal value itself remains unchanged.
        return value.Length != value.Trim().Length ? EmitLiteral(literal) : value;
    }
    private static string Arg(SeparatedSyntaxList<ArgumentSyntax> args, int index) => index >= 0 && index < args.Count ? Expression(args[index].Expression) : "";

    private static void EnsureOneBlankLineBeforeDialogue(StringBuilder sb)
    {
        while (sb.Length > 0 && (sb[^1] == '\r' || sb[^1] == '\n')) sb.Remove(sb.Length - 1, 1);
        sb.AppendLine();
        sb.AppendLine();
    }

    private static void EnsureOneBlankLineAfterDialogue(StringBuilder sb)
    {
        while (sb.Length > 0 && (sb[^1] == '\r' || sb[^1] == '\n')) sb.Remove(sb.Length - 1, 1);
        sb.AppendLine();
        sb.AppendLine();
    }

    private static void EnsureExactlyOneBlankLine(StringBuilder sb)
    {
        while (sb.Length > 0 && (sb[^1] == '\r' || sb[^1] == '\n')) sb.Remove(sb.Length - 1, 1);
        sb.AppendLine();
        sb.AppendLine();
    }

    private static void EnsureOneBlankLineAfterHeader(StringBuilder sb)
    {
        while (sb.Length > 0 && (sb[^1] == '\r' || sb[^1] == '\n')) sb.Remove(sb.Length - 1, 1);
        sb.AppendLine();
        sb.AppendLine();
    }

    private static string FormatCondition(ExpressionSyntax expression, string prefix, string continuationPrefix, string suffix)
        => FormatConditionText(prefix, continuationPrefix, Expression(expression), suffix, expression);

    private static string FormatConditionText(string prefix, string continuationPrefix, string rendered, string suffix, ExpressionSyntax? syntax = null)
    {
        var clauses = new List<(string Operator, ExpressionSyntax Expression)>();
        if (syntax is not null && syntax is BinaryExpressionSyntax binary)
        {
            var op = binary.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalAndExpression) ? "and" : binary.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalOrExpression) ? "or" : null;
            if (op is not null) CollectConditionClauses(binary, op, clauses);
        }
        if (clauses.Count < 3 && rendered.Length <= 100) return prefix + rendered + suffix;
        if (clauses.Count == 0) return prefix + rendered + suffix;
        var result = new StringBuilder(prefix).Append(Expression(clauses[0].Expression));
        foreach (var clause in clauses.Skip(1)) result.AppendLine().Append(continuationPrefix).Append(clause.Operator).Append(' ').Append(Expression(clause.Expression));
        return result.Append(suffix).ToString();
    }

    private static void CollectConditionClauses(ExpressionSyntax expression, string op, List<(string Operator, ExpressionSyntax Expression)> clauses)
    {
        if (expression is BinaryExpressionSyntax binary
            && ((op == "and" && binary.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalAndExpression))
                || (op == "or" && binary.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.LogicalOrExpression))))
        {
            CollectConditionClauses(binary.Left, op, clauses);
            clauses.Add((op, binary.Right));
            return;
        }
        if (clauses.Count == 0) clauses.Add(("", expression));
    }
    private static string? RegistrationKind(InvocationExpressionSyntax x) => x.Expression.ToString() switch { "addHandlerCombine" => "combine", "addHandlerUseFor" => "use-for", "addHandlerUseHere" => "use-here", "addHandlerPickUp" => "pickup", "addHandlerTalkHere" => "talk-here", "addHandlerCancelTextInput" => "cancel-text-input", "addHandlerSubmitTextInput" => "submit-text-input", "addRoomChangedHandler" => "room-changed", _ => null };
}
