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

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(NonLiteral, Empty, Duplicate);

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(start =>
        {
            var ids = new Dictionary<string, Location>(StringComparer.Ordinal);
            start.RegisterSyntaxNodeAction(c => AnalyzeInvocation(c, ids), SyntaxKind.InvocationExpression);
        });
    }

    private static void AnalyzeInvocation(SyntaxNodeAnalysisContext context, Dictionary<string, Location> ids)
    {
        var invocation = (InvocationExpressionSyntax)context.Node;
        if (context.SemanticModel.GetSymbolInfo(invocation).Symbol is not IMethodSymbol method ||
            !IsSegusumCycleApi(method))
            return;

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
            if (ids.TryGetValue(id, out var first))
            {
                var firstLine = first.GetLineSpan().StartLinePosition.Line + 1;
                context.ReportDiagnostic(Diagnostic.Create(Duplicate, argument.GetLocation(), id, firstLine));
            }
            else
            {
                ids.Add(id, argument.GetLocation());
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
