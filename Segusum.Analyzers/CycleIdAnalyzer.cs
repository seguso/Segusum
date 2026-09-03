using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Segusum.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class CycleIdAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor NonLiteral = new("SEG001", "Cycle element ID must be a string literal", "The string ID of {0} must be a direct string literal", "Segusum", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor Empty = new("SEG002", "Cycle element ID cannot be empty", "The ID of {0} cannot be empty", "Segusum", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor Duplicate = new("SEG003", "Duplicate cycle element ID", "Cycle element ID '{0}' is duplicated; first occurrence is at line {1}", "Segusum", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor DuplicateCombine = new("SEG004", "Duplicate addHandlerCombine registration", "A combine handler for '{0}' -> '{1}' is already registered; first registration is at line {2}", "Segusum", DiagnosticSeverity.Error, true);
    private static readonly DiagnosticDescriptor DuplicateUseFor = new("SEG005", "Duplicate addHandlerUseFor registration", "A use-for handler for '{0}' and objective '{1}' is already registered; first registration is at line {2}", "Segusum", DiagnosticSeverity.Error, true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(NonLiteral, Empty, Duplicate, DuplicateCombine, DuplicateUseFor);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var ids = new List<(string Id, Location Location)>();
            var handlers = new List<HandlerRegistration>();
            start.RegisterSyntaxNodeAction(c => AnalyzeInvocation(c, ids, handlers), SyntaxKind.InvocationExpression);
            start.RegisterCompilationEndAction(c => ReportDuplicates(c, ids, handlers));
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, List<(string Id, Location Location)> ids, List<HandlerRegistration> handlers)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method)
            return;

        if (IsSegusumCycleApi(method))
        {
            AnalyzeCycleId(context, invocation, method, ids);
            return;
        }

        if (IsDuplicateHandlerApi(method) &&
            context.SemanticModel.GetEnclosingSymbol(invocation.SpanStart)?.ContainingType is INamedTypeSymbol world &&
            IsWorldDerivedFromWorldBase(world) &&
            TryGetHandlerElements(context.SemanticModel, invocation, method, out var first, out var second, out var registrationLocation))
        {
            lock (handlers)
            {
                handlers.Add(new HandlerRegistration(world, first, second, method.Name, registrationLocation));
            }
        }
    }

    private static void AnalyzeCycleId(SyntaxNodeAnalysisContext context, InvocationExpressionSyntax invocation, IMethodSymbol method, List<(string Id, Location Location)> ids)
    {

        var stringParameter = method.Parameters.FirstOrDefault(p =>
            p.Type.SpecialType == SpecialType.System_String);
        if (stringParameter == null)
            return;

        // Extension-method invocations omit the receiver from ArgumentList.
        var argumentIndex = method.IsExtensionMethod
            ? (method.Parameters[0].Type.SpecialType == SpecialType.System_String
                ? stringParameter.Ordinal
                : stringParameter.Ordinal - 1)
            : stringParameter.Ordinal;
        if (argumentIndex < 0 || argumentIndex >= invocation.ArgumentList.Arguments.Count)
            return;

        var argument = invocation.ArgumentList.Arguments[argumentIndex].Expression;
        if (argument is not LiteralExpressionSyntax literal || !literal.IsKind(SyntaxKind.StringLiteralExpression))
        {
            context.ReportDiagnostic(Diagnostic.Create(NonLiteral, argument.GetLocation(), method.Name));
            return;
        }

        var id = literal.Token.ValueText;
        if (string.IsNullOrWhiteSpace(id))
        {
            context.ReportDiagnostic(Diagnostic.Create(Empty, argument.GetLocation(), method.Name));
            return;
        }

        lock (ids)
        {
            ids.Add((id, argument.GetLocation()));
        }
    }

    private static void ReportDuplicates(CompilationAnalysisContext context, List<(string Id, Location Location)> ids, List<HandlerRegistration> handlers)
    {
        var treeOrder = context.Compilation.SyntaxTrees
            .Select((tree, index) => (tree, index))
            .ToDictionary(item => item.tree, item => item.index);

        var occurrences = ids
            .OrderBy(item => treeOrder[item.Location.SourceTree!])
            .ThenBy(item => item.Location.SourceSpan.Start)
            .ToList();

        foreach (var group in occurrences.GroupBy(item => item.Id, StringComparer.Ordinal))
        {
            var first = group.First();
            var firstLine = first.Location.GetLineSpan().StartLinePosition.Line + 1;
            foreach (var duplicate in group.Skip(1))
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    Duplicate,
                    duplicate.Location,
                    duplicate.Id,
                    firstLine));
            }
        }

        var handlerOccurrences = handlers
            .OrderBy(item => treeOrder[item.Location.SourceTree!])
            .ThenBy(item => item.Location.SourceSpan.Start)
            .ToList();

        foreach (var group in handlerOccurrences.GroupBy(item => item, HandlerKeyComparer.Instance))
        {
            var first = group.First();
            var firstLine = first.Location.GetLineSpan().StartLinePosition.Line + 1;
            foreach (var duplicate in group.Skip(1))
            {
                var descriptor = first.MethodName == "addHandlerCombine" ? DuplicateCombine : DuplicateUseFor;
                context.ReportDiagnostic(Diagnostic.Create(descriptor, duplicate.Location,
                    first.First.Name, first.Second.Name, firstLine));
            }
        }
    }

    private static bool IsDuplicateHandlerApi(IMethodSymbol method)
    {
        if (method.Name is not ("addHandlerCombine" or "addHandlerUseFor")) return false;
        var type = method.ContainingType;
        if (type?.Name != "WorldBase" || type.ContainingNamespace?.ToDisplayString() != "Seg") return false;
        if (method.Parameters.Length < 2) return false;
        return method.Parameters[0].Type.ToDisplayString() == "Seg.LogicObj" &&
               method.Parameters[1].Type.ToDisplayString() == (method.Name == "addHandlerCombine" ? "Seg.LogicObj" : "Seg.Objective");
    }

    private static bool IsWorldDerivedFromWorldBase(INamedTypeSymbol type)
    {
        for (var current = type; current != null; current = current.BaseType)
            if (current.Name == "WorldBase" && current.ContainingNamespace?.ToDisplayString() == "Seg") return true;
        return false;
    }

    private static bool TryGetHandlerElements(SemanticModel model, InvocationExpressionSyntax invocation, IMethodSymbol method, out ISymbol first, out ISymbol second, out Location registrationLocation)
    {
        first = null!;
        second = null!;
        registrationLocation = invocation.GetLocation();
        var mapped = new Dictionary<int, ArgumentSyntax>();
        var used = new HashSet<int>();
        foreach (var argument in invocation.ArgumentList.Arguments)
        {
            int ordinal;
            if (argument.NameColon != null)
            {
                var namedParameter = method.Parameters.FirstOrDefault(p => p.Name == argument.NameColon.Name.Identifier.ValueText);
                if (namedParameter == null) continue;
                ordinal = namedParameter.Ordinal;
            }
            else
            {
                ordinal = 0;
                while (used.Contains(ordinal) && ordinal < method.Parameters.Length) ordinal++;
            }
            used.Add(ordinal);
            mapped[ordinal] = argument;
        }

        if (!mapped.TryGetValue(0, out var firstArg) || !mapped.TryGetValue(1, out var secondArg)) return false;
        var firstInfo = model.GetSymbolInfo(firstArg.Expression).Symbol;
        var secondInfo = model.GetSymbolInfo(secondArg.Expression).Symbol;
        if (firstInfo is not (IFieldSymbol or IPropertySymbol) || secondInfo is not (IFieldSymbol or IPropertySymbol)) return false;
        first = firstInfo;
        second = secondInfo;
        // Point at the first element of the duplicate registration. This is
        // stable for positional and named invocations alike and highlights
        // the identity-bearing argument rather than an arbitrary overload
        // parameter.
        registrationLocation = firstArg.Expression.GetLocation();
        return true;
    }

    private sealed class HandlerRegistration
    {
        public HandlerRegistration(INamedTypeSymbol world, ISymbol first, ISymbol second, string methodName, Location location)
        {
            World = world; First = first; Second = second; MethodName = methodName; Location = location;
        }
        public INamedTypeSymbol World { get; }
        public ISymbol First { get; }
        public ISymbol Second { get; }
        public string MethodName { get; }
        public Location Location { get; }
    }

    private sealed class HandlerKeyComparer : IEqualityComparer<HandlerRegistration>
    {
        public static readonly HandlerKeyComparer Instance = new();
        public bool Equals(HandlerRegistration? x, HandlerRegistration? y) => x != null && y != null &&
            x.MethodName == y.MethodName && SymbolEqualityComparer.Default.Equals(x.World, y.World) &&
            SymbolEqualityComparer.Default.Equals(x.First, y.First) && SymbolEqualityComparer.Default.Equals(x.Second, y.Second);
        public int GetHashCode(HandlerRegistration obj)
        {
            unchecked
            {
                var hash = SymbolEqualityComparer.Default.GetHashCode(obj.World);
                hash = (hash * 397) ^ SymbolEqualityComparer.Default.GetHashCode(obj.First);
                hash = (hash * 397) ^ SymbolEqualityComparer.Default.GetHashCode(obj.Second);
                return (hash * 397) ^ obj.MethodName.GetHashCode();
            }
        }
    }

    private static bool IsSegusumCycleApi(IMethodSymbol method)
    {
        if (method.Name != "startCycle" && method.Name != "addToCycle")
            return false;

        var containingType = method.ContainingType;
        return containingType != null &&
               containingType.ContainingNamespace?.ToDisplayString() == "Seg" &&
               ((method.Name == "startCycle" && containingType.Name == "WorldBase") ||
                (method.Name == "addToCycle" && containingType.Name == "Utils")) &&
               method.Parameters.Any(p => p.Type.SpecialType == SpecialType.System_String);
    }
}
