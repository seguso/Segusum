using System;
using System.Collections.Generic;
using System.Linq;

namespace Segusum.Scripting.Core;

public sealed record DslSource(string Path, string Text);
public sealed record DslDiagnostic(string Id, string Message, string Path, int Line, int Column);
public abstract record DslDeclaration(string Path, int Line);
public sealed record StateDeclaration(string Name, string Type, string Initializer, string Path, int Line) : DslDeclaration(Path, Line);
public sealed record FunctionDeclaration(string Name, IReadOnlyList<(string Name, string Type)> Parameters, string? ReturnType, IReadOnlyList<string> Body, string Path, int Line) : DslDeclaration(Path, Line);
public sealed record HandlerDeclaration(string Kind, string First, string? Second, string? Target, string? Phrase, string? Explanation, string? Condition, IReadOnlyList<string> Body, string Path, int Line) : DslDeclaration(Path, Line);
public sealed record CycleDeclaration(string Variable, string Path, int Line) : DslDeclaration(Path, Line);
public sealed record CycleElementDeclaration(string Cycle, string Id, bool Important, string? Condition, IReadOnlyList<string> Body, string Path, int Line) : DslDeclaration(Path, Line);
public sealed record DslDocument(IReadOnlyList<DslDeclaration> Declarations);

public static class DslNames
{
    public static string Camel(string name)
    {
        var parts = name.Split(new[] { '-' }, StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 0) return name;
        return parts[0] + string.Concat(parts.Skip(1).Select(x => char.ToUpperInvariant(x[0]) + x.Substring(1)));
    }
    public static IEnumerable<string> Candidates(string name)
    {
        yield return name;
        var camel = Camel(name);
        if (camel != name) yield return camel;
        if (camel.Length > 0) yield return char.ToUpperInvariant(camel[0]) + camel.Substring(1);
    }
}
