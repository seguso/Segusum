using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Segusum.Scripting.Core;

namespace Segusum.Scripting.Tooling;

public enum EquivalenceStatus { Pass, Fail, Inconclusive }

public sealed record HandlerRegistrationFingerprint(
    string Kind,
    string First,
    string? SecondOrTarget,
    string? Phrase,
    string? Explanation,
    string? PossibleWhen,
    string SourcePath,
    int SourceLine);

public sealed record VerificationCheck(string Name, EquivalenceStatus Status, string? Detail = null);

public sealed record NamedCutsceneFingerprint(
    string Id,
    string? Title,
    IReadOnlyList<string> RuntimeArguments,
    string BodyShape,
    string SourcePath,
    int SourceLine);

public sealed record MarkHappenedOnceFingerprint(string Target, string SourcePath, int SourceLine);
public sealed record MarkHappenedFingerprint(string Target, string SourcePath, int SourceLine);
public sealed record DialogueFingerprint(string Speaker, string Text, string? Insta, string SourcePath, int SourceLine);
public sealed record BeforeRoomChangeFingerprint(IReadOnlyList<string> OrderedOperations, IReadOnlyList<string> Strings, string SourcePath, int SourceLine);

public sealed record HandlerEquivalenceResult(
    HandlerRegistrationFingerprint? CSharpRegistration,
    HandlerRegistrationFingerprint? DslRegistration,
    IReadOnlyList<VerificationCheck> Checks)
{
    public EquivalenceStatus Overall => Checks.Any(x => x.Status == EquivalenceStatus.Fail)
        ? EquivalenceStatus.Fail
        : Checks.Any(x => x.Status == EquivalenceStatus.Inconclusive)
            ? EquivalenceStatus.Inconclusive
            : EquivalenceStatus.Pass;
}

