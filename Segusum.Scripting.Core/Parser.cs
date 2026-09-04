using System;
using System.Collections.Generic;

namespace Segusum.Scripting.Core;

public static class DslParser
{
    public static (DslDocument Document, IReadOnlyList<DslDiagnostic> Diagnostics) Parse(DslSource source)
    {
        var diagnostics = new List<DslDiagnostic>();
        var parser = new Parser(DslLexer.Lex(source, diagnostics), diagnostics);
        return (new DslDocument(parser.ParseDocument()), diagnostics);
    }

    private sealed class Parser
    {
        private readonly IReadOnlyList<DslToken> tokens;
        private readonly List<DslDiagnostic> diagnostics;
        private int position;
        public Parser(IReadOnlyList<DslToken> tokens, List<DslDiagnostic> diagnostics) { this.tokens = tokens; this.diagnostics = diagnostics; }
        private DslToken Current => tokens[position];
        private bool Is(string text) => Current.Text == text;
        private DslToken Take() => tokens[position++];
        private void SkipTerminators() { while (Current.Kind is DslTokenKind.NewLine or DslTokenKind.Semicolon) Take(); }
        private void Need(string text) { if (Is(text)) Take(); else Error($"Expected '{text}'."); }
        private string Word() { if (Current.Kind != DslTokenKind.Identifier) { Error("Expected identifier."); return "_error"; } return Take().Text; }
        private void Error(string message) => diagnostics.Add(new DslDiagnostic("SEGDSL101", message, Current.Span));
        private void RecoverLine() { while (Current.Kind is not (DslTokenKind.NewLine or DslTokenKind.Semicolon or DslTokenKind.EndOfFile)) Take(); }

