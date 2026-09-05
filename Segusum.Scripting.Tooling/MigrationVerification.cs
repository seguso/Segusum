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
                && (kind == "room-changed" || OperandText(x.ArgumentList.Arguments, 1) == secondOrTarget));
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
                && (kind == "room-changed" || OperandText(x.ArgumentList.Arguments, 1) == secondOrTarget));
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

    private static string? RegistrationKind(InvocationExpressionSyntax invocation) => InvocationName(invocation) switch
    {
        "addHandlerCombine" => "combine",
        "addHandlerUseFor" => "use-for",
        "addHandlerUseHere" => "use-here",
        "addHandlerPickUp" => "pickup",
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
