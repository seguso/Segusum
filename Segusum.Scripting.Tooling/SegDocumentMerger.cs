using System;
using System.Collections.Generic;
using System.Linq;
using Segusum.Scripting.Core;

namespace Segusum.Scripting.Tooling;

public sealed record SegMergeResult(string Text, IReadOnlyList<string> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}

/// <summary>AST-validated merger for generated top-level SEG declarations.</summary>
public static class SegDocumentMerger
{
    public static SegMergeResult Merge(string basePath, string baseText, IReadOnlyList<(string Path, string Text)> additions)
    {
        var diagnostics = new List<string>();
        var baseParsed = DslParser.Parse(new DslSource(basePath, baseText));
        AddParserDiagnostics(baseParsed, diagnostics);
        var baseFunctionList = baseParsed.Document.Declarations.OfType<FunctionDeclaration>().ToArray();
        var baseFunctions = baseFunctionList.ToDictionary(FunctionKey, StringComparer.Ordinal);
        var baseKeys = baseFunctions.Keys.ToHashSet(StringComparer.Ordinal);
        var additionSegments = new List<string>();

        foreach (var addition in additions)
        {
            var parsed = DslParser.Parse(new DslSource(addition.Path, addition.Text));
            AddParserDiagnostics(parsed, diagnostics);
            var functionDeclarations = parsed.Document.Declarations.OfType<FunctionDeclaration>().ToArray();
            var keys = new HashSet<string>(StringComparer.Ordinal);
            var newDeclarations = new List<FunctionDeclaration>();
            foreach (var declaration in functionDeclarations)
            {
                var key = FunctionKey(declaration);
                if (!keys.Add(key)) diagnostics.Add($"duplicate generated SEG declaration: {key} ({addition.Path})");
                if (baseFunctions.TryGetValue(key, out var existing))
                {
                    if (!string.Equals(DeclarationText(baseText, existing, baseFunctionList), DeclarationText(addition.Text, declaration, functionDeclarations), StringComparison.Ordinal))
                        diagnostics.Add($"generated SEG declaration collides with a different existing declaration: {key} ({addition.Path})");
                }
                else newDeclarations.Add(declaration);
            }
            if (newDeclarations.Count != 0 && newDeclarations.Count != functionDeclarations.Length)
                diagnostics.Add($"addition mixes existing and new declarations; refusing unsafe merge ({addition.Path})");
            if (newDeclarations.Count == functionDeclarations.Length && parsed.Document.Declarations.Count != 0)
                additionSegments.Add(TopLevelSegments(addition.Text, parsed.Document.Declarations));
            baseKeys.UnionWith(keys);
            foreach (var declaration in newDeclarations)
                baseFunctions[FunctionKey(declaration)] = declaration;
        }

        if (diagnostics.Count != 0) return new(baseText, diagnostics);
        var result = baseText.TrimEnd('\r', '\n', ' ', '\t');
        foreach (var segment in additionSegments)
        {
            if (segment.Length == 0) continue;
            result = result.TrimEnd('\r', '\n', ' ', '\t') + "\r\n\r\n" + segment.Trim('\r', '\n');
        }
        return new(result + "\r\n", diagnostics);
    }

    private static string FunctionKey(FunctionDeclaration function)
        => function.Name + "(" + string.Join(",", function.Parameters.Select(x => x.Type)) + ")";

    private static string DeclarationText(string text, DslDeclaration declaration, IReadOnlyList<FunctionDeclaration> declarations)
    {
        var start = Math.Clamp(declaration.Span.Start, 0, text.Length);
        var next = declarations
            .Where(x => x.Span.Start > declaration.Span.Start)
            .OrderBy(x => x.Span.Start)
            .Select(x => x.Span.Start)
            .FirstOrDefault(text.Length);
        var end = next == 0 ? text.Length : Math.Clamp(next, start, text.Length);
        return text[start..end].Trim();
    }

    private static string TopLevelSegments(string text, IReadOnlyList<DslDeclaration> declarations)
    {
        // The parser is the authority for validating declaration boundaries and
        // duplicate identity.  Keep the complete addition payload after its
        // world header so comments/trivia outside the declaration spans are
        // not lost during integration.
        var headerEnd = text.IndexOf('\n');
        var start = headerEnd < 0 ? 0 : headerEnd + 1;
        return text[start..].Trim('\r', '\n');
    }

    private static void AddParserDiagnostics((DslDocument Document, IReadOnlyList<DslDiagnostic> Diagnostics) parsed, List<string> diagnostics)
    {
        foreach (var diagnostic in parsed.Diagnostics)
            diagnostics.Add($"{diagnostic.Id} {diagnostic.Span.Line}:{diagnostic.Span.Column}: {diagnostic.Message}");
    }
}