        public IReadOnlyList<DslDeclaration> ParseDocument()
        {
            var result = new List<DslDeclaration>(); SkipTerminators();
            while (Current.Kind != DslTokenKind.EndOfFile)
            {
                var span = Current.Span; var keyword = Word();
                switch (keyword)
                {
                    case "state": result.Add(ParseState(span)); break;
                    case "def": result.Add(ParseFunction(span)); break;
                    case "combine": result.Add(ParseHandler("combine", span)); break;
                    case "use": result.Add(ParseHandler("use", span)); break;
                    case "add": result.Add(ParseCycleElement(span)); break;
                    case "var": { var name = Word(); Need("="); Need("new-cycle"); result.Add(new CycleDeclaration(name, span)); break; }
                    case "next": result.Add(new NextCycleDeclaration(Expression(), span)); break;
                    default: Error($"Unexpected declaration '{keyword}'."); RecoverLine(); break;
                }
                SkipTerminators();
            }
            return result;
        }
        private StateDeclaration ParseState(SourceSpan span) { var name = Word(); Need(":"); var type = Word(); Need("="); return new(name, type, Expression(), span); }
        private FunctionDeclaration ParseFunction(SourceSpan span)
        {
            var name = Word(); var parameters = new List<(string Name, string Type)>();
            while (!Is("ret") && !Is(":") && Current.Kind != DslTokenKind.NewLine && Current.Kind != DslTokenKind.EndOfFile)
            { var parameter = Word(); Need(":"); parameters.Add((parameter, Word())); if (Is(",")) Take(); }
            string? returnType = null; if (Is("ret")) { Take(); returnType = Word(); }
            return new(name, parameters, returnType, ParseBody(true), span);
        }
        private HandlerDeclaration ParseHandler(string kind, SourceSpan span)
        {
            var first = Word(); string? second = null; string? target = null;
            if (kind == "combine") { Need("with"); second = Word(); }
            else if (Is("for")) { Take(); kind = "use-for"; target = Word(); }
            else { Need("here"); kind = "use-here"; }
            Need(":"); SkipTerminators(); DslExpression? phrase = null, explanation = null, condition = null; var body = new List<DslStatement>();
            while (!Is("end") && Current.Kind != DslTokenKind.EndOfFile)
            { var s = Current.Span; var w = Word(); if (w == "phrase") phrase = Expression(); else if (w == "exp") explanation = Expression(); else if (w == "possible-when") condition = Expression(); else body.Add(ParseStatement(w, s)); SkipTerminators(); }
            Need("end"); return new(kind, first, second, target, phrase, explanation, condition, body, span);
        }
        private CycleElementDeclaration ParseCycleElement(SourceSpan span)
        { var cycle = Word(); var id = Word(); var important = Is("important"); if (important) Take(); var repeat = ParseRepeatModifier(); var x = ParseBlockWithClause("when"); return new(cycle, id, important, repeat, x.Condition, x.Body, span); }
        private AddCycleElementStatement ParseAdd(SourceSpan span)
        { var cycle = Word(); var id = Word(); var important = Is("important"); if (important) Take(); var repeat = ParseRepeatModifier(); var x = ParseBlockWithClause("when"); return new(cycle, id, important, repeat, x.Condition, x.Body, span); }
        private string? ParseRepeatModifier()
        {
            if (Current.Kind != DslTokenKind.Identifier) return null;
            var value = Take().Text;
            if (value is "once" or "forever") return value;
            diagnostics.Add(new DslDiagnostic("SEGDSL102", $"Unknown Repeat modifier '{value}'. Expected 'once' or 'forever'.", tokens[position - 1].Span));
            return value;
        }
        private (DslExpression? Condition, IReadOnlyList<DslStatement> Body) ParseBlockWithClause(string clause)
        {
            SkipTerminators(); DslExpression? condition = null; var body = new List<DslStatement>();
            while (!Is("end") && Current.Kind != DslTokenKind.EndOfFile)
            { var s = Current.Span; var w = Word(); if (w == clause) condition = Expression(); else body.Add(ParseStatement(w, s)); SkipTerminators(); }
            Need("end"); return (condition, body);
        }
        private IReadOnlyList<DslStatement> ParseBody(bool colon)
        {
            if (colon) Need(":"); SkipTerminators(); var body = new List<DslStatement>();
            while (!Is("end") && Current.Kind != DslTokenKind.EndOfFile) { var s = Current.Span; body.Add(ParseStatement(Word(), s)); SkipTerminators(); }
            Need("end"); return body;
        }
        private DslStatement ParseStatement(string keyword, SourceSpan span)
        {
            switch (keyword)
            {
                case "if": return ParseIf(span); case "ret": return new ReturnStatement(Expression(), span); case "nar": return new NarStatement(Expression(), span);
                case "nar-room": return new NarRoomStatement(Expression(), span); case "call": return new CallStatement(ParseCallAfterKeyword(span), span);
                case "var": { var name = Word(); Need("="); return new VariableDeclaration(name, Expression(), span); }
                case "next": return new NextCycleStatement(Expression(), span); case "add": return ParseAdd(span);
                case "makes-no-sense": return new MakesNoSenseStatement(span); case "finish-game": return new FinishGameStatement(span); case "do-not-advance-time": return new DoNotAdvanceTimeStatement(span);
                default:
                    if (Is(":")) { Take(); return new DialogueStatement(keyword, Expression(), span); }
                    if (Is("++")) { Take(); return new IncrementStatement(keyword, span); }
                    if (Is("=") || Is("+=") || Is("-=")) { var op = Take().Text; return new AssignmentStatement(keyword, op, Expression(), span); }
                    Error($"Unknown statement '{keyword}'."); RecoverLine(); return new CallStatement(new IdentifierExpression("_error", span), span);
            }
        }
        private IfStatement ParseIf(SourceSpan span)
        {
            var branches = new List<(DslExpression Condition, IReadOnlyList<DslStatement> Body)>(); var condition = Expression(); Need(":"); branches.Add((condition, ParseUntilBranchBoundary())); IReadOnlyList<DslStatement>? otherwise = null;
            while (Is("elif")) { Take(); var c = Expression(); Need(":"); branches.Add((c, ParseUntilBranchBoundary())); }
            if (Is("else")) { Take(); Need(":"); otherwise = ParseUntilEnd(); }
            Need("end"); return new(branches, otherwise, span);
        }
        private IReadOnlyList<DslStatement> ParseUntilBranchBoundary()
        { SkipTerminators(); var body = new List<DslStatement>(); while (!Is("elif") && !Is("else") && !Is("end") && Current.Kind != DslTokenKind.EndOfFile) { var s = Current.Span; body.Add(ParseStatement(Word(), s)); SkipTerminators(); } return body; }
        private IReadOnlyList<DslStatement> ParseUntilEnd()
        { SkipTerminators(); var body = new List<DslStatement>(); while (!Is("end") && Current.Kind != DslTokenKind.EndOfFile) { var s = Current.Span; body.Add(ParseStatement(Word(), s)); SkipTerminators(); } return body; }
        private DslExpression ParseCallAfterKeyword(SourceSpan span) { var name = Word(); var args = new List<DslArgument>(); while (CanStartArgument()) args.Add(ParseArgument()); return new CallExpression(name, args, span); }
        private bool CanStartArgument() => Current.Kind is DslTokenKind.Identifier or DslTokenKind.Number or DslTokenKind.String or DslTokenKind.LParen;
        private DslArgument ParseArgument()
        { var span = Current.Span; if (Current.Kind == DslTokenKind.Identifier && position + 1 < tokens.Count && tokens[position + 1].Kind == DslTokenKind.Colon) { var name = Take().Text; Take(); return new DslArgument(name, Expression(), span); } return new DslArgument(null, Prefix(), span); }
        private DslExpression Expression(int minimumPrecedence = 0)
        {
            var left = Prefix(); while (true) { ConsumeExpressionContinuation(); var precedence = Precedence(Current.Text); if (precedence <= minimumPrecedence) break; var op = Take().Text; left = new BinaryExpression(op, left, Expression(precedence), left.Span); } return left;
        }
        private void ConsumeExpressionContinuation()
        { if (Current.Kind != DslTokenKind.NewLine) return; var next = position; while (tokens[next].Kind == DslTokenKind.NewLine) next++; if (Precedence(tokens[next].Text) > 0) while (position < next) Take(); }
        private static int Precedence(string op) => op switch { "or" => 1, "and" => 2, "==" or "!=" or ">" or ">=" or "<" or "<=" => 3, "+" or "-" => 4, "*" or "/" => 5, _ => 0 };
        private DslExpression Prefix()
        {
            while (Current.Kind == DslTokenKind.NewLine) Take(); var span = Current.Span;
            if (Is("not")) { Take(); return new UnaryExpression("not", Prefix(), span); }
            if (Is("call")) { Take(); return ParseCallAfterKeyword(span); }
            if (Current.Kind == DslTokenKind.LParen) { Take(); var expression = Expression(); Need(")"); return new ParenthesizedExpression(expression, span); }
            if (Current.Kind == DslTokenKind.String) return new LiteralExpression(Take().Text, "string", span);
            if (Current.Kind == DslTokenKind.Number) return new LiteralExpression(Take().Text, "number", span);
            if (Is("true") || Is("false")) return new LiteralExpression(Take().Text, "bool", span);
            if (Is("new-cycle")) { Take(); return new LiteralExpression("new-cycle", "cycle", span); }
            var identifier = new IdentifierExpression(Word(), span);
            if (Is("not-seen-recently")) { Take(); return new CallExpression("not-seen-recently", new[] { new DslArgument(null, identifier, span), new DslArgument(null, Prefix(), Current.Span) }, span); }
            if (Is("was-seen-at-least-once")) { Take(); return new CallExpression("was-seen-at-least-once", new[] { new DslArgument(null, identifier, span) }, span); }
            return identifier;
        }
    }
}