/// <summary>
/// Small, editor-independent safety net for C# to DSL migrations. It is
/// deliberately conservative: unresolved semantic information is reported as
/// INCONCLUSIVE instead of being guessed from a spelling.
/// </summary>
public static class MigrationVerifier
{
    public static IReadOnlyList<NamedCutsceneFingerprint> ExtractCSharpNamedCutscenes(string path, string text)
    {
        var tree = CSharpSyntaxTree.ParseText(text, path: path);
        var root = tree.GetRoot();
        var titles = root.DescendantNodes().OfType<VariableDeclaratorSyntax>()
            .Where(x => x.Initializer?.Value is ObjectCreationExpressionSyntax creation
                && creation.Type.ToString() == "NamedCutSceneId")
            .Select(x => (Id: x.Identifier.ValueText, Title: NamedCutsceneTitle(x.Initializer!.Value as ObjectCreationExpressionSyntax)))
            .ToDictionary(x => x.Id, x => x.Title, StringComparer.Ordinal);

        return root.DescendantNodes().OfType<UsingStatementSyntax>()
            .Select(x => (Using: x, Invocation: x.Expression as InvocationExpressionSyntax))
            .Where(x => x.Invocation != null && InvocationName(x.Invocation!) == "namedCutScene")
            .Select(x =>
            {
                var invocation = x.Invocation!;
                var arguments = invocation.ArgumentList.Arguments.Select(a => a.Expression.ToString()).ToArray();
                var id = arguments.ElementAtOrDefault(0) ?? "";
                return new NamedCutsceneFingerprint(id, titles.GetValueOrDefault(id), arguments.Skip(1).ToArray(), CSharpBodyShape(x.Using.Statement), path,
                    x.Using.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
            })
            .ToArray();
    }

    public static IReadOnlyList<NamedCutsceneFingerprint> ExtractDslNamedCutscenes(DslSource source)
    {
        var result = new List<NamedCutsceneFingerprint>();
        var parsed = DslParser.Parse(source);
        foreach (var declaration in parsed.Document.Declarations)
        {
            switch (declaration)
            {
                case HandlerDeclaration handler: CollectDslNamedCutscenes(handler.Body, source.Path, result); break;
                case FunctionDeclaration function: CollectDslNamedCutscenes(function.Body, source.Path, result); break;
                case CycleElementDeclaration element: CollectDslNamedCutscenes(element.Body, source.Path, result); break;
            }
        }
        return result;
    }

    public static VerificationCheck CompareNamedCutscenes(
        IReadOnlyList<NamedCutsceneFingerprint> csharp,
        IReadOnlyList<NamedCutsceneFingerprint> dsl)
    {
        if (csharp.Count != dsl.Count)
            return new("named-cutscene presence", EquivalenceStatus.Fail, $"C# count={csharp.Count}; DSL count={dsl.Count}");
        for (var i = 0; i < csharp.Count; i++)
        {
            var left = csharp[i];
            var right = dsl[i];
            if (left.Id != right.Id || left.Title != right.Title || !left.RuntimeArguments.SequenceEqual(right.RuntimeArguments, StringComparer.Ordinal) || left.BodyShape != right.BodyShape)
                return new("named-cutscene", EquivalenceStatus.Fail,
                    $"C#={left.Id}|{left.Title}|({string.Join(",", left.RuntimeArguments)})|{left.BodyShape}; DSL={right.Id}|{right.Title}|({string.Join(",", right.RuntimeArguments)})|{right.BodyShape}");
        }
        return new("named-cutscene", EquivalenceStatus.Pass);
    }

    public static IReadOnlyList<HandlerRegistrationFingerprint> ExtractCSharpRegistrations(string path, string text)
    {
        var tree = CSharpSyntaxTree.ParseText(text, path: path);
        return ExtractCSharpRegistrationsCore(tree, null);
    }

    public static IReadOnlyList<HandlerRegistrationFingerprint> ExtractCSharpRegistrations(
        SyntaxTree tree, SemanticModel semanticModel)
        => ExtractCSharpRegistrationsCore(tree, semanticModel);

    private static IReadOnlyList<HandlerRegistrationFingerprint> ExtractCSharpRegistrationsCore(
        SyntaxTree tree, SemanticModel? semanticModel)
    {
        return tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(x => RegistrationKind(x) != null)
            .Select(x => Registration(tree.FilePath, x, semanticModel?.GetOperation(x)))
            .ToArray();
    }

    public static IReadOnlyList<HandlerRegistrationFingerprint> ExtractDslRegistrations(DslSource source)
    {
        var result = new List<HandlerRegistrationFingerprint>();
        var parsed = DslParser.Parse(source);
        foreach (var declaration in parsed.Document.Declarations.OfType<HandlerDeclaration>())
        {
            result.Add(new HandlerRegistrationFingerprint(
                declaration.Kind,
                declaration.First,
                declaration.Second ?? declaration.Target,
                StringValue(declaration.Phrase),
                StringValue(declaration.Explanation),
                ExpressionText(declaration.Condition),
                source.Path,
                declaration.Span.Line));
        }
        return result;
    }

    public static HandlerEquivalenceResult CompareRegistration(
        HandlerRegistrationFingerprint? csharp,
        HandlerRegistrationFingerprint? dsl)
    {
        if (csharp == null || dsl == null)
            return new(csharp, dsl, new[] { new VerificationCheck("registration", EquivalenceStatus.Inconclusive, "Missing registration on one side.") });

        var checks = new List<VerificationCheck>
        {
            Equal("kind", csharp.Kind, dsl.Kind),
            Equal("first operand", csharp.First, dsl.First),
            Equal("second/target", csharp.SecondOrTarget, dsl.SecondOrTarget),
            Equal("phrase", csharp.Phrase, dsl.Phrase),
            Equal("explanation", csharp.Explanation, dsl.Explanation),
            Equal("possible-when", csharp.PossibleWhen, dsl.PossibleWhen)
        };
        return new(csharp, dsl, checks);
    }

    public static VerificationCheck CompareStrings(IReadOnlyList<string> csharp, IReadOnlyList<string> dsl)
        => csharp.SequenceEqual(dsl, StringComparer.Ordinal)
            ? new("strings", EquivalenceStatus.Pass)
            : new("strings", EquivalenceStatus.Fail, $"C#=[{string.Join(", ", csharp)}] DSL=[{string.Join(", ", dsl)}]");

    public static IReadOnlyList<DialogueFingerprint> ExtractCSharpDialogues(string path, string text)
    {
        var tree = CSharpSyntaxTree.ParseText(text, path: path);
        return tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(x => InvocationName(x) == "dial" && x.ArgumentList.Arguments.Count >= 2)
            .Select(x => new DialogueFingerprint(
                x.ArgumentList.Arguments[0].Expression.ToString(),
                LiteralText(x.ArgumentList.Arguments[1].Expression.ToString()) ?? x.ArgumentList.Arguments[1].Expression.ToString(),
                NormalizeRuntimeExpression(x.ArgumentList.Arguments.FirstOrDefault(a => a.NameColon?.Name.Identifier.ValueText == "insta")?.Expression.ToString()),
                path, x.GetLocation().GetLineSpan().StartLinePosition.Line + 1))
            .ToArray();
    }

    public static IReadOnlyList<DialogueFingerprint> ExtractDslDialogues(DslSource source)
    {
        var result = new List<DialogueFingerprint>();
        var parsed = DslParser.Parse(source);
        foreach (var declaration in parsed.Document.Declarations)
            CollectDslDialogues(declaration, source.Path, result);
        return result;
    }

    public static VerificationCheck CompareDialogues(IReadOnlyList<DialogueFingerprint> csharp, IReadOnlyList<DialogueFingerprint> dsl)
    {
        if (csharp.Count != dsl.Count)
            return new("dialogues", EquivalenceStatus.Fail, $"C# count={csharp.Count}; DSL count={dsl.Count}");
        for (var i = 0; i < csharp.Count; i++)
        {
            if (csharp[i].Speaker != dsl[i].Speaker || csharp[i].Text != dsl[i].Text || csharp[i].Insta != dsl[i].Insta)
                return new("dialogues", EquivalenceStatus.Fail, $"C#={csharp[i].Speaker}|{csharp[i].Text}|{csharp[i].Insta}; DSL={dsl[i].Speaker}|{dsl[i].Text}|{dsl[i].Insta}");
        }
        return new("dialogues", EquivalenceStatus.Pass);
    }

    public static IReadOnlyList<BeforeRoomChangeFingerprint> ExtractDslBeforeRoomChange(DslSource source)
    {
        var result = new List<BeforeRoomChangeFingerprint>();
        foreach (var hook in DslParser.Parse(source).Document.Declarations.OfType<BeforeRoomChangeDeclaration>())
        {
            var operations = new List<string>(); var strings = new List<string>();
            CollectBeforeRoomChange(hook.Body, operations, strings);
            result.Add(new BeforeRoomChangeFingerprint(operations, strings, source.Path, hook.Span.Line));
        }
        return result;
    }

    public static IReadOnlyList<BeforeRoomChangeFingerprint> ExtractCSharpBeforeRoomChange(string path, string text)
    {
        var tree = CSharpSyntaxTree.ParseText(text, path: path);
        var result = new List<BeforeRoomChangeFingerprint>();
        foreach (var method in tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .Where(x => x.Identifier.ValueText is "beforeRoomChangeManual" or "beforeRoomChangeSegusum"))
        {
            var operations = new List<string>(); var strings = new List<string>();
            if (method.Body != null) CollectCSharpBeforeRoomChange(method.Body.Statements, operations, strings);
            result.Add(new BeforeRoomChangeFingerprint(operations, strings, path,
                method.GetLocation().GetLineSpan().StartLinePosition.Line + 1));
        }
        return result;
    }

    public static VerificationCheck CompareBeforeRoomChange(BeforeRoomChangeFingerprint csharp, BeforeRoomChangeFingerprint dsl)
    {
        var operations = csharp.OrderedOperations.SequenceEqual(dsl.OrderedOperations, StringComparer.Ordinal);
        var strings = csharp.Strings.SequenceEqual(dsl.Strings, StringComparer.Ordinal);
        return operations && strings
            ? new("before-room-change", EquivalenceStatus.Pass)
            : new("before-room-change", EquivalenceStatus.Fail,
                $"operations C#=[{string.Join(";", csharp.OrderedOperations)}] DSL=[{string.Join(";", dsl.OrderedOperations)}]; strings C#=[{string.Join(";", csharp.Strings)}] DSL=[{string.Join(";", dsl.Strings)}]");
    }

    public static IReadOnlyList<MarkHappenedOnceFingerprint> ExtractCSharpMarkHappenedOnce(string path, string text)
    {
        var tree = CSharpSyntaxTree.ParseText(text, path: path);
        return tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(x => InvocationName(x) == "setIfNeverHappened"
                && x.ArgumentList.Arguments.Count == 1
                && x.ArgumentList.Arguments[0].RefKindKeyword.IsKind(SyntaxKind.RefKeyword))
            .Select(x => new MarkHappenedOnceFingerprint(x.ArgumentList.Arguments[0].Expression.ToString(), path,
                x.GetLocation().GetLineSpan().StartLinePosition.Line + 1))
            .ToArray();
    }

    public static IReadOnlyList<MarkHappenedOnceFingerprint> ExtractDslMarkHappenedOnce(DslSource source)
    {
        var result = new List<MarkHappenedOnceFingerprint>();
        var parsed = DslParser.Parse(source);
        foreach (var declaration in parsed.Document.Declarations)
        {
            switch (declaration)
            {
                case HandlerDeclaration handler: CollectDslMarkHappenedOnce(handler.Body, source.Path, result); break;
                case FunctionDeclaration function: CollectDslMarkHappenedOnce(function.Body, source.Path, result); break;
                case CycleElementDeclaration element: CollectDslMarkHappenedOnce(element.Body, source.Path, result); break;
            }
        }
        return result;
    }

    public static VerificationCheck CompareMarkHappenedOnce(
        IReadOnlyList<MarkHappenedOnceFingerprint> csharp,
        IReadOnlyList<MarkHappenedOnceFingerprint> dsl)
    {
        if (csharp.Count != dsl.Count)
            return new("mark-happened-once presence", EquivalenceStatus.Fail, $"C# count={csharp.Count}; DSL count={dsl.Count}");
        for (var i = 0; i < csharp.Count; i++)
        {
            if (!string.Equals(csharp[i].Target, dsl[i].Target, StringComparison.Ordinal))
                return new("mark-happened-once target", EquivalenceStatus.Fail, $"C#='{csharp[i].Target}' DSL='{dsl[i].Target}'");
        }
        return new("mark-happened-once", EquivalenceStatus.Pass);
    }

    public static IReadOnlyList<MarkHappenedFingerprint> ExtractCSharpMarkHappened(string path, string text)
    {
        var tree = CSharpSyntaxTree.ParseText(text, path: path);
        return tree.GetRoot().DescendantNodes().OfType<AssignmentExpressionSyntax>()
            .Where(x => x.Right.ToString() == "DateTime.Now")
            .Select(x => new MarkHappenedFingerprint(x.Left.ToString(), path, x.GetLocation().GetLineSpan().StartLinePosition.Line + 1))
            .ToArray();
    }

    public static IReadOnlyList<MarkHappenedFingerprint> ExtractDslMarkHappened(DslSource source)
    {
        var result = new List<MarkHappenedFingerprint>();
        var parsed = DslParser.Parse(source);
        foreach (var declaration in parsed.Document.Declarations)
            switch (declaration)
            {
                case HandlerDeclaration handler: CollectDslMarkHappened(handler.Body, source.Path, result); break;
                case FunctionDeclaration function: CollectDslMarkHappened(function.Body, source.Path, result); break;
                case CycleElementDeclaration element: CollectDslMarkHappened(element.Body, source.Path, result); break;
            }
        return result;
    }

    public static VerificationCheck CompareMarkHappened(IReadOnlyList<MarkHappenedFingerprint> csharp, IReadOnlyList<MarkHappenedFingerprint> dsl)
    {
        if (csharp.Count != dsl.Count) return new("mark-happened presence", EquivalenceStatus.Fail, $"C# count={csharp.Count}; DSL count={dsl.Count}");
        for (var i = 0; i < csharp.Count; i++)
            if (!string.Equals(csharp[i].Target, dsl[i].Target, StringComparison.Ordinal))
                return new("mark-happened target", EquivalenceStatus.Fail, $"C#='{csharp[i].Target}' DSL='{dsl[i].Target}'");
        return new("mark-happened", EquivalenceStatus.Pass);
    }

    public static IReadOnlyList<string> ExtractCSharpStrings(string path, string text)
    {
        var tree = CSharpSyntaxTree.ParseText(text, path: path);
        var strings = new List<string>();
        foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(x => !x.Ancestors().OfType<InvocationExpressionSyntax>().Any()))
            ExtractStrings(invocation, strings);
        return strings;
    }

