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
    int SourceLine,
    IReadOnlyList<string>? BodyEffects = null,
    IReadOnlyList<CycleFingerprint>? Cycles = null);

public sealed record CycleElementFingerprint(
    string CycleId,
    string Id,
    string? Importance,
    string? Repeat,
    string? Predicate,
    IReadOnlyList<string> BodyEffects,
    string SourcePath,
    int SourceLine);

public sealed record CycleFingerprint(
    string Id,
    string? Importance,
    string? Repeat,
    string? Predicate,
    IReadOnlyList<string> BodyEffects,
    IReadOnlyList<CycleElementFingerprint> Elements,
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
                CanonicalDslExpression(declaration.Explanation),
                CanonicalDslExpression(declaration.Condition),
                source.Path,
                declaration.Span.Line,
                ExtractDslHandlerEffects(declaration.Body),
                ExtractDslCyclesFromStatements(declaration.Body, source.Path)));
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
            Metadata("explanation", csharp.Explanation, dsl.Explanation, "MissingExplanation", "AddedExplanation", "ChangedExplanation"),
            Metadata("possible-when", csharp.PossibleWhen, dsl.PossibleWhen, "MissingHandlerMetadata", "AddedHandlerMetadata", "ChangedHandlerMetadata")
        };
        if (csharp.BodyEffects is not null && dsl.BodyEffects is not null)
            checks.Add(SequenceCheck("body", csharp.BodyEffects, dsl.BodyEffects));
        if (csharp.Cycles is not null && dsl.Cycles is not null)
            checks.Add(CompareCycles(csharp.Cycles, dsl.Cycles));
        return new(csharp, dsl, checks);
    }

    public static VerificationCheck CompareHelperMethod(MethodDeclarationSyntax method, DslSource dslSource)
    {
        var parsed = DslParser.Parse(dslSource);
        var function = parsed.Document.Declarations.OfType<FunctionDeclaration>()
            .FirstOrDefault(x => x.Name == method.Identifier.ValueText);
        if (function is null)
            return new("helper", EquivalenceStatus.Fail, "generated helper declaration is missing");
        if (method.Body is null)
            return new("helper", EquivalenceStatus.Inconclusive, "helper has no block body");
        var csharpEffects = new List<string>();
        CollectCSharpHandlerEffects(method.Body.Statements, csharpEffects);
        return SequenceCheck("helper body", csharpEffects, ExtractDslHandlerEffects(function.Body));
    }

    public static IReadOnlyList<HandlerEquivalenceResult> VerifyHandlerRegistrations(string csharpPath, string csharpText, DslSource dslSource)
    {
        var tree = CSharpSyntaxTree.ParseText(csharpText, path: csharpPath);
        var unknown = tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>()
            .Where(x => InvocationName(x).StartsWith("addHandler", StringComparison.Ordinal) && RegistrationKind(x) is null).ToArray();
        if (unknown.Length != 0)
            return unknown.Select(x => new HandlerEquivalenceResult(null, null, new[] { new VerificationCheck("registration", EquivalenceStatus.Inconclusive,
                $"Unverifiable handler registration '{InvocationName(x)}' at line {x.GetLocation().GetLineSpan().StartLinePosition.Line + 1}.") })).ToArray();
        var parsed = DslParser.Parse(dslSource);
        if (parsed.Diagnostics.Count != 0)
            return new[] { new HandlerEquivalenceResult(null, null, new[] { new VerificationCheck("registration", EquivalenceStatus.Inconclusive,
                "Unverifiable SEG declaration/statement: " + string.Join("; ", parsed.Diagnostics.Select(x => x.Message))) }) };
        var csharp = ExtractCSharpRegistrations(csharpPath, csharpText);
        var dsl = ExtractDslRegistrations(dslSource);
        var results = new List<HandlerEquivalenceResult>();
        var used = new HashSet<int>();
        var matches = new List<int>();
        for (var i = 0; i < csharp.Count; i++)
        {
            var match = Enumerable.Range(0, dsl.Count).Where(j => !used.Contains(j) && SameRegistrationIdentity(csharp[i], dsl[j])).Cast<int?>().FirstOrDefault();
            if (match is null)
            {
                results.Add(new(csharp[i], null, new[] { new VerificationCheck("registration", EquivalenceStatus.Fail, "MissingHandlerRegistration") }));
                matches.Add(-1);
                continue;
            }
            used.Add(match.Value); matches.Add(match.Value); results.Add(CompareRegistration(csharp[i], dsl[match.Value]));
        }
        for (var j = 0; j < dsl.Count; j++)
            if (!used.Contains(j)) results.Add(new(null, dsl[j], new[] { new VerificationCheck("registration", EquivalenceStatus.Fail, "AddedHandlerRegistration") }));
        var matchedOrder = matches.Where(x => x >= 0).ToArray();
        if (!matchedOrder.SequenceEqual(matchedOrder.OrderBy(x => x)))
            results.Add(new(null, null, new[] { new VerificationCheck("registration order", EquivalenceStatus.Fail, "OrderMismatch") }));
        return results;
    }

    private static bool SameRegistrationIdentity(HandlerRegistrationFingerprint left, HandlerRegistrationFingerprint right)
        => left.Kind == right.Kind && left.First == right.First && left.SecondOrTarget == right.SecondOrTarget;

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
                $"{SequenceMismatch("operations", csharp.OrderedOperations, dsl.OrderedOperations)}; {SequenceMismatch("strings", csharp.Strings, dsl.Strings)}");
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
        var possibleExpression = args.FirstOrDefault(x => x.NameColon?.Name.Identifier.ValueText == "isPossibleNow")?.Expression;
        var possible = CanonicalCSharpExpression(possibleExpression is AnonymousFunctionExpressionSyntax possibleLambda
            ? possibleLambda.Body.ToString() : possibleExpression?.ToString());
        var lambda = HandlerLambda(invocation);
        var body = lambda is { } handlerLambda ? ExtractCSharpHandlerEffects(handlerLambda) : Array.Empty<string>();
        var cycles = lambda is { } cycleLambda ? ExtractCSharpCyclesFromStatements(cycleLambda, path) : Array.Empty<CycleFingerprint>();
        return new(kind, OperandText(args, 0), kind == "combine" ? OperandText(args, 1) : kind == "use-for" ? OperandText(args, 1) : null,
            LiteralText(phrase), CanonicalCSharpExpression(explanation), possible, path,
            invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1, body, cycles);
    }

    private static AnonymousFunctionExpressionSyntax? HandlerLambda(InvocationExpressionSyntax invocation)
        => invocation.ArgumentList.Arguments.Select(x => x.Expression).OfType<AnonymousFunctionExpressionSyntax>().LastOrDefault();

    public static IReadOnlyList<CycleFingerprint> ExtractCSharpCycles(string path, string text)
    {
        var tree = CSharpSyntaxTree.ParseText(text, path: path);
        return ExtractCSharpCyclesFromStatements(tree.GetRoot().DescendantNodes().OfType<MethodDeclarationSyntax>()
            .SelectMany(x => x.Body?.Statements ?? Enumerable.Empty<StatementSyntax>()), path);
    }

    private static IReadOnlyList<CycleFingerprint> ExtractCSharpCyclesFromStatements(AnonymousFunctionExpressionSyntax lambda, string path)
        => lambda.Body is BlockSyntax block ? ExtractCSharpCyclesFromStatements(block.Statements, path) : Array.Empty<CycleFingerprint>();

    private static IReadOnlyList<CycleFingerprint> ExtractCSharpCyclesFromStatements(IEnumerable<StatementSyntax> statements, string path)
    {
        var cycles = new List<CycleFingerprint>();
        var allInvocations = statements.SelectMany(x => x.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()).ToArray();
        var starts = allInvocations.Where(x => InvocationName(x) == "startCycle").ToArray();
        foreach (var invocation in starts)
        {
            var args = invocation.ArgumentList.Arguments;
            var lambdas = args.Select(x => x.Expression).OfType<AnonymousFunctionExpressionSyntax>().ToArray();
            var id = OperandText(args, 0);
            var importance = EnumMetadata(args, "Importance", "Important");
            var repeat = EnumMetadata(args, "Repeat", "OnlyOnce", "Forever");
            var predicate = lambdas.Length > 1 ? CanonicalLambdaBody(lambdas[0], "$cycleElement") : null;
            var body = lambdas.Length == 0 ? Array.Empty<string>() : ExtractCSharpHandlerEffects(lambdas[^1]);
            var variable = invocation.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault()?.Identifier.ValueText ?? id;
            var nextSameVariable = starts.FirstOrDefault(x => x.Span.Start > invocation.Span.Start
                && (x.Ancestors().OfType<VariableDeclaratorSyntax>().FirstOrDefault()?.Identifier.ValueText ?? OperandText(x.ArgumentList.Arguments, 0)) == variable);
            var elements = allInvocations
                .Where(x => InvocationName(x) == "addToCycle" && IsCycleReceiver(x, invocation, variable))
                .Where(x => IsFluentCycleAdd(x, invocation)
                    || (x.Span.Start > invocation.Span.Start
                        && (nextSameVariable is null || x.Span.Start < nextSameVariable.Span.Start)))
                // Roslyn's descendant walk visits the outermost invocation of
                // a fluent chain first. Runtime chain order is the order in
                // which each invocation closes, i.e. increasing Span.End.
                .OrderBy(x => x.Span.End)
                .Select(x => CSharpCycleElement(x, path)).ToArray();
            cycles.Add(new CycleFingerprint(id, importance, repeat, predicate, body, elements, path,
                invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1));
        }
        return cycles;
    }

    private static bool IsCycleReceiver(InvocationExpressionSyntax add, InvocationExpressionSyntax start, string variable)
    {
        if (add.Expression is not MemberAccessExpressionSyntax member) return false;
        var receiver = member.Expression;
        while (receiver is InvocationExpressionSyntax invocation)
        {
            if (invocation == start) return true;
            if (invocation.Expression is not MemberAccessExpressionSyntax fluent) break;
            receiver = fluent.Expression;
        }
        return receiver == start || receiver is IdentifierNameSyntax identifier && identifier.Identifier.ValueText == variable;
    }

    private static bool IsFluentCycleAdd(InvocationExpressionSyntax add, InvocationExpressionSyntax start)
    {
        if (add.Expression is not MemberAccessExpressionSyntax member
            || member.Expression is not InvocationExpressionSyntax receiver)
            return false;
        while (true)
        {
            if (receiver == start) return true;
            if (receiver.Expression is not MemberAccessExpressionSyntax nested
                || nested.Expression is not InvocationExpressionSyntax next)
                return false;
            receiver = next;
        }
    }

    private static string? EnumMetadata(SeparatedSyntaxList<ArgumentSyntax> args, string enumType, params string[] supported)
    {
        var expression = args.Select(x => x.Expression).OfType<MemberAccessExpressionSyntax>()
            .FirstOrDefault(x => x.Expression.ToString() == enumType);
        if (expression is null) return null;
        var value = expression.Name.Identifier.ValueText;
        return supported.Contains(value, StringComparer.Ordinal) ? value : "unverifiable:" + enumType + "." + value;
    }

    private static CycleElementFingerprint CSharpCycleElement(InvocationExpressionSyntax invocation, string path)
    {
        var args = invocation.ArgumentList.Arguments;
        var lambdas = args.Select(x => x.Expression).OfType<AnonymousFunctionExpressionSyntax>().ToArray();
        var importance = EnumMetadata(args, "Importance", "Important");
        var repeat = EnumMetadata(args, "Repeat", "OnlyOnce", "Forever");
        var predicate = lambdas.Length > 1 ? CanonicalLambdaBody(lambdas[0], "$cycleElement") : null;
        var body = lambdas.Length == 0 ? Array.Empty<string>() : ExtractCSharpHandlerEffects(lambdas[^1]);
        return new CycleElementFingerprint(invocation.Expression is MemberAccessExpressionSyntax member ? member.Expression.ToString() : "", OperandText(args, 0), importance, repeat, predicate, body, path,
            invocation.GetLocation().GetLineSpan().StartLinePosition.Line + 1);
    }

    public static IReadOnlyList<CycleFingerprint> ExtractDslCycles(DslSource source)
    {
        var parsed = DslParser.Parse(source);
        var statements = parsed.Document.Declarations.SelectMany(x => DeclarationStatements(x)).SelectMany(x => x)
            .SelectMany(FlattenDslStatements).OrderBy(x => x.Span.Start).ToArray();
        return ExtractScopedDslCycles(statements, source.Path);
    }

    public static VerificationCheck CompareCycles(IReadOnlyList<CycleFingerprint> csharp, IReadOnlyList<CycleFingerprint> dsl)
    {
        if (csharp.Count != dsl.Count) return new("cycles", EquivalenceStatus.Fail, $"cycle count C#={csharp.Count} DSL={dsl.Count}");
        for (var i = 0; i < csharp.Count; i++)
        {
            var a = csharp[i]; var b = dsl[i];
            if (new[] { a.Importance, a.Repeat, b.Importance, b.Repeat }.Any(x => x?.StartsWith("unverifiable:", StringComparison.Ordinal) == true))
                return new("cycle metadata", EquivalenceStatus.Inconclusive, "Unverifiable cycle Importance/Repeat value.");
            if (a.Id != b.Id || a.Importance != b.Importance || a.Repeat != b.Repeat || NormalizeCyclePredicate(a.Predicate) != NormalizeCyclePredicate(b.Predicate))
                return new("cycle", EquivalenceStatus.Fail, $"C#={a.Id}|{a.Importance}|{a.Repeat}|{a.Predicate}; DSL={b.Id}|{b.Importance}|{b.Repeat}|{b.Predicate}");
            var body = SequenceCheck("cycle body", a.BodyEffects, b.BodyEffects); if (body.Status != EquivalenceStatus.Pass)
                return body with { Detail = $"cycle '{a.Id}': {body.Detail}" };
            if (a.Elements.Count != b.Elements.Count)
                return new("cycle elements", EquivalenceStatus.Fail,
                    $"cycle element count mismatch C#={a.Elements.Count}[{string.Join(",", a.Elements.Select(x => x.Id))}] DSL={b.Elements.Count}[{string.Join(",", b.Elements.Select(x => x.Id))}]");
            for (var j = 0; j < a.Elements.Count; j++)
            {
                var x = a.Elements[j]; var y = b.Elements[j];
                if (x.Id != y.Id || x.Importance != y.Importance || x.Repeat != y.Repeat || NormalizeCyclePredicate(x.Predicate) != NormalizeCyclePredicate(y.Predicate))
                    return new("cycle element", EquivalenceStatus.Fail, $"element #{j + 1} mismatch C#={x.Id}|{x.Importance}|{x.Repeat}|{x.Predicate}; DSL={y.Id}|{y.Importance}|{y.Repeat}|{y.Predicate}");
                var effects = SequenceCheck($"cycle element #{j + 1} body", x.BodyEffects, y.BodyEffects); if (effects.Status != EquivalenceStatus.Pass)
                    return effects with { Detail = $"cycle '{a.Id}', element '{x.Id}': {effects.Detail}" };
            }
        }
        return new("cycles", EquivalenceStatus.Pass);
    }

    private static string? NormalizeCyclePredicate(string? value)
        => value?.Replace("$cycleElement", "it", StringComparison.Ordinal);

    private static IReadOnlyList<CycleFingerprint> ExtractDslCyclesFromStatements(IEnumerable<DslStatement> input, string path)
    {
        var statements = input.SelectMany(FlattenDslStatements).OrderBy(x => x.Span.Start).ToArray();
        return ExtractScopedDslCycles(statements, path);
    }

    private static IReadOnlyList<CycleFingerprint> ExtractScopedDslCycles(IReadOnlyList<DslStatement> statements, string path)
    {
        var starts = statements.OfType<VariableDeclaration>()
            .Where(x => ExpressionText(x.Initializer) == "new-cycle")
            .OrderBy(x => x.Span.Start).ToArray();
        var result = new List<CycleFingerprint>();
        foreach (var start in starts)
        {
            var nextSameName = starts.FirstOrDefault(x => x.Span.Start > start.Span.Start && x.Name == start.Name);
            var adds = statements.OfType<AddCycleElementStatement>()
                .Where(x => x.Cycle == start.Name && x.Span.Start > start.Span.Start
                    && (nextSameName is null || x.Span.Start < nextSameName.Span.Start))
                .ToArray();
            result.Add(DslCycleFingerprint(start.Name, adds, path, start.Span.Line));
        }
        return result;
    }

    private static CycleFingerprint DslCycleFingerprint(string cycleName, IReadOnlyList<AddCycleElementStatement> all, string path, int line)
    {
        var initial = all.FirstOrDefault();
        var elements = all.Skip(1).Select(x => new CycleElementFingerprint(cycleName, x.Id, x.Important ? "Important" : null,
            x.Repeat is null ? null : x.Repeat == "once" ? "OnlyOnce" : "Forever", CanonicalCyclePredicate(x.Condition),
            ExtractDslHandlerEffects(x.Body), path, x.Span.Line)).ToArray();
        return new CycleFingerprint(initial?.Id ?? cycleName, initial?.Important == true ? "Important" : null,
            initial?.Repeat is null ? null : initial.Repeat == "once" ? "OnlyOnce" : "Forever", CanonicalCyclePredicate(initial?.Condition),
            initial is null ? Array.Empty<string>() : ExtractDslHandlerEffects(initial.Body), elements, path, line);
    }

    private static string? CanonicalCyclePredicate(DslExpression? expression)
    {
        var value = CanonicalDslExpression(expression);
        if (value is null) return null;
        value = System.Text.RegularExpressions.Regex.Replace(value, "not-seen-recently\\(it,(.*?)\\)", "$cycleElement.notSeenRecently($1)", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
        return System.Text.RegularExpressions.Regex.Replace(value, "\\bit\\.", "$cycleElement.", System.Text.RegularExpressions.RegexOptions.CultureInvariant);
    }

    private static IEnumerable<IEnumerable<DslStatement>> DeclarationStatements(DslDeclaration declaration)
    {
        return declaration switch
        {
            FunctionDeclaration x => new[] { x.Body },
            HandlerDeclaration x => new[] { x.Body },
            BeforeRoomChangeDeclaration x => new[] { x.Body },
            AfterActionExecutedDeclaration x => new[] { x.Body },
            CycleElementDeclaration x => new[] { x.Body },
            _ => Array.Empty<IEnumerable<DslStatement>>()
        };
    }

    private static IEnumerable<DslStatement> FlattenDslStatements(DslStatement statement)
    {
        yield return statement;
        switch (statement)
        {
            case IfStatement conditional:
                foreach (var branch in conditional.Branches.SelectMany(x => x.Body).SelectMany(FlattenDslStatements)) yield return branch;
                if (conditional.ElseBody is not null) foreach (var child in conditional.ElseBody.SelectMany(FlattenDslStatements)) yield return child;
                break;
            case AddCycleElementStatement cycle:
                foreach (var child in cycle.Body.SelectMany(FlattenDslStatements)) yield return child;
                break;
            case NamedCutsceneStatement named:
                foreach (var child in named.Body.SelectMany(FlattenDslStatements)) yield return child;
                break;
        }
    }

    private static IReadOnlyList<string> ExtractCSharpHandlerEffects(AnonymousFunctionExpressionSyntax lambda)
    {
        if (lambda.Body is not BlockSyntax block)
            return new[] { "unverifiable:expression-bodied-lambda" };
        var effects = new List<string>();
        CollectCSharpHandlerEffects(block.Statements, effects);
        return effects;
    }

    private static void CollectCSharpHandlerEffects(IEnumerable<StatementSyntax> statements, List<string> effects)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case IfStatementSyntax conditional:
                    CollectCSharpConditionalChain(conditional, effects, true);
                    break;
                case ExpressionStatementSyntax expression when expression.Expression is AssignmentExpressionSyntax assignment:
                    var target = assignment.Left.ToString();
                    if (target.EndsWith("makesNoSenseAtThisTime", StringComparison.Ordinal) && assignment.Right.ToString() == "true") effects.Add("makes-no-sense");
                    else if (target.EndsWith("textInputToShow", StringComparison.Ordinal)) effects.Add("text-input:" + CanonicalCSharpExpression(assignment.Right.ToString()));
                    else effects.Add("assign:" + CanonicalCSharpExpression(assignment.Left + assignment.OperatorToken.Text + assignment.Right));
                    break;
                case ExpressionStatementSyntax expression when expression.Expression is PostfixUnaryExpressionSyntax postfix:
                    effects.Add("increment:" + postfix.Operand.ToString());
                    break;
                case ExpressionStatementSyntax expression when expression.Expression is PrefixUnaryExpressionSyntax prefix
                    && prefix.OperatorToken.IsKind(SyntaxKind.PlusPlusToken):
                    effects.Add("increment:" + prefix.Operand.ToString());
                    break;
                case ExpressionStatementSyntax expression when expression.Expression is PostfixUnaryExpressionSyntax postfix
                    && postfix.OperatorToken.IsKind(SyntaxKind.PlusPlusToken):
                    effects.Add("increment:" + postfix.Operand.ToString());
                    break;
                case ExpressionStatementSyntax expression when expression.Expression is InvocationExpressionSyntax invocation:
                    if (InvocationName(invocation) == "addToCycle")
                        AddCSharpCycleElementEffects(invocation, effects);
                    else effects.Add(CSharpInvocationEffect(invocation));
                    break;
                case LocalDeclarationStatementSyntax local:
                    foreach (var variable in local.Declaration.Variables)
                        if (variable.Initializer?.Value is InvocationExpressionSyntax initializer && FindStartCycle(initializer) is { } start)
                        {
                            effects.Add("assign:" + variable.Identifier.ValueText + "=new-cycle");
                            AddCSharpCycleElementEffects(start, effects, variable.Identifier.ValueText);
                            foreach (var add in initializer.DescendantNodesAndSelf().OfType<InvocationExpressionSyntax>()
                                         .Where(x => InvocationName(x) == "addToCycle").OrderBy(x => x.Span.End))
                                AddCSharpCycleElementEffects(add, effects, variable.Identifier.ValueText);
                        }
                        else if (variable.Initializer is not null) effects.Add("assign:" + variable.Identifier.ValueText + "=" + CanonicalCSharpExpression(variable.Initializer.Value.ToString()));
                    break;
                case UsingStatementSyntax usingStatement when usingStatement.Expression is InvocationExpressionSyntax namedCutscene && InvocationName(namedCutscene) == "namedCutScene":
                    effects.Add(CSharpNamedCutsceneEffect(namedCutscene, usingStatement.Statement));
                    break;
                case ReturnStatementSyntax ret: effects.Add("return:" + CanonicalCSharpExpression(ret.Expression?.ToString())); break;
                default: effects.Add("unverifiable:" + statement.GetType().Name + ":" + statement.ToString()); break;
            }
        }
    }

    private static string CSharpInvocationEffect(InvocationExpressionSyntax invocation)
    {
        var name = InvocationName(invocation);
        if (name == "setIfNeverHappened" && invocation.ArgumentList.Arguments.Count == 1)
            return "mark-happened-once:" + CanonicalCSharpExpression(invocation.ArgumentList.Arguments[0].Expression.ToString());
        if (name is "finishGame" or "finish") return "finish-game";
        if (name is "doNotAdvanceTime") return "do-not-advance-time";
        if (name is "preventRoomChange") return "prevent-room-change";
        if (name == "execNextInCycle" && invocation.ArgumentList.Arguments.Count == 1)
            return "cycle:next:" + CanonicalCSharpExpression(invocation.ArgumentList.Arguments[0].Expression.ToString());
        if (name == "dial" && invocation.ArgumentList.Arguments.Count >= 2)
            return "dialogue:" + CanonicalCSharpExpression(invocation.ArgumentList.Arguments[0].Expression.ToString()) + ":" + LiteralText(invocation.ArgumentList.Arguments[1].Expression.ToString());
        if (name is "nar" or "narText" or "narRoom" or "narImg")
            return "narration-call:" + CanonicalCSharpCall(invocation);
        return "call:" + CanonicalCSharpExpression(invocation.ToString());
    }

    private static void AddCSharpCycleElementEffects(InvocationExpressionSyntax invocation, List<string> effects, string? cycleName = null)
    {
        var cycle = cycleName ?? (invocation.Expression as MemberAccessExpressionSyntax)?.Expression.ToString() ?? "cyc";
        var id = OperandText(invocation.ArgumentList.Arguments, 0);
        effects.Add("cycle:add:" + cycle + ":" + id);
        var lambdas = invocation.ArgumentList.Arguments.Select(x => x.Expression).OfType<AnonymousFunctionExpressionSyntax>().ToArray();
        if (lambdas.Length != 0) CollectCSharpHandlerEffects((lambdas[^1].Body as BlockSyntax)?.Statements ?? Enumerable.Empty<StatementSyntax>(), effects);
    }

    private static InvocationExpressionSyntax? FindStartCycle(InvocationExpressionSyntax invocation)
    {
        var current = invocation;
        while (true)
        {
            if (InvocationName(current) == "startCycle") return current;
            if (current.Expression is not MemberAccessExpressionSyntax member || member.Expression is not InvocationExpressionSyntax next) return null;
            current = next;
        }
    }

    private static void CollectCSharpConditionalChain(IfStatementSyntax conditional, List<string> effects, bool first)
    {
        effects.Add((first ? "if:" : "elif:") + CanonicalCSharpExpression(conditional.Condition.ToString()));
        CollectCSharpHandlerEffects(conditional.Statement is BlockSyntax block ? block.Statements : new[] { conditional.Statement }, effects);
        if (conditional.Else?.Statement is IfStatementSyntax nested)
            CollectCSharpConditionalChain(nested, effects, false);
        else if (conditional.Else?.Statement is { } elseStatement)
        {
            effects.Add("else");
            CollectCSharpHandlerEffects(elseStatement is BlockSyntax elseBlock ? elseBlock.Statements : new[] { elseStatement }, effects);
        }
    }

    private static string CycleInvocationFingerprint(InvocationExpressionSyntax invocation)
    {
        var args = invocation.ArgumentList.Arguments;
        var lambdas = args.Select(x => x.Expression).OfType<AnonymousFunctionExpressionSyntax>().ToArray();
        var importance = EnumMetadata(args, "Importance", "Important") ?? "-";
        var repeat = EnumMetadata(args, "Repeat", "OnlyOnce", "Forever") ?? "-";
        var predicate = lambdas.Length > 1 ? CanonicalLambdaBody(lambdas[0], "$cycleElement") : "-";
        var body = lambdas.Length == 0 ? Array.Empty<string>() : ExtractCSharpHandlerEffects(lambdas[^1]);
        return CanonicalCSharpExpression(OperandText(args, 0)) + "|importance=" + importance + "|repeat=" + repeat + "|predicate=" + predicate + "|body=[" + string.Join(";", body) + "]";
    }

    private static string CycleElementInvocationFingerprint(InvocationExpressionSyntax invocation)
    {
        var args = invocation.ArgumentList.Arguments;
        var lambdas = args.Select(x => x.Expression).OfType<AnonymousFunctionExpressionSyntax>().ToArray();
        var importance = EnumMetadata(args, "Importance", "Important") ?? "-";
        var repeat = EnumMetadata(args, "Repeat", "OnlyOnce", "Forever") ?? "-";
        var predicate = lambdas.Length > 1 ? CanonicalLambdaBody(lambdas[0], "$cycleElement") : "-";
        var body = lambdas.Length == 0 ? Array.Empty<string>() : ExtractCSharpHandlerEffects(lambdas[^1]);
        return CanonicalCSharpExpression(OperandText(args, 0)) + "|importance=" + importance + "|repeat=" + repeat + "|predicate=" + predicate + "|body=[" + string.Join(";", body) + "]";
    }

    private static string CSharpNamedCutsceneEffect(InvocationExpressionSyntax invocation, StatementSyntax statement)
    {
        var args = invocation.ArgumentList.Arguments.Select(x => CanonicalCSharpExpression(x.Expression.ToString()) ?? "").ToArray();
        var body = Array.Empty<string>();
        if (statement is BlockSyntax block)
        {
            var collected = new List<string>();
            CollectCSharpHandlerEffects(block.Statements, collected);
            body = collected.ToArray();
        }
        return "named-cutscene:" + (args.FirstOrDefault() ?? "") + "|args=[" + string.Join(",", args.Skip(1)) + "]|body=[" + string.Join(";", body) + "]";
    }

    private static string? CanonicalLambdaBody(AnonymousFunctionExpressionSyntax lambda, string role)
    {
        var parameter = lambda switch
        {
            SimpleLambdaExpressionSyntax simple => simple.Parameter.Identifier.ValueText,
            ParenthesizedLambdaExpressionSyntax parenthesized => parenthesized.ParameterList.Parameters.FirstOrDefault()?.Identifier.ValueText,
            _ => null
        };
        var body = lambda.Body.ToString();
        if (!string.IsNullOrEmpty(parameter)) body = System.Text.RegularExpressions.Regex.Replace(body, $"\\b{System.Text.RegularExpressions.Regex.Escape(parameter)}\\b", role);
        return CanonicalCSharpExpression(body);
    }

    private static IReadOnlyList<string> ExtractDslHandlerEffects(IEnumerable<DslStatement> statements)
    {
        var effects = new List<string>();
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case IfStatement conditional:
                    for (var i = 0; i < conditional.Branches.Count; i++)
                    {
                        effects.Add((i == 0 ? "if:" : "elif:") + CanonicalDslExpression(conditional.Branches[i].Condition));
                        effects.AddRange(ExtractDslHandlerEffects(conditional.Branches[i].Body));
                    }
                    if (conditional.ElseBody is not null) { effects.Add("else"); effects.AddRange(ExtractDslHandlerEffects(conditional.ElseBody)); }
                    break;
                case MakesNoSenseStatement: effects.Add("makes-no-sense"); break;
                case TextInputStatement input: effects.Add("text-input:" + CanonicalDslExpression(input.TextInput)); break;
                case VariableDeclaration variable: effects.Add("assign:" + variable.Name + "=" + CanonicalDslExpression(variable.Initializer)); break;
                case IncrementStatement increment: effects.Add("increment:" + increment.Name); break;
                case AssignmentStatement assignment:
                    effects.Add("assign:" + (assignment.Receiver is null ? assignment.Name : CanonicalDslExpression(assignment.Receiver) + "." + assignment.MemberName) + assignment.Operator + CanonicalDslExpression(assignment.Value)); break;
                case DialogueStatement dialogue: effects.Add("dialogue:" + dialogue.Character + ":" + LiteralDslText(dialogue.Text)); break;
                case NarStatement nar: effects.Add("narration-call:narText(s=" + QuoteCanonicalString(LiteralDslText(nar.Text)) + ")"); break;
                case NarRoomStatement narRoom: effects.Add("narration-call:narRoom(s=" + QuoteCanonicalString(LiteralDslText(narRoom.Text)) + ")"); break;
                case NarImgStatement narImg: effects.Add("narration-call:narImg(s=" + QuoteCanonicalString(LiteralDslText(narImg.Text)) + ")"); break;
                case CallStatement call:
                    if (call.Expression is CallExpression narrative
                        && narrative.Name is "nar" or "narText" or "narRoom" or "narImg")
                        effects.Add("narration-call:" + CanonicalDslCall(narrative));
                    else
                        effects.Add("call:" + CanonicalDslExpression(call.Expression));
                    break;
                case ReturnStatement ret: effects.Add("return:" + CanonicalDslExpression(ret.Expression)); break;
                case NextCycleStatement next: effects.Add("cycle:next:" + CanonicalDslExpression(next.Cycle)); break;
                case AddCycleElementStatement cycle:
                    effects.Add("cycle:add:" + cycle.Cycle + ":" + cycle.Id);
                    effects.AddRange(ExtractDslHandlerEffects(cycle.Body));
                    break;
                case MarkHappenedOnceStatement mark: effects.Add("mark-happened-once:" + CanonicalDslExpression(mark.Target)); break;
                case MarkHappenedStatement mark: effects.Add("mark-happened:" + CanonicalDslExpression(mark.Target)); break;
                case FinishGameStatement: effects.Add("finish-game"); break;
                case DoNotAdvanceTimeStatement: effects.Add("do-not-advance-time"); break;
                case PreventRoomChangeStatement: effects.Add("prevent-room-change"); break;
                case NamedCutsceneStatement named:
                    effects.Add("named-cutscene:" + named.Id + "|args=[" + string.Join(",", named.Arguments.Select(CanonicalDslExpression)) + "]|body=[" + string.Join(";", ExtractDslHandlerEffects(named.Body)) + "]");
                    break;
                default: effects.Add("unverifiable:" + statement.GetType().Name); break;
            }
        }
        return effects;
    }

    private static VerificationCheck Metadata(string name, string? left, string? right, string missing, string added, string changed)
        => left == null && right == null ? new(name, EquivalenceStatus.Pass) :
            left == null ? new(name, EquivalenceStatus.Fail, added) : right == null ? new(name, EquivalenceStatus.Fail, missing) :
            left == right ? new(name, EquivalenceStatus.Pass) : new(name, EquivalenceStatus.Fail, changed + $": C#='{left}' DSL='{right}'");

    private static VerificationCheck SequenceCheck(string name, IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.Any(x => x.StartsWith("unverifiable:", StringComparison.Ordinal)) || right.Any(x => x.StartsWith("unverifiable:", StringComparison.Ordinal)))
            return new(name, EquivalenceStatus.Inconclusive, string.Join("; ", left.Concat(right).Where(x => x.StartsWith("unverifiable:", StringComparison.Ordinal))));
        if (left.SequenceEqual(right, StringComparer.Ordinal)) return new(name, EquivalenceStatus.Pass);
        var common = Math.Min(left.Count, right.Count);
        var detail = Enumerable.Range(0, common).Where(i => left[i] != right[i]).Select(i => $"effect #{i + 1}: C#='{left[i]}' DSL='{right[i]}'").ToList();
        if (left.Count > right.Count) detail.Add($"missing effects={left.Count - right.Count}");
        if (right.Count > left.Count) detail.Add($"added effects={right.Count - left.Count}");
        return new(name, EquivalenceStatus.Fail, string.Join("; ", detail));
    }

    private static string SequenceMismatch(string name, IReadOnlyList<string> left, IReadOnlyList<string> right)
    {
        if (left.SequenceEqual(right, StringComparer.Ordinal)) return name + " match";
        var common = Math.Min(left.Count, right.Count);
        var index = Enumerable.Range(0, common).FirstOrDefault(i => left[i] != right[i], -1);
        var first = index >= 0
            ? $"first difference at #{index + 1}: C#='{left[index]}' DSL='{right[index]}'"
              + (index > 0 ? $"; previous C#='{left[index - 1]}' DSL='{right[index - 1]}'" : "")
            : "no common differing item";
        return $"{name} mismatch ({first}; C# count={left.Count}, DSL count={right.Count})";
    }

    private static string? CanonicalCSharpExpression(string? expression)
    {
        if (expression is null) return null;
        expression = StripCSharpComments(expression);
        try
        {
            var syntax = Microsoft.CodeAnalysis.CSharp.SyntaxFactory.ParseExpression(expression);
            if (!syntax.ContainsDiagnostics)
                expression = syntax.WithoutTrivia().ToFullString();
        }
        catch
        {
            // Keep the lexical fallback below for intentionally partial expressions.
        }
        var builder = new System.Text.StringBuilder(); var quoted = false;
        foreach (var ch in expression)
        {
            if (ch == '"') quoted = !quoted;
            if (!char.IsWhiteSpace(ch) || quoted) builder.Append(ch);
        }
        var result = builder.ToString().Replace("&&", "and", StringComparison.Ordinal).Replace("||", "or", StringComparison.Ordinal)
            .Replace("!=", "<>", StringComparison.Ordinal).Replace("!", "not", StringComparison.Ordinal).Replace("<>", "!=", StringComparison.Ordinal)
            .Replace("()", "", StringComparison.Ordinal);
        return System.Text.RegularExpressions.Regex.Replace(result, @"([A-Za-z_]\w*):", "$1=");
    }

    private static string StripCSharpComments(string expression)
    {
        var builder = new System.Text.StringBuilder(expression.Length);
        var inString = false;
        var inLineComment = false;
        var inBlockComment = false;
        for (var i = 0; i < expression.Length; i++)
        {
            var ch = expression[i];
            var next = i + 1 < expression.Length ? expression[i + 1] : '\0';
            if (inLineComment)
            {
                if (ch is '\r' or '\n') { inLineComment = false; builder.Append(ch); }
                continue;
            }
            if (inBlockComment)
            {
                if (ch == '*' && next == '/') { inBlockComment = false; i++; }
                continue;
            }
            if (!inString && ch == '/' && next == '/') { inLineComment = true; i++; continue; }
            if (!inString && ch == '/' && next == '*') { inBlockComment = true; i++; continue; }
            builder.Append(ch);
            if (ch == '"' && (i == 0 || expression[i - 1] != '\\')) inString = !inString;
        }
        return builder.ToString();
    }

    private static string? CanonicalDslExpression(DslExpression? expression) => CanonicalCSharpExpression(ExpressionText(expression)) ?? null;
    private static string LiteralDslText(DslExpression expression) => StringValue(expression) ?? CanonicalDslExpression(expression) ?? "";

    private static string CanonicalDslCall(CallExpression call)
    {
        var receiver = call.Receiver is null ? call.Name : ExpressionText(call.Receiver) + "." + call.Name;
        var arguments = call.Arguments.Select((argument, index) =>
        {
            var parameter = NarrativeParameterName(call.Name, index, argument.Name);
            var prefix = parameter is null ? "" : parameter + "=";
            var value = argument.Expression is LiteralExpression { Kind: "string" or "raw-string" }
                ? QuoteCanonicalString(DslLiteralTextCanonical(argument.Expression))
                : CanonicalDslExpression(argument.Expression) ?? "";
            return prefix + value;
        });
        return receiver + "(" + string.Join(",", arguments) + ")";
    }

    private static string CanonicalCSharpCall(InvocationExpressionSyntax invocation)
    {
        var name = InvocationName(invocation);
        var arguments = invocation.ArgumentList.Arguments.Select((argument, index) =>
        {
            var parameter = NarrativeParameterName(name, index, argument.NameColon?.Name.Identifier.ValueText);
            var prefix = parameter is null ? "" : parameter + "=";
            return prefix + (CanonicalCSharpExpression(argument.Expression.ToString()) ?? "");
        });
        return name + "(" + string.Join(",", arguments) + ")";
    }

    private static string? NarrativeParameterName(string name, int index, string? explicitName)
    {
        if (explicitName is not null) return explicitName;
        if (name is "nar" or "narText") return index switch { 0 => "s", 1 => "removeIfLast", _ => null };
        if (name == "narRoom") return index switch { 0 => "s", 1 => "room", 2 => "removeIfLast", 3 => "alsoShowGraphicsInTextMode", _ => explicitName };
        if (name == "narImg") return index switch { 0 => "s", 1 => "img", 2 => "size", 3 => "removeIfLast", 4 => "alsoShowGraphicsInTextMode", _ => explicitName };
        return explicitName;
    }

    private static string QuoteCanonicalString(string value)
        => "\"" + value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";

    private static string DslLiteralTextCanonical(DslExpression expression)
        => expression is LiteralExpression literal && literal.Kind is "string" or "raw-string"
            ? DslLiteralSemantics.Decode(literal)
            : LiteralDslText(expression);

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
                case NarStatement nar: operations.Add("narration-call:narText(s=" + QuoteCanonicalString(DslLiteralTextCanonical(nar.Text)) + ")"); strings.Add(DslLiteralTextCanonical(nar.Text)); break;
                case NarRoomStatement narRoom: operations.Add("narration-call:narRoom(s=" + QuoteCanonicalString(DslLiteralTextCanonical(narRoom.Text)) + ")"); strings.Add(DslLiteralTextCanonical(narRoom.Text)); break;
                case NarImgStatement narImg: operations.Add("narration-call:narImg(s=" + QuoteCanonicalString(DslLiteralTextCanonical(narImg.Text)) + ")"); strings.Add(DslLiteralTextCanonical(narImg.Text)); break;
                case PreventRoomChangeStatement: operations.Add("prevent-room-change"); break;
                case MarkHappenedOnceStatement mark: operations.Add("mark-happened-once:" + ExpressionText(mark.Target)); break;
                case MarkHappenedStatement mark: operations.Add("mark-happened:" + ExpressionText(mark.Target)); break;
                case AssignmentStatement assignment: operations.Add("assign:" + (assignment.Receiver == null ? assignment.Name : ExpressionText(assignment.Receiver) + "." + assignment.MemberName) + assignment.Operator + ExpressionText(assignment.Value)); break;
                case IncrementStatement increment: operations.Add("increment:" + increment.Name); break;
                case CallStatement call:
                    if (call.Expression is CallExpression narrative
                        && narrative.Name is "nar" or "narText" or "narRoom" or "narImg")
                    {
                        operations.Add("narration-call:" + CanonicalDslCall(narrative));
                        if (narrative.Arguments.FirstOrDefault() is { } textArgument)
                            strings.Add(DslLiteralTextCanonical(textArgument.Expression));
                    }
                    else operations.Add("call:" + CanonicalDslExpression(call.Expression));
                    break;
                case AddCycleElementStatement: break;
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
                    else if (name is "nar" or "narText" or "narRoom" or "narImg")
                    {
                        var text = LiteralText(invocation.ArgumentList.Arguments.FirstOrDefault()?.Expression.ToString()) ?? "";
                        operations.Add("narration-call:" + CanonicalCSharpCall(invocation));
                        strings.Add(text);
                    }
                    else if (name == "preventRoomChange")
                        operations.Add("prevent-room-change");
                    else if (name is "startCycle" or "addToCycle" or "execNextInCycle")
                    {
                        // Cycles are certified separately with their ordered
                        // predicates/effects; do not compare their C# fluent
                        // spelling with SEG statements here.
                    }
                    else if (name == "setIfNeverHappened" && invocation.ArgumentList.Arguments.Count == 1)
                        operations.Add("mark-happened-once:" + invocation.ArgumentList.Arguments[0].Expression);
                    else
                        operations.Add("call:" + CanonicalCSharpExpression(invocation.ToString()));
                    break;
                case ExpressionStatementSyntax expression when expression.Expression is AssignmentExpressionSyntax assignment:
                    operations.Add("assign:" + CanonicalCSharpExpression(assignment.Left + assignment.OperatorToken.Text + assignment.Right));
                    break;
                case ExpressionStatementSyntax expression when expression.Expression is PostfixUnaryExpressionSyntax postfix
                    && postfix.OperatorToken.IsKind(SyntaxKind.PlusPlusToken):
                    operations.Add("increment:" + postfix.Operand.ToString());
                    break;
                case ExpressionStatementSyntax expression when expression.Expression is PrefixUnaryExpressionSyntax prefix
                    && prefix.OperatorToken.IsKind(SyntaxKind.PlusPlusToken):
                    operations.Add("increment:" + prefix.Operand.ToString());
                    break;
                case BlockSyntax block:
                    CollectCSharpBeforeRoomChange(block.Statements, operations, strings);
                    break;
                case IfStatementSyntax conditional:
                    CollectCSharpBeforeRoomChangeConditionalChain(conditional, operations, strings);
                    break;
            }
        }
    }

    private static void CollectCSharpBeforeRoomChangeConditionalChain(IfStatementSyntax conditional, List<string> operations, List<string> strings)
    {
        var branches = new List<IfStatementSyntax>();
        IfStatementSyntax? current = conditional;
        while (current is not null)
        {
            branches.Add(current);
            current = current.Else?.Statement as IfStatementSyntax;
        }

        operations.Add("if:" + string.Join("|", branches.Select(x => CanonicalCondition(x.Condition.ToString()))));
        foreach (var branch in branches)
            CollectCSharpBeforeRoomChange(branch.Statement is BlockSyntax body ? body.Statements : new[] { branch.Statement }, operations, strings);

        if (branches[^1].Else?.Statement is { } elseStatement)
            CollectCSharpBeforeRoomChange(elseStatement is BlockSyntax elseBlock ? elseBlock.Statements : new[] { elseStatement }, operations, strings);
    }

    private static string CanonicalCondition(string condition)
        => CanonicalCSharpExpression(condition) ?? "";

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
        ? DslLiteralSemantics.Decode(literal) : expression == null ? null : ExpressionText(expression);

    private static string? ExpressionText(DslExpression? expression) => expression switch
    {
        null => null,
        IdentifierExpression id => id.Name,
        LiteralExpression literal => literal.Kind is "string" or "raw-string" ? DslLiteralSemantics.Decode(literal) : literal.Value,
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
                result.Add("increment:" + ReferencedSymbol(increment.Target) + ":" + (increment.Kind == OperationKind.Increment ? "increment" : "decrement") + ":" + (increment.IsPostfix ? "postfix" : "prefix"));
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
