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
public sealed record MigrationUnit(string Id, string Path, int Line, int EndLine, string GeneratedSeg, MigrationUnitStatus Status, IReadOnlyList<MigrationDiagnostic> Diagnostics);
public sealed record MigrationOutput(string Text, IReadOnlyList<MigrationDiagnostic> Diagnostics)
{
    public IReadOnlyList<MigrationUnit> Units { get; init; } = Array.Empty<MigrationUnit>();
    public bool IsFullyTranslated => Units.Count != 0 && Units.All(x => x.Status == MigrationUnitStatus.Translated);
}

/// <summary>Deterministic Roslyn-based first-pass C# to SEG emitter.</summary>
public static class CSharpToSegTranspiler
{
    public static MigrationOutput Transpile(string path, string text, bool emitPartial = false, string? methodName = null)
    {
        var tree = CSharpSyntaxTree.ParseText(text, path: path);
        var diagnostics = new List<MigrationDiagnostic>();
        var sb = new StringBuilder("world migrated\n");
        var root = (CompilationUnitSyntax)tree.GetRoot();
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
            else if (method.Identifier.ValueText is not "Configure" && method.Body != null && IsHelperCandidate(method))
            {
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
        foreach (var comment in CommentInventory(root))
            if (!generated.Contains(comment, StringComparison.Ordinal))
                diagnostics.Add(new(MigrationUnitStatus.Unsupported, path, 1, "comment was not preserved: " + comment));
        var parsed = DslParser.Parse(new Segusum.Scripting.Core.DslSource(path + ".generated.seg", generated));
        foreach (var diagnostic in parsed.Diagnostics)
            diagnostics.Add(new(MigrationUnitStatus.Unsupported, path + ".generated.seg", diagnostic.Span.Line, "generated SEG is not parsable: " + diagnostic.Message));
        if (parsed.Diagnostics.Count == 0)
        {
            var sourceRegistrations = MigrationVerifier.ExtractCSharpRegistrations(path, text);
            var generatedRegistrations = MigrationVerifier.ExtractDslRegistrations(new DslSource(path + ".generated.seg", generated));
            foreach (var sourceRegistration in sourceRegistrations)
            {
                var generatedRegistration = generatedRegistrations.FirstOrDefault(x => x.Kind == sourceRegistration.Kind && x.First == sourceRegistration.First && x.SecondOrTarget == sourceRegistration.SecondOrTarget);
                if (generatedRegistration is null)
                    diagnostics.Add(new(MigrationUnitStatus.Unsupported, path, sourceRegistration.SourceLine, "semantic round-trip mismatch: generated handler registration is missing"));
                else if (MigrationVerifier.CompareRegistration(sourceRegistration, generatedRegistration).Overall != EquivalenceStatus.Pass)
                    diagnostics.Add(new(MigrationUnitStatus.Unsupported, path, sourceRegistration.SourceLine, "semantic round-trip mismatch: handler fingerprint differs"));
            }

            var sourceCycles = MigrationVerifier.ExtractCSharpCycles(path, text);
            var generatedCycles = MigrationVerifier.ExtractDslCycles(new DslSource(path + ".generated.seg", generated));
            foreach (var sourceCycle in sourceCycles)
            {
                var generatedCycle = generatedCycles.FirstOrDefault(x => x.Id == sourceCycle.Id);
                if (generatedCycle is null)
                {
                    diagnostics.Add(new(MigrationUnitStatus.Unsupported, path, sourceCycle.SourceLine,
                        "semantic round-trip mismatch: generated cycle is missing"));
                }
                else if (MigrationVerifier.CompareCycles(new[] { sourceCycle }, new[] { generatedCycle }).Status != EquivalenceStatus.Pass)
                {
                    diagnostics.Add(new(MigrationUnitStatus.Unsupported, path, sourceCycle.SourceLine,
                        "semantic round-trip mismatch: cycle fingerprint differs"));
                }
            }
        }
        var units = BuildUnits(path, root, generated, diagnostics);
        return new MigrationOutput(generated, diagnostics) { Units = units };
    }

    public static IReadOnlyList<string> CommentInventory(string path, string text)
        => CommentInventory((CompilationUnitSyntax)CSharpSyntaxTree.ParseText(text, path: path).GetRoot());

    private static IReadOnlyList<string> CommentInventory(CompilationUnitSyntax root)
        => root.DescendantTrivia()
            .Where(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineCommentTrivia) || x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineCommentTrivia))
            .Select(x => x.ToString().Trim())
            .ToArray();

    private static IReadOnlyList<MigrationUnit> BuildUnits(string path, CompilationUnitSyntax root, string generated, List<MigrationDiagnostic> diagnostics)
    {
        var units = new List<MigrationUnit>();
        foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>().Where(x => RegistrationKind(x) != null))
        {
            var line = invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var id = RegistrationKind(invocation) + ":" + Arg(invocation.ArgumentList.Arguments, 0);
            var endLine = invocation.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
            var own = diagnostics.Where(x => (x.Path == path && x.Line >= line && x.Line <= endLine) || IsGeneratedDiagnosticForUnit(x, path) || IsGlobalDiagnostic(x, path)).ToArray();
            units.Add(new MigrationUnit(id, path, line, endLine, generated, own.Length == 0 ? MigrationUnitStatus.Translated : own[0].Status, own));
        }
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>().Where(x => x.Body != null && (x.Identifier.ValueText is "afterActionExecutedCSharp" or "beforeRoomChangeManual" or "beforeRoomChangeSegusum" || IsHelperCandidate(x))))
        {
            var line = method.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            var endLine = method.GetLocation().GetLineSpan().EndLinePosition.Line + 1;
            var own = diagnostics.Where(x => (x.Path == path && x.Line >= line && x.Line <= endLine) || IsGeneratedDiagnosticForUnit(x, path) || IsGlobalDiagnostic(x, path)).ToArray();
            units.Add(new MigrationUnit(method.Identifier.ValueText, path, line, endLine, generated, own.Any(x => x.Status == MigrationUnitStatus.Unsupported) ? MigrationUnitStatus.Unsupported : own.Any() ? own[0].Status : MigrationUnitStatus.Translated, own));
        }
        return units;
    }

    private static bool IsGeneratedDiagnosticForUnit(MigrationDiagnostic diagnostic, string sourcePath)
        => diagnostic.Path == sourcePath + ".generated.seg";

    private static bool IsGlobalDiagnostic(MigrationDiagnostic diagnostic, string sourcePath)
        => diagnostic.Path == sourcePath && (diagnostic.Reason.StartsWith("comment was not preserved:", StringComparison.Ordinal)
            || diagnostic.Reason.StartsWith("semantic round-trip mismatch:", StringComparison.Ordinal));

    private static bool IsHelperCandidate(MethodDeclarationSyntax method)
        => method.Modifiers.Any(x => x.ValueText is "private" or "protected") && method.ParameterList.Parameters.All(x => x.Type != null);

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
        if (possible is AnonymousFunctionExpressionSyntax lambda)
        {
            if (lambda.Body is BlockSyntax) Unsupported(lambda, diagnostics, "block-bodied possible-when", partial, sb, 1);
            else sb.Append("  possible-when ").AppendLine(Expression(lambda.Body));
        }
        var handler = args.Select(x => x.Expression).OfType<AnonymousFunctionExpressionSyntax>().LastOrDefault();
        if (handler?.Body is BlockSyntax block) EmitStatements(block.Statements, sb, diagnostics, 1, partial);
        else if (handler != null) Unsupported(handler, diagnostics, "expression-bodied handler", partial, sb, 1);
        sb.AppendLine("end");
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
                    if (a.Left.ToString().EndsWith("makesNoSenseAtThisTime", StringComparison.Ordinal) && a.Right.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.TrueLiteralExpression)) sb.Append(indent).AppendLine("makes-no-sense");
                    else if (a.Left.ToString().EndsWith("textInputToShow", StringComparison.Ordinal)) sb.Append(indent).Append("text-input ").AppendLine(Expression(a.Right));
                    else sb.Append(indent).Append(Expression(a.Left)).Append(' ').Append(a.OperatorToken.Text).Append(' ').AppendLine(Expression(a.Right)); break;
                case ExpressionStatementSyntax x when x.Expression is InvocationExpressionSyntax i:
                    EmitInvocation(i, sb, diagnostics, level, partial); break;
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
        if (CallName(invocation) == "addToCycle") { EmitCycleElement(invocation, sb, diagnostics, level, partial); return; }
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
        foreach (var add in new[] { initializer }.Concat(initializer.DescendantNodes().OfType<InvocationExpressionSyntax>()).Where(x => CallName(x) == "addToCycle").OrderBy(x => x.Span.End))
            EmitCycleElementCore(variable, add.ArgumentList.Arguments, add, sb, diagnostics, level, partial, false);
    }

    private static void EmitCycleElement(InvocationExpressionSyntax invocation, StringBuilder sb, List<MigrationDiagnostic> diagnostics, int level, bool partial)
        => EmitCycleElementCore(invocation.Expression is MemberAccessExpressionSyntax member ? Expression(member.Expression) : "cyc", invocation.ArgumentList.Arguments, invocation, sb, diagnostics, level, partial, false);

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
        var root = invocation.SyntaxTree.GetRoot();
        var declaration = root.DescendantNodes().OfType<VariableDeclaratorSyntax>().FirstOrDefault(x => x.Identifier.ValueText == id && x.Initializer?.Value is ObjectCreationExpressionSyntax creation && creation.Type.ToString().EndsWith("NamedCutSceneId", StringComparison.Ordinal));
        var title = declaration?.Initializer?.Value.DescendantNodes().OfType<LiteralExpressionSyntax>().FirstOrDefault(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.StringLiteralExpression));
        return title?.Token.Text;
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
    { foreach (var t in trivia.Where(x => x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.SingleLineCommentTrivia) || x.IsKind(Microsoft.CodeAnalysis.CSharp.SyntaxKind.MultiLineCommentTrivia))) sb.Append(new string(' ', level * 4)).Append("// ").AppendLine(t.ToString().TrimStart('/').Trim()); }
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
            BinaryExpressionSyntax x => Expression(x.Left) + " " + BinaryOperator(x.Kind()) + " " + Expression(x.Right),
            MemberAccessExpressionSyntax x => Expression(x.Expression) + "." + x.Name.Identifier.ValueText,
            InvocationExpressionSyntax x => EmitCallExpression(x),
            ArgumentSyntax x => (x.NameColon is null ? "" : x.NameColon.Name.Identifier.ValueText + ": ") + Expression(x.Expression),
            _ => throw new InvalidOperationException("Unsupported C# expression: " + node.Kind())
        };
    }

    private static string EmitCallExpression(InvocationExpressionSyntax invocation)
    {
        var name = invocation.Expression is MemberAccessExpressionSyntax member ? member.Name.Identifier.ValueText : invocation.Expression.ToString();
        if (name is "Where" or "Select" or "OrderBy" or "ThenBy" or "GroupBy" or "Count" or "Any" or "First" or "FirstOrDefault" || invocation.ArgumentList.Arguments.Any(x => x.Expression is AnonymousFunctionExpressionSyntax or LambdaExpressionSyntax))
            throw new InvalidOperationException("Unsupported C# call: " + name);
        return Expression(invocation.Expression) + "(" + string.Join(", ", invocation.ArgumentList.Arguments.Select(Expression)) + ")";
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
