using System;
using System.Collections.Generic;
using System.Linq;
namespace Segusum.Scripting.Core;
public readonly record struct SourceSpan(string Path, int Start, int Length, int Line, int Column)
{
    public static SourceSpan From(string path, string text, int start, int length) { var line=1; var column=1; for(var i=0;i<start&&i<text.Length;i++){if(text[i]=='\n'){line++;column=1;}else column++;} return new(path,start,length,line,column); }
}
public sealed record DslSource(string Path, string Text);
public sealed record DslDiagnostic(string Id, string Message, SourceSpan Span);
public abstract record DslNode(SourceSpan Span);
public abstract record DslDeclaration(SourceSpan Span) : DslNode(Span);
public sealed record StateDeclaration(string Name,string Type,DslExpression Initializer,SourceSpan Span) : DslDeclaration(Span);
public sealed record FunctionDeclaration(string Name,IReadOnlyList<(string Name,string Type)> Parameters,string? ReturnType,IReadOnlyList<DslStatement> Body,SourceSpan Span) : DslDeclaration(Span);
public sealed record HandlerDeclaration(string Kind,string First,string? Second,string? Target,string? Phrase,string? Explanation,DslExpression? Condition,IReadOnlyList<DslStatement> Body,SourceSpan Span) : DslDeclaration(Span);
public sealed record CycleDeclaration(string Variable,SourceSpan Span) : DslDeclaration(Span);
public sealed record CycleElementDeclaration(string Cycle,string Id,bool Important,DslExpression? Condition,IReadOnlyList<DslStatement> Body,SourceSpan Span) : DslDeclaration(Span);
public sealed record DslDocument(IReadOnlyList<DslDeclaration> Declarations);
public abstract record DslStatement(SourceSpan Span) : DslNode(Span);
public sealed record VariableDeclaration(string Name,DslExpression Initializer,SourceSpan Span) : DslStatement(Span);
public sealed record AssignmentStatement(string Name,string Operator,DslExpression Value,SourceSpan Span) : DslStatement(Span);
public sealed record IncrementStatement(string Name,SourceSpan Span) : DslStatement(Span);
public sealed record CallStatement(DslExpression Expression,SourceSpan Span) : DslStatement(Span);
public sealed record ReturnStatement(DslExpression Expression,SourceSpan Span) : DslStatement(Span);
public sealed record IfStatement(IReadOnlyList<(DslExpression Condition,IReadOnlyList<DslStatement> Body)> Branches,IReadOnlyList<DslStatement>? ElseBody,SourceSpan Span) : DslStatement(Span);
public sealed record NarStatement(DslExpression Text,SourceSpan Span) : DslStatement(Span);
public sealed record DialogueStatement(string Character,DslExpression Text,SourceSpan Span) : DslStatement(Span);
public sealed record NextCycleStatement(DslExpression Cycle,SourceSpan Span) : DslStatement(Span);
public sealed record AddCycleElementStatement(string Cycle,string Id,bool Important,DslExpression? Condition,IReadOnlyList<DslStatement> Body,SourceSpan Span) : DslStatement(Span);
public sealed record MakesNoSenseStatement(SourceSpan Span) : DslStatement(Span);
public sealed record FinishGameStatement(SourceSpan Span) : DslStatement(Span);
public sealed record DoNotAdvanceTimeStatement(SourceSpan Span) : DslStatement(Span);
public abstract record DslExpression(SourceSpan Span) : DslNode(Span);
public sealed record IdentifierExpression(string Name,SourceSpan Span) : DslExpression(Span);
public sealed record LiteralExpression(string Value,string Kind,SourceSpan Span) : DslExpression(Span);
public sealed record UnaryExpression(string Operator,DslExpression Operand,SourceSpan Span) : DslExpression(Span);
public sealed record BinaryExpression(string Operator,DslExpression Left,DslExpression Right,SourceSpan Span) : DslExpression(Span);
public sealed record CallExpression(string Name,IReadOnlyList<DslArgument> Arguments,SourceSpan Span) : DslExpression(Span);
public sealed record ParenthesizedExpression(DslExpression Expression,SourceSpan Span) : DslExpression(Span);
public sealed record DslArgument(string? Name,DslExpression Expression,SourceSpan Span);
public static class DslNames
{
    public static string Camel(string name) { var parts=name.Split(new[]{'-'},StringSplitOptions.RemoveEmptyEntries); if(parts.Length==0)return name; return parts[0]+string.Concat(parts.Skip(1).Select(x=>char.ToUpperInvariant(x[0])+x.Substring(1))); }
    public static IEnumerable<string> Candidates(string name) { yield return name; var c=Camel(name); if(c!=name)yield return c; if(c.Length>0)yield return char.ToUpperInvariant(c[0])+c.Substring(1); }
}