    public static IReadOnlyList<string> ExtractCSharpStringsForRegistration(
        string path, string text, string kind, string first, string? secondOrTarget)
    {
        var tree = CSharpSyntaxTree.ParseText(text, path: path);
        return ExtractCSharpStringsForRegistration(tree, kind, first, secondOrTarget);
    }

    public static IReadOnlyList<string> ExtractCSharpStringsForRegistration(
        SyntaxTree tree, SemanticModel semanticModel, string kind, string first, string? secondOrTarget)
    {
        var registration = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(x => RegistrationKind(x) == kind
                && OperandText(x.ArgumentList.Arguments, 0) == first
                && (kind is "room-changed" or "pickup" or "talk-here" or "cancel-text-input" or "submit-text-input"
                    || OperandText(x.ArgumentList.Arguments, 1) == secondOrTarget));
        var strings = new List<string>();
        if (registration != null)
        {
            ExtractStrings(registration, strings);
            AddReferencedMethodStrings(tree.GetRoot(), registration, strings,
                new HashSet<string>(StringComparer.Ordinal), semanticModel);
        }
        return strings;
    }

    private static IReadOnlyList<string> ExtractCSharpStringsForRegistration(
        SyntaxTree tree, string kind, string first, string? secondOrTarget)
    {
        var registration = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(x => RegistrationKind(x) == kind
                && OperandText(x.ArgumentList.Arguments, 0) == first
                && (kind is "room-changed" or "pickup" or "talk-here" or "cancel-text-input" or "submit-text-input"
                    || OperandText(x.ArgumentList.Arguments, 1) == secondOrTarget));
        var strings = new List<string>();
        if (registration != null)
        {
            ExtractStrings(registration, strings);
            AddReferencedMethodStrings(tree.GetRoot(), registration, strings, new HashSet<string>(StringComparer.Ordinal), null);
        }
        return strings;
    }

