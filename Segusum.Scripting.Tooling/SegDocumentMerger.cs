using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using Segusum.Scripting.Core;

namespace Segusum.Scripting.Tooling;

public sealed record SegMergeResult(string Text, IReadOnlyList<string> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}

public sealed record SegDeclarationAuditEntry(string Identity, string Status, string? ExistingPath, string GeneratedBodyHash);
public sealed record SegDeclarationAuditResult(IReadOnlyList<SegDeclarationAuditEntry> Entries, IReadOnlyList<string> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}

/// <summary>AST-validated merger for generated top-level SEG declarations.</summary>
public static class SegDocumentMerger
{
    public static (SegDeclarationAuditResult Audit, string NewOnlyText) ExtractNewDeclarations(string generatedPath, string generatedText, IEnumerable<(string Path, string Text)> runtimeSources)
    {
        var audit = Audit(generatedPath, generatedText, runtimeSources);
        var newKeys = audit.Entries.Where(x => x.Status == "New").Select(x => x.Identity).ToHashSet(StringComparer.Ordinal);
        var parsed = DslParser.Parse(new DslSource(generatedPath, generatedText));
        var declarations = parsed.Document.Declarations.OrderBy(x => x.Span.Start).ToArray();
        var headerEnd = generatedText.IndexOf('\n');
        var header = headerEnd < 0 ? generatedText.Trim() : generatedText[..headerEnd].TrimEnd('\r');
        var segments = new List<string>();
        for (var i = 0; i < declarations.Length; i++)
        {
            if (!newKeys.Contains(DeclarationKey(declarations[i]))) continue;
            var start = Math.Clamp(declarations[i].Span.Start, 0, generatedText.Length);
            var end = i + 1 < declarations.Length ? Math.Clamp(declarations[i + 1].Span.Start, start, generatedText.Length) : generatedText.Length;
            segments.Add(generatedText[start..end].Trim('\r', '\n'));
        }
        var newOnly = header + (segments.Count == 0 ? "\r\n" : "\r\n\r\n" + string.Join("\r\n\r\n", segments) + "\r\n");
        return (audit, newOnly);
    }

    public static SegDeclarationAuditResult Audit(string generatedPath, string generatedText, IEnumerable<(string Path, string Text)> runtimeSources)
    {
        var diagnostics = new List<string>();
        var generated = DslParser.Parse(new DslSource(generatedPath, generatedText));
        AddParserDiagnostics(generated, diagnostics);
        var existing = new Dictionary<string, (DslDeclaration Declaration, string Path, string Canonical)>(StringComparer.Ordinal);
        foreach (var source in runtimeSources.OrderBy(x => x.Path, StringComparer.OrdinalIgnoreCase))
        {
            var parsed = DslParser.Parse(new DslSource(source.Path, source.Text));
            AddParserDiagnostics(parsed, diagnostics);
            foreach (var declaration in parsed.Document.Declarations)
            {
                var identity = DeclarationKey(declaration);
                var canonical = Canonical(declaration);
                if (existing.TryGetValue(identity, out var previous))
                {
                    if (!string.Equals(previous.Canonical, canonical, StringComparison.Ordinal))
                        diagnostics.Add($"runtime SEG declaration conflict: {identity} ({previous.Path} vs {source.Path})");
                }
                else existing.Add(identity, (declaration, source.Path, canonical));
            }
        }
        var entries = new List<SegDeclarationAuditEntry>();
        foreach (var declaration in generated.Document.Declarations)
        {
            var identity = DeclarationKey(declaration);
            var canonical = Canonical(declaration);
            var hash = BitConverter.ToString(System.Security.Cryptography.SHA256.Create().ComputeHash(Encoding.UTF8.GetBytes(canonical))).Replace("-", "");
            if (!existing.TryGetValue(identity, out var match))
                entries.Add(new(identity, "New", null, hash));
            else if (string.Equals(match.Canonical, canonical, StringComparison.Ordinal))
                entries.Add(new(identity, "AlreadyPresent", match.Path, hash));
            else
            {
                entries.Add(new(identity, "Conflict", match.Path, hash));
                diagnostics.Add($"generated SEG declaration conflict: {identity} ({match.Path})");
            }
        }
        return new(entries, diagnostics);
    }

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

    private static string DeclarationKey(DslDeclaration declaration) => declaration switch
    {
        FunctionDeclaration function => function.Name + "(" + string.Join(",", function.Parameters.Select(x => x.Type)) + ")",
        BeforeRoomChangeDeclaration => "before-room-change",
        AfterActionExecutedDeclaration => "after-action-executed",
        HandlerDeclaration handler => handler.Kind + ":" + handler.First + ":" + handler.Second + ":" + handler.Target,
        _ => declaration.GetType().Name
    };

    private static string Canonical(object? value)
    {
        if (value == null) return "null";
        if (value is SourceSpan) return "<span>";
        if (value is string text) return "\"" + text.Replace("\\", "\\\\").Replace("\"", "\\\"") + "\"";
        if (value is bool or byte or sbyte or short or ushort or int or uint or long or ulong or float or double or decimal or char or Enum)
            return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
        if (value is IEnumerable sequence && value is not string)
        {
            var builder = new StringBuilder("[");
            var first = true;
            foreach (var item in sequence) { if (!first) builder.Append(','); first = false; builder.Append(Canonical(item)); }
            return builder.Append(']').ToString();
        }
        var type = value.GetType();
        var result = new StringBuilder(type.FullName);
        foreach (var property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public).OrderBy(x => x.Name, StringComparer.Ordinal))
        {
            if (property.Name.EndsWith("Span", StringComparison.Ordinal) || property.PropertyType == typeof(SourceSpan)) continue;
            result.Append('|').Append(property.Name).Append('=').Append(Canonical(property.GetValue(value)));
        }
        foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public).OrderBy(x => x.Name, StringComparer.Ordinal))
            result.Append('|').Append(field.Name).Append('=').Append(Canonical(field.GetValue(value)));
        return result.ToString();
    }
}