    public static IReadOnlyList<string> ExtractDslStrings(DslSource source)
    {
        var result = new List<string>();
        var parsed = DslParser.Parse(source);
        foreach (var declaration in parsed.Document.Declarations)
        {
            if (declaration is HandlerDeclaration handler && handler.Phrase != null) AddString(handler.Phrase, result);
            if (declaration is HandlerDeclaration h) Walk(h.Body, result);
            if (declaration is FunctionDeclaration f) Walk(f.Body, result);
            if (declaration is BeforeRoomChangeDeclaration b) Walk(b.Body, result);
        }
        return result;
    }

    /// <summary>Creates a conservative, order-preserving IOperation fingerprint.</summary>
    public static string OperationFingerprint(IOperation operation)
    {
        var parts = new List<string>();
        Visit(operation, parts);
        return string.Join("|", parts);
    }

    public static VerificationCheck CompareOperations(IOperation? csharp, IOperation? generated)
    {
        if (csharp == null || generated == null)
            return new("operations", EquivalenceStatus.Inconclusive, "An IOperation was unavailable.");
        var left = OperationFingerprint(csharp);
        var right = OperationFingerprint(generated);
        return left == right
            ? new("operations", EquivalenceStatus.Pass)
            : new("operations", EquivalenceStatus.Fail, $"C#={left}; DSL={right}");
    }

    private static HandlerRegistrationFingerprint Registration(string path, InvocationExpressionSyntax invocation, IOperation? operation)
    {
        var kind = RegistrationKind(invocation)!;
        var args = invocation.ArgumentList.Arguments;
        var phrase = ArgumentText(args, kind == "combine" ? 2 : -1, "fullSentenceUntransl", "dynamicSentenceUntransl");
        var explanation = ArgumentText(args, kind == "use-for" ? 2 : -1, "explanation");
        var possible = ArgumentText(args, -1, "isPossibleNow");
        return new(kind, OperandText(args, 0), kind == "combine" ? OperandText(args, 1) : kind == "use-for" ? OperandText(args, 1) : null,
            LiteralText(phrase), LiteralText(explanation), possible, path,
            invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
    }

    private static string? NamedCutsceneTitle(ObjectCreationExpressionSyntax? creation)
    {
        var assignment = creation?.Initializer?.Expressions.OfType<AssignmentExpressionSyntax>()
            .FirstOrDefault(x => x.Left.ToString() == "titleUntranslated");
        if (assignment?.Right is InvocationExpressionSyntax invocation
            && InvocationName(invocation) == "translatable"
            && invocation.Expression is MemberAccessExpressionSyntax member
            && member.Expression is LiteralExpressionSyntax literal
            && literal.IsKind(SyntaxKind.StringLiteralExpression))
            return literal.Token.ValueText;
        return null;
    }

    private static string CSharpBodyShape(StatementSyntax statement)
        => string.Join("|", statement.DescendantNodes().OfType<InvocationExpressionSyntax>().Select(InvocationName));

    private static string? NormalizeRuntimeExpression(string? expression)
    {
        if (expression == null) return null;
        return expression.EndsWith("()", StringComparison.Ordinal) ? expression[..^2] : expression;
    }

    private static void CollectDslDialogues(DslDeclaration declaration, string path, List<DialogueFingerprint> result)
    {
        switch (declaration)
        {
            case HandlerDeclaration handler: CollectDslDialogues(handler.Body, path, result); break;
            case FunctionDeclaration function: CollectDslDialogues(function.Body, path, result); break;
            case CycleElementDeclaration cycle: CollectDslDialogues(cycle.Body, path, result); break;
        }
    }

    private static void CollectDslDialogues(IEnumerable<DslStatement> statements, string path, List<DialogueFingerprint> result)
    {
        foreach (var statement in statements)
            switch (statement)
            {
                case DialogueStatement dialogue:
                    result.Add(new DialogueFingerprint(dialogue.Character, StringValue(dialogue.Text) ?? "", ExpressionText(dialogue.Insta), path, dialogue.Span.Line));
                    break;
                case IfStatement conditional:
                    foreach (var branch in conditional.Branches) CollectDslDialogues(branch.Body, path, result);
                    if (conditional.ElseBody != null) CollectDslDialogues(conditional.ElseBody, path, result);
                    break;
                case AddCycleElementStatement cycle: CollectDslDialogues(cycle.Body, path, result); break;
                case NamedCutsceneStatement named: CollectDslDialogues(named.Body, path, result); break;
            }
    }

    private static void CollectBeforeRoomChange(IEnumerable<DslStatement> statements, List<string> operations, List<string> strings)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case DialogueStatement dialogue: operations.Add("dial:" + dialogue.Character + ":" + ExpressionText(dialogue.Text)); strings.Add(StringValue(dialogue.Text) ?? ""); break;
                case NarStatement nar: operations.Add("nar:" + ExpressionText(nar.Text)); strings.Add(StringValue(nar.Text) ?? ""); break;
                case NarRoomStatement narRoom: operations.Add("nar-room:" + ExpressionText(narRoom.Text)); strings.Add(StringValue(narRoom.Text) ?? ""); break;
                case PreventRoomChangeStatement: operations.Add("prevent-room-change"); break;
                case MarkHappenedOnceStatement mark: operations.Add("mark-happened-once:" + ExpressionText(mark.Target)); break;
                case MarkHappenedStatement mark: operations.Add("mark-happened:" + ExpressionText(mark.Target)); break;
                case AssignmentStatement assignment: operations.Add("assign:" + (assignment.Receiver == null ? assignment.Name : ExpressionText(assignment.Receiver) + "." + assignment.MemberName) + assignment.Operator + ExpressionText(assignment.Value)); break;
                case CallStatement call: operations.Add("call:" + ExpressionText(call.Expression)); break;
                case IfStatement conditional:
                    operations.Add("if:" + string.Join("|", conditional.Branches.Select(x => CanonicalCondition(ExpressionText(x.Condition) ?? ""))));
                    foreach (var branch in conditional.Branches) CollectBeforeRoomChange(branch.Body, operations, strings);
                    if (conditional.ElseBody != null) CollectBeforeRoomChange(conditional.ElseBody, operations, strings);
                    break;
            }
        }
    }

    private static void CollectCSharpBeforeRoomChange(IEnumerable<StatementSyntax> statements, List<string> operations, List<string> strings)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case ExpressionStatementSyntax expression when expression.Expression is InvocationExpressionSyntax invocation:
                    var name = InvocationName(invocation);
                    if (name == "dial" && invocation.ArgumentList.Arguments.Count >= 2)
                    {
                        var text = LiteralText(invocation.ArgumentList.Arguments[1].Expression.ToString()) ?? invocation.ArgumentList.Arguments[1].Expression.ToString();
                        operations.Add("dial:" + invocation.ArgumentList.Arguments[0].Expression + ":" + text);
                        strings.Add(text);
                    }
                    else if (name == "setIfNeverHappened" && invocation.ArgumentList.Arguments.Count == 1)
                        operations.Add("mark-happened-once:" + invocation.ArgumentList.Arguments[0].Expression);
                    else
                        operations.Add("call:" + name + "(" + string.Join(",", invocation.ArgumentList.Arguments.Select(x => x.Expression.ToString())) + ")");
                    break;
                case ExpressionStatementSyntax expression when expression.Expression is AssignmentExpressionSyntax assignment:
                    operations.Add("assign:" + assignment.Left + assignment.OperatorToken.Text + assignment.Right);
                    break;
                case IfStatementSyntax conditional:
                    operations.Add("if:" + CanonicalCondition(conditional.Condition.ToString()));
                    CollectCSharpBeforeRoomChange(conditional.Statement is BlockSyntax block ? block.Statements : new[] { conditional.Statement }, operations, strings);
                    if (conditional.Else?.Statement is { } elseStatement)
                        CollectCSharpBeforeRoomChange(elseStatement is BlockSyntax elseBlock ? elseBlock.Statements : new[] { elseStatement }, operations, strings);
                    break;
            }
        }
    }

    private static string CanonicalCondition(string condition)
        => condition.Replace("&&", " and ", StringComparison.Ordinal)
            .Replace("||", " or ", StringComparison.Ordinal)
            .Replace("!", "not ", StringComparison.Ordinal)
            .Replace("  ", " ", StringComparison.Ordinal).Trim();

    private static void CollectDslNamedCutscenes(IEnumerable<DslStatement> statements, string path, List<NamedCutsceneFingerprint> result)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case NamedCutsceneStatement named:
                    result.Add(new NamedCutsceneFingerprint(named.Id, StringValue(named.Title), named.Arguments.Select(x => ExpressionText(x) ?? "").ToArray(), DslBodyShape(named.Body), path, named.Span.Line));
                    CollectDslNamedCutscenes(named.Body, path, result);
                    break;
                case IfStatement conditional:
                    foreach (var branch in conditional.Branches) CollectDslNamedCutscenes(branch.Body, path, result);
                    if (conditional.ElseBody != null) CollectDslNamedCutscenes(conditional.ElseBody, path, result);
                    break;
                case AddCycleElementStatement cycle: CollectDslNamedCutscenes(cycle.Body, path, result); break;
            }
        }
    }

    private static void CollectDslMarkHappenedOnce(IEnumerable<DslStatement> statements, string path, List<MarkHappenedOnceFingerprint> result)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case MarkHappenedOnceStatement mark:
                    result.Add(new MarkHappenedOnceFingerprint(ExpressionText(mark.Target) ?? "", path, mark.Span.Line));
                    break;
                case IfStatement conditional:
                    foreach (var branch in conditional.Branches) CollectDslMarkHappenedOnce(branch.Body, path, result);
                    if (conditional.ElseBody != null) CollectDslMarkHappenedOnce(conditional.ElseBody, path, result);
                    break;
                case AddCycleElementStatement cycle: CollectDslMarkHappenedOnce(cycle.Body, path, result); break;
                case NamedCutsceneStatement named: CollectDslMarkHappenedOnce(named.Body, path, result); break;
            }
        }
    }

    private static void CollectDslMarkHappened(IEnumerable<DslStatement> statements, string path, List<MarkHappenedFingerprint> result)
    {
        foreach (var statement in statements)
            switch (statement)
            {
                case MarkHappenedStatement mark: result.Add(new MarkHappenedFingerprint(ExpressionText(mark.Target) ?? "", path, mark.Span.Line)); break;
                case IfStatement conditional:
                    foreach (var branch in conditional.Branches) CollectDslMarkHappened(branch.Body, path, result);
                    if (conditional.ElseBody != null) CollectDslMarkHappened(conditional.ElseBody, path, result);
                    break;
                case AddCycleElementStatement cycle: CollectDslMarkHappened(cycle.Body, path, result); break;
                case NamedCutsceneStatement named: CollectDslMarkHappened(named.Body, path, result); break;
            }
    }

    private static string DslBodyShape(IEnumerable<DslStatement> statements)
        => string.Join("|", statements.Select(statement => statement switch
        {
            DialogueStatement => "dial",
            NarStatement => "nar",
            NarRoomStatement => "nar-room",
            NarImgStatement => "nar-img",
            IfStatement conditional => "if(" + string.Join(";", conditional.Branches.Select(x => DslBodyShape(x.Body))) + ")",
            _ => statement.GetType().Name
        }));

    private static string? RegistrationKind(InvocationExpressionSyntax invocation) => InvocationName(invocation) switch
    {
        "addHandlerCombine" => "combine",
        "addHandlerUseFor" => "use-for",
        "addHandlerUseHere" => "use-here",
        "addHandlerPickUp" => "pickup",
        "addHandlerTalkHere" => "talk-here",
        "addHandlerCancelTextInput" => "cancel-text-input",
        "addHandlerSubmitTextInput" => "submit-text-input",
        "addRoomChangedHandler" => "room-changed",
        _ => null
    };

    private static string InvocationName(InvocationExpressionSyntax invocation) => invocation.Expression switch
    {
        IdentifierNameSyntax id => id.Identifier.ValueText,
        MemberAccessExpressionSyntax member => member.Name.Identifier.ValueText,
        _ => ""
    };

    private static string OperandText(SeparatedSyntaxList<ArgumentSyntax> args, int index)
        => index >= 0 && index < args.Count ? args[index].Expression.ToString() : "";

    private static string? ArgumentText(SeparatedSyntaxList<ArgumentSyntax> args, int index, params string[] names)
    {
        var named = args.FirstOrDefault(x => x.NameColon != null && names.Contains(x.NameColon.Name.Identifier.ValueText, StringComparer.Ordinal));
        if (named != null) return named.Expression.ToString();
        if (index < 0 || index >= args.Count) return null;
        // In the common overload without an explanation, argument 2 is the
        // handler lambda. It must not be mistaken for an explanation.
        if (args[index].Expression is AnonymousFunctionExpressionSyntax) return null;
        return args[index].Expression.ToString();
    }

    private static string? LiteralText(string? expression)
        => expression == null ? null : CSharpSyntaxTree.ParseText(expression).GetRoot().DescendantNodes().OfType<LiteralExpressionSyntax>().FirstOrDefault(x => x.IsKind(SyntaxKind.StringLiteralExpression))?.Token.ValueText ?? expression;

    private static string? StringValue(DslExpression? expression) => expression is LiteralExpression literal && literal.Kind is "string" or "raw-string"
        ? literal.Value.Trim('"') : expression == null ? null : ExpressionText(expression);

    private static string? ExpressionText(DslExpression? expression) => expression switch
    {
        null => null,
        IdentifierExpression id => id.Name,
        LiteralExpression literal => literal.Kind is "string" or "raw-string" ? literal.Value.Trim('"') : literal.Value,
        UnaryExpression unary => unary.Operator + ExpressionText(unary.Operand),
        BinaryExpression binary => ExpressionText(binary.Left) + " " + binary.Operator + " " + ExpressionText(binary.Right),
        ExistsExpression exists => "exists[from " + ExpressionText(exists.Collection) + " " + exists.ItemName + " where " + ExpressionText(exists.Predicate) + "]",
        ParenthesizedExpression parenthesized => "(" + ExpressionText(parenthesized.Expression) + ")",
        MemberAccessExpression member => ExpressionText(member.Receiver) + "." + member.MemberName,
        CallExpression call => (call.Receiver == null ? call.Name : ExpressionText(call.Receiver) + "." + call.Name)
            + "(" + string.Join(",", call.Arguments.Select(x => (x.Name == null ? "" : x.Name + ":") + ExpressionText(x.Expression))) + ")",
        FunctionReferenceExpression reference => "ref " + reference.Name,
        _ => expression.ToString()
    };

    private static void ExtractStrings(InvocationExpressionSyntax invocation, List<string> strings)
    {
        var name = InvocationName(invocation);
        var argument = name switch
        {
            "dial" => invocation.ArgumentList.Arguments.ElementAtOrDefault(1)?.Expression,
            "nar" or "narText" or "narRoom" => invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression,
            "addHandlerCombine" => invocation.ArgumentList.Arguments.ElementAtOrDefault(2)?.Expression,
            _ => invocation.ArgumentList.Arguments.FirstOrDefault(x => x.NameColon?.Name.Identifier.ValueText is "fullSentenceUntransl" or "dynamicSentenceUntransl")?.Expression
        };
        if (argument is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            strings.Add(literal.Token.ValueText);
        // DescendantNodes returns every nested invocation once. Recursing from
        // each nested invocation would count grandchildren repeatedly (which
        // is especially visible for cycle/dialogue lambdas).
        foreach (var nested in invocation.ArgumentList.DescendantNodes().OfType<InvocationExpressionSyntax>())
            ExtractStringsShallow(nested, strings);
    }

    private static void ExtractStringsShallow(InvocationExpressionSyntax invocation, List<string> strings)
    {
        var name = InvocationName(invocation);
        var argument = name switch
        {
            "dial" => invocation.ArgumentList.Arguments.ElementAtOrDefault(1)?.Expression,
            "nar" or "narText" or "narRoom" => invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression,
            "addHandlerCombine" => invocation.ArgumentList.Arguments.ElementAtOrDefault(2)?.Expression,
            _ => invocation.ArgumentList.Arguments.FirstOrDefault(x => x.NameColon?.Name.Identifier.ValueText is "fullSentenceUntransl" or "dynamicSentenceUntransl")?.Expression
        };
        if (argument is LiteralExpressionSyntax literal && literal.IsKind(SyntaxKind.StringLiteralExpression))
            strings.Add(literal.Token.ValueText);
    }

    private static void AddReferencedMethodStrings(SyntaxNode root, SyntaxNode owner, List<string> strings, HashSet<string> visited, SemanticModel? semanticModel)
    {
        var invocations = owner.DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(x => !x.Ancestors().OfType<InvocationExpressionSyntax>().Any(a => a != owner));
        foreach (var invocation in invocations)
        {
            var name = InvocationName(invocation);
            MethodDeclarationSyntax? method;
            if (semanticModel != null)
            {
                var operation = semanticModel.GetOperation(invocation) as IInvocationOperation;
                method = operation?.TargetMethod.DeclaringSyntaxReferences
                    .Select(reference => reference.GetSyntax())
                    .OfType<MethodDeclarationSyntax>()
                    .FirstOrDefault(x => root.SyntaxTree == x.SyntaxTree);
                if (method == null) continue;
            }
            else
            {
                var candidates = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                    .Where(x => x.Identifier.ValueText == name).ToArray();
                // Syntax-only extraction cannot safely choose among overloads.
                if (candidates.Length != 1) continue;
                method = candidates[0];
            }

            var visitKey = semanticModel == null
                ? name
                : method.GetLocation().GetLineSpan().Path + ":" + method.GetLocation().GetLineSpan().StartLinePosition.Line;
            if (!visited.Add(visitKey)) continue;

            var bodyInvocations = method.Body == null
                ? Enumerable.Empty<InvocationExpressionSyntax>()
                : method.Body.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Where(x => !x.Ancestors().OfType<InvocationExpressionSyntax>().Any());
            foreach (var nested in bodyInvocations)
                ExtractStrings(nested, strings);
            AddReferencedMethodStrings(root, method.Body ?? (SyntaxNode)method, strings, visited, semanticModel);
        }
    }

    private static VerificationCheck Equal(string name, string? left, string? right)
        => left == null && right == null
            ? new(name, EquivalenceStatus.Pass)
            : left == null || right == null
                ? new(name, EquivalenceStatus.Fail, $"C#='{left}' DSL='{right}'")
                : left == right ? new(name, EquivalenceStatus.Pass) : new(name, EquivalenceStatus.Fail, $"C#='{left}' DSL='{right}'");

    private static void AddString(DslExpression expression, List<string> result)
    {
        if (StringValue(expression) is { } value) result.Add(value);
    }

    private static void Walk(IEnumerable<DslStatement> statements, List<string> result)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case DialogueStatement dialogue: AddString(dialogue.Text, result); break;
                case NarStatement nar: AddString(nar.Text, result); break;
                case NarRoomStatement narRoom: AddString(narRoom.Text, result); break;
                case NarImgStatement narImg: AddString(narImg.Text, result); break;
                case IfStatement conditional:
                    foreach (var branch in conditional.Branches) Walk(branch.Body, result);
                    if (conditional.ElseBody != null) Walk(conditional.ElseBody, result);
                    break;
                case AddCycleElementStatement cycle: Walk(cycle.Body, result); break;
                case NamedCutsceneStatement namedCutscene: AddString(namedCutscene.Title, result); Walk(namedCutscene.Body, result); break;
            }
        }
    }

    private static void Visit(IOperation operation, List<string> result)
    {
        switch (operation)
        {
            case IInvocationOperation invocation:
                result.Add("invoke:" + invocation.TargetMethod.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat));
                break;
            case IFieldReferenceOperation field: result.Add("field:" + field.Field.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)); break;
            case IPropertyReferenceOperation property: result.Add("property:" + property.Property.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)); break;
            case IParameterReferenceOperation parameter: result.Add("parameter:" + parameter.Parameter.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)); break;
            case ILocalReferenceOperation local: result.Add("local:" + local.Local.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)); break;
            case ILiteralOperation literal: result.Add(literal.ConstantValue.HasValue ? "literal:" + literal.ConstantValue.Value?.ToString() : "literal:null"); break;
            case IUnaryOperation unary: result.Add("unary:" + unary.OperatorKind); break;
            case IBinaryOperation binary: result.Add("binary:" + binary.OperatorKind); break;
            case ICompoundAssignmentOperation compound: result.Add("compound-assign:" + compound.OperatorKind); break;
            case IIncrementOrDecrementOperation increment:
                result.Add("increment:" + (increment.Kind == OperationKind.Increment ? "increment" : "decrement") + ":" + (increment.IsPostfix ? "postfix" : "prefix"));
                break;
            case ISimpleAssignmentOperation assignment:
                result.Add("assign-target:" + ReferencedSymbol(assignment.Target));
                break;
            case IConditionalOperation: result.Add("conditional"); break;
            case IReturnOperation: result.Add("return"); break;
        }
        foreach (var child in operation.ChildOperations) Visit(child, result);
    }

    private static string ReferencedSymbol(IOperation operation) => operation switch
    {
        IFieldReferenceOperation field => field.Field.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
        IPropertyReferenceOperation property => property.Property.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
        IParameterReferenceOperation parameter => parameter.Parameter.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
        ILocalReferenceOperation local => local.Local.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat),
        _ => operation.Type?.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat) ?? "<unknown>"
    };
}
