using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace Segusum.Scripting.Core;

internal sealed class DslParserProfile
{
    private readonly Dictionary<string, (long Calls, long Ticks)> counters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (long Calls, long Ticks)> phases = new(StringComparer.Ordinal);

    public void Count(string name, long amount = 1)
    {
        counters.TryGetValue(name, out var value);
        value.Calls += amount;
        counters[name] = value;
    }

    public void Add(string name, long ticks, long calls = 1)
    {
        counters.TryGetValue(name, out var value);
        value.Calls += calls;
        value.Ticks += ticks;
        counters[name] = value;
    }

    public void AddPhase(string name, long ticks, long calls = 1)
    {
        phases.TryGetValue(name, out var value);
        value.Calls += calls;
        value.Ticks += ticks;
        phases[name] = value;
    }

    public T Measure<T>(string name, Func<T> action)
    {
        var started = Stopwatch.GetTimestamp();
        try { return action(); }
        finally { Add(name, Stopwatch.GetTimestamp() - started); }
    }

    public string Format()
    {
        static string Table(Dictionary<string, (long Calls, long Ticks)> values)
            => string.Join("; ", values.OrderByDescending(x => x.Value.Ticks)
                .Select(x => $"{x.Key}:calls={x.Value.Calls},ms={x.Value.Ticks * 1000.0 / Stopwatch.Frequency:0.0}"));
        return $"phases=[{Table(phases)}] counters=[{Table(counters)}]";
    }
}

public static class DslParser
{
    [ThreadStatic]
    internal static DslParserProfile? ActiveProfile;

    public static (DslDocument Document, IReadOnlyList<DslDiagnostic> Diagnostics) Parse(DslSource source)
    {
        var profile = new DslParserProfile();
        var allocatedBefore = GC.GetTotalMemory(false);
        var diagnostics = new List<DslDiagnostic>();
        ActiveProfile = profile;
        try
        {
            var lexerTimer = Stopwatch.StartNew();
            var tokens = DslLexer.Lex(source, diagnostics);
            lexerTimer.Stop();
            profile.AddPhase("lexer+token-list", lexerTimer.ElapsedTicks, tokens.Count);
            var parser = new Parser(tokens, diagnostics, source.Text, profile);
            var document = profile.Measure("ParseDocument", parser.ParseDocument);
            var allocated = GC.GetTotalMemory(false) - allocatedBefore;
            if (string.Equals(Environment.GetEnvironmentVariable("SEGUSUM_DSL_PROFILE"), "1", StringComparison.Ordinal))
                Console.Error.WriteLine($"dslParser path={source.Path} chars={source.Text.Length} lines={CountLines(source.Text)} tokens={tokens.Count} declarations={document.Declarations.Count} diagnostics={diagnostics.Count} managedMemoryDeltaBytes={allocated} profile={profile.Format()}");
            return (document, diagnostics);
        }
        finally { ActiveProfile = null; }
    }

    private static int CountLines(string text) => text.Length == 0 ? 0 : text.Count(x => x == '\n') + 1;

    private sealed class Parser
    {
        private readonly IReadOnlyList<DslToken> tokens;
        private readonly List<DslDiagnostic> diagnostics;
        private readonly string sourceText;
        private readonly DslParserProfile profile;
        private readonly int[] lineStarts;
        private int position;
        private bool parsingCallArgument;
        private bool parsingListComprehensionClause;
        public Parser(IReadOnlyList<DslToken> tokens, List<DslDiagnostic> diagnostics, string sourceText, DslParserProfile profile)
        {
            this.tokens = tokens; this.diagnostics = diagnostics; this.sourceText = sourceText; this.profile = profile;
            lineStarts = BuildLineStarts(sourceText);
        }
        private static int[] BuildLineStarts(string text)
        {
            var starts = new List<int> { 0 };
            for (var i = 0; i < text.Length; i++) if (text[i] == '\n') starts.Add(i + 1);
            return starts.ToArray();
        }
        private SourceSpan SpanAt(int start, int length)
        {
            start = start < 0 ? 0 : start > sourceText.Length ? sourceText.Length : start;
            var line = Array.BinarySearch(lineStarts, start);
            if (line < 0) line = ~line - 1;
            return new(tokens[0].Span.Path, start, length, line + 1, start - lineStarts[line] + 1);
        }
        private DslToken Current => tokens[position];
        private bool Is(string text) { profile.Count("Is"); return Current.Text == text; }
        private bool Is(DslTokenKind kind) { profile.Count("IsKind"); return Current.Kind == kind; }
        private DslToken Take()
        {
            profile.Count("Take"); return tokens[position++];
        }
        private void SkipTerminators() { while (Current.Kind is DslTokenKind.NewLine or DslTokenKind.Semicolon) Take(); }
        private void Need(string text) { if (Is(text)) Take(); else Error($"Expected '{text}'."); }
        private string Word() => WordToken().Text;
        private DslToken WordToken() { profile.Count("WordToken"); if (Current.Kind != DslTokenKind.Identifier) { Error("Expected identifier."); return Current; } return Take(); }
        private void Error(string message) { profile.Count("diagnostics"); diagnostics.Add(new DslDiagnostic("SEGDSL101", message, Current.Span)); }
        private void RecoverLine() { profile.Count("error-recovery"); while (Current.Kind is not (DslTokenKind.NewLine or DslTokenKind.Semicolon or DslTokenKind.EndOfFile)) Take(); }

        public DslDocument ParseDocument()
        {
            var result = new List<DslDeclaration>(); SkipTerminators(); string? worldId = null;
            var headerStarted = Stopwatch.GetTimestamp();
            if (Is("world")) { Take(); worldId = Word(); SkipTerminators(); }
            else diagnostics.Add(new DslDiagnostic("SEGDSL103", "A .seg file must begin with a world directive.", Current.Span));
            profile.AddPhase("world-header", Stopwatch.GetTimestamp() - headerStarted);
            var declarationsStarted = Stopwatch.GetTimestamp();
            while (Current.Kind != DslTokenKind.EndOfFile)
            {
                var span = Current.Span; var keyword = Word();
                switch (keyword)
                {
                    case "state": result.Add(profile.Measure("ParseDeclaration.state", () => ParseState(span))); break;
                    case "def": result.Add(profile.Measure("ParseDeclaration.def", () => ParseFunction(span))); break;
                    case "combine": result.Add(profile.Measure("ParseDeclaration.handler.combine", () => ParseHandler("combine", span))); break;
                    case "use": result.Add(profile.Measure("ParseDeclaration.handler.use", () => ParseHandler("use", span))); break;
                    case "pickup": result.Add(profile.Measure("ParseDeclaration.handler.pickup", () => ParseHandler("pickup", span))); break;
                    case "talk-here": result.Add(profile.Measure("ParseDeclaration.handler.talk-here", () => ParseHandler("talk-here", span))); break;
                    case "cancel-text-input": result.Add(profile.Measure("ParseDeclaration.handler.cancel-text-input", () => ParseHandler("cancel-text-input", span))); break;
                    case "submit-text-input": result.Add(profile.Measure("ParseDeclaration.handler.submit-text-input", () => ParseHandler("submit-text-input", span))); break;
                    case "room-changed": result.Add(profile.Measure("ParseDeclaration.handler.room-changed", () => ParseHandler("room-changed", span))); break;
                    case "before-room-change": result.Add(profile.Measure("ParseDeclaration.before-room-change", () => new BeforeRoomChangeDeclaration(ParseBody(true), span))); break;
                    case "after-action-executed": result.Add(profile.Measure("ParseDeclaration.after-action-executed", () => new AfterActionExecutedDeclaration(ParseBody(true), span))); break;
                    case "add": result.Add(profile.Measure("ParseDeclaration.add", () => ParseCycleElement(span))); break;
                    case "var": { var name = Word(); Need("="); Need("new-cycle"); result.Add(profile.Measure("ParseDeclaration.var", () => new CycleDeclaration(name, span))); break; }
                    case "next": result.Add(profile.Measure("ParseDeclaration.next", () => new NextCycleDeclaration(Expression(), span))); break;
                    case "world": diagnostics.Add(new DslDiagnostic("SEGDSL104", worldId == null ? "The world directive must appear before declarations." : "The world directive must appear exactly once before declarations.", span)); RecoverLine(); break;
                    default: Error($"Unexpected declaration '{keyword}'."); RecoverLine(); break;
                }
                SkipTerminators();
            }
            profile.AddPhase("declaration-cycle", Stopwatch.GetTimestamp() - declarationsStarted, result.Count);
            return new DslDocument(worldId, result);
        }
        private StateDeclaration ParseState(SourceSpan span) { var name = Word(); Need(":"); var type = Word(); Need("="); return new(name, type, Expression(), span); }
        private FunctionDeclaration ParseFunction(SourceSpan span)
        {
            var name = Word(); var parameters = new List<(string Name, string Type)>();
            while (!Is("ret") && !Is(":") && Current.Kind != DslTokenKind.NewLine && Current.Kind != DslTokenKind.EndOfFile)
            { var parameter = Word(); Need(":"); parameters.Add((parameter, ParseTypeName())); if (Is(",")) Take(); }
            string? returnType = null; if (Is("ret")) { Take(); returnType = ParseTypeName(); }
            return new(name, parameters, returnType, ParseBody(true), span);
        }
        private string ParseTypeName()
        {
            var type = Word();
            if (Is("<"))
            {
                Take();
                var arguments = new List<string> { ParseTypeName() };
                while (Is(",")) { Take(); arguments.Add(ParseTypeName()); }
                Need(">");
                type += "<" + string.Join(", ", arguments) + ">";
            }
            while (Is("["))
            {
                Take(); Need("]"); type += "[]";
            }
            if (Is("?")) { Take(); type += "?"; }
            return type;
        }
        private HandlerDeclaration ParseHandler(string kind, SourceSpan span)
        {
            var firstToken = WordToken(); var first = firstToken.Text; string? second = null; string? target = null; SourceSpan? secondSpan = null; SourceSpan? targetSpan = null;
            if (kind == "combine") { Need("with"); var token = WordToken(); second = token.Text; secondSpan = token.Span; }
            else if (kind == "room-changed") { }
            else if (kind is "pickup" or "talk-here" or "cancel-text-input" or "submit-text-input") { }
            else if (Is("for")) { Take(); kind = "use-for"; var token = WordToken(); target = token.Text; targetSpan = token.Span; }
            else { Need("here"); kind = "use-here"; }
            Need(":"); SkipTerminators(); DslExpression? phrase = null, explanation = null, condition = null; var body = new List<DslStatement>();
            while (!Is("end") && Current.Kind != DslTokenKind.EndOfFile)
            { var s = Current.Span; var w = Word(); if (w == "phrase") phrase = Expression(); else if (w == "exp") explanation = Expression(); else if (w == "possible-when") condition = Expression(); else body.Add(ParseStatement(w, s)); SkipTerminators(); }
            Need("end"); return new(kind, first, second, target, phrase, explanation, condition, body, span) { FirstSpan = firstToken.Span, SecondSpan = secondSpan, TargetSpan = targetSpan };
        }
        private CycleElementDeclaration ParseCycleElement(SourceSpan span)
        { var cycleToken = WordToken(); var idToken = WordToken(); var cycle = cycleToken.Text; var id = idToken.Text; var important = Is("important"); if (important) Take(); var repeat = ParseRepeatModifier(); var x = ParseBlockWithClause("when"); return new(cycle, id, important, repeat, x.Condition, x.Body, span) { CycleSpan = cycleToken.Span, IdSpan = idToken.Span }; }
        private AddCycleElementStatement ParseAdd(SourceSpan span)
        { var cycleToken = WordToken(); var idToken = WordToken(); var cycle = cycleToken.Text; var id = idToken.Text; var important = Is("important"); if (important) Take(); var repeat = ParseRepeatModifier(); var x = ParseBlockWithClause("when", true); return new(cycle, id, important, repeat, x.Condition, x.Body, span) { CycleSpan = cycleToken.Span, IdSpan = idToken.Span }; }
        private string? ParseRepeatModifier()
        {
            if (Current.Kind != DslTokenKind.Identifier) return null;
            if (Is("once") || Is("forever")) return Take().Text;
            if (Is("when") || Is("nar") || Is("nar-room") || Is("call") || Is("if") || Is("var") || Is("next") || Is("makes-no-sense") || Is("mark-happened-once") || Is("mark-happened") || Is("finish-game") || Is("do-not-advance-time")) return null;
            diagnostics.Add(new DslDiagnostic("SEGDSL102", $"Unknown Repeat modifier '{Current.Text}'. Expected 'once' or 'forever'.", Current.Span));
            return null;
        }
        private (DslExpression? Condition, IReadOnlyList<DslStatement> Body) ParseBlockWithClause(string clause, bool implicitConsecutiveAdd = false)
        {
            SkipTerminators(); DslExpression? condition = null; var body = new List<DslStatement>();
            while (!Is("end") && !(implicitConsecutiveAdd && Is("add")) && Current.Kind != DslTokenKind.EndOfFile)
            { var s = Current.Span; var w = Word(); if (w == clause) condition = Expression(); else body.Add(ParseStatement(w, s)); SkipTerminators(); }
            if (implicitConsecutiveAdd && Is("add"))
            {
                // The next add belongs to the same sibling chain. Leave it
                // untouched so the enclosing body can parse it.
            }
            else
            {
                Need("end");
            }
            return (condition, body);
        }
        private IReadOnlyList<DslStatement> ParseBody(bool colon)
        {
            if (colon) Need(":"); SkipTerminators(); var body = new List<DslStatement>();
            while (!Is("end") && Current.Kind != DslTokenKind.EndOfFile) { var s = Current.Span; body.Add(ParseStatement(Word(), s)); SkipTerminators(); }
            Need("end"); return body;
        }
        private DslStatement ParseStatement(string keyword, SourceSpan span)
        {
            profile.Count("statements");
            var category = StatementCategory(keyword);
            return profile.Measure("ParseStatement." + category, () => ParseStatementCore(keyword, span));
        }
        private string StatementCategory(string keyword)
        {
            if (keyword == "if") return "if";
            if (keyword is "ret" or "return") return "return";
            if (keyword is "var") return "variable-declaration";
            if (keyword == "for") return "for";
            if (keyword is "next") return "next";
            if (keyword is "add") return "add-cycle";
            if (keyword is "mark-happened" or "mark-happened-once") return "mark-happened";
            if (keyword == "named-cutscene") return "named-cutscene";
            if (keyword is "text-input") return "call";
            if (Is(":")) return "dialogue";
            if (Is("++")) return "increment";
            if (Is(".") && position + 1 < tokens.Count && tokens[position + 1].Kind == DslTokenKind.Identifier) return "member-access/call";
            if (Is("=") || Is("+=") || Is("-=")) return "assignment";
            return "call";
        }
        private DslStatement ParseStatementCore(string keyword, SourceSpan span)
        {
            switch (keyword)
            {
                case "if": return ParseIf(span); case "ret": return new ReturnStatement(Is(DslTokenKind.NewLine) || Is(DslTokenKind.Semicolon) || Is(DslTokenKind.EndOfFile) ? null : Expression(), span); case "nar": if (Is(":")) Take(); return new NarStatement(RawTextAfterKeyword(), span);
                case "nar-room": if (Is(":")) Take(); return new NarRoomStatement(RawTextAfterKeyword(), span); case "call": Error("The 'call' keyword is no longer part of the DSL syntax."); return new CallStatement(new IdentifierExpression("_error", span), span);
                case "var":
                {
                    var name = Word();
                    string? type = null;
                    if (Is(DslTokenKind.Colon)) { Take(); type = Word(); }
                    Need("=");
                    return new VariableDeclaration(name, Expression(), span) { Type = type };
                }
                case "for":
                {
                    var item = Word();
                    Need("in");
                    var collection = Expression();
                    Need(":");
                    var body = ParseUntilEnd();
                    Need("end");
                    return new ForStatement(item, collection, body, span);
                }
                case "next": return new NextCycleStatement(Expression(), span); case "add": return ParseAdd(span);
                case "makes-no-sense": return new MakesNoSenseStatement(span); case "prevent-room-change": return new PreventRoomChangeStatement(span); case "mark-happened-once": return new MarkHappenedOnceStatement(Expression(), span); case "mark-happened": return new MarkHappenedStatement(Expression(), span); case "finish-game": return new FinishGameStatement(span); case "do-not-advance-time": return new DoNotAdvanceTimeStatement(span);
                case "named-cutscene": return ParseNamedCutscene(span);
                case "text-input": return new TextInputStatement(Expression(), span);
                case "nar-img": return ParseNarImg(span);
                default:
                    if (Is(":")) { return ParseDialogue(keyword, span); }
                    if (Is("++")) { Take(); return new IncrementStatement(keyword, span) { NameSpan = span }; }
                    if (Is(".") && position + 1 < tokens.Count && tokens[position + 1].Kind == DslTokenKind.Identifier)
                    {
                        Take(); var memberToken = WordToken(); var member = memberToken.Text; var receiver = new IdentifierExpression(keyword, span);
                        if (Is("=")) { Take(); return new AssignmentStatement(member, "=", Expression(), span) { Receiver = receiver, MemberName = member, MemberSpan = memberToken.Span }; }
                        var memberAccess = new MemberAccessExpression(receiver, member, span) { MemberSpan = memberToken.Span };
                        if (CanStartArgument()) { var memberArgs = new List<DslArgument>(); while (CanStartArgument()) memberArgs.Add(ParseArgument()); return new CallStatement(new CallExpression(member, memberArgs, span) { Receiver = receiver, NameSpan = memberToken.Span }, span); }
                        return new CallStatement(memberAccess, span);
                    }
                    if (Is("=") || Is("+=") || Is("-=")) { var op = Take().Text; return new AssignmentStatement(keyword, op, Expression(), span) { NameSpan = span }; }
                    var args = new List<DslArgument>(); while (CanStartArgument()) args.Add(ParseArgument()); return new CallStatement(new CallExpression(keyword, args, span) { NameSpan = span }, span);
            }
        }
        private DialogueStatement ParseDialogue(string character, SourceSpan span)
        {
            var colon = Take();
            var lineEnd = sourceText.IndexOf('\n', colon.Span.Start + colon.Span.Length);
            if (lineEnd < 0) lineEnd = sourceText.Length;
            var contentStart = colon.Span.Start + colon.Span.Length;
            if (FindInstaMarker(sourceText.Substring(contentStart, Math.Max(0, lineEnd - contentStart))) < 0)
                return new DialogueStatement(character, RawText(contentStart), span) { CharacterSpan = span };
            return ParseNarrativeLine(character, span, colon, (text, insta) => new DialogueStatement(character, text, span) { CharacterSpan = span, Insta = insta });
        }
        private T ParseNarrativeLine<T>(string keyword, SourceSpan span, DslToken colon, Func<DslExpression, DslExpression?, T> factory)
        {
            var lineEnd = sourceText.IndexOf('\n', colon.Span.Start + colon.Span.Length);
            if (lineEnd < 0) lineEnd = sourceText.Length;
            var contentStart = colon.Span.Start + colon.Span.Length;
            var content = sourceText.Substring(contentStart, Math.Max(0, lineEnd - contentStart));
            var marker = FindInstaMarker(content);
            DslExpression? insta = null;
            var textEnd = marker < 0 ? content.Length : marker;
            if (marker >= 0)
            {
                var markerOffset = contentStart + marker;
                while (position < tokens.Count && tokens[position].Span.Start < markerOffset) position++;
                Need("insta"); Need(":"); insta = Expression();
                if (Current.Kind == DslTokenKind.Comma)
                    Error("The runtime dial API accepts one insta argument; multiple insta arguments are not supported.");
                while (Current.Kind != DslTokenKind.NewLine && Current.Kind != DslTokenKind.EndOfFile) Take();
            }
            else
            {
                while (Current.Kind != DslTokenKind.NewLine && Current.Kind != DslTokenKind.EndOfFile) Take();
            }
            var text = StripComment(content.Substring(0, textEnd)).Trim();
            if (text.Length == 0) Error("Dialogue text cannot be empty.");
            var expression = new LiteralExpression(text, text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"' ? "string" : "raw-string", SpanAt(contentStart, text.Length));
            return factory(expression, insta);
        }
        private static int FindInstaMarker(string content)
        {
            var quoted = false;
            for (var i = 0; i + 5 < content.Length; i++)
            {
                if (content[i] == '"' && (i == 0 || content[i - 1] != '\\')) quoted = !quoted;
                if (!quoted && content.IndexOf("insta:", i, StringComparison.Ordinal) == i
                    && (i == 0 || !char.IsLetterOrDigit(content[i - 1]) && content[i - 1] != '_')) return i;
            }
            return -1;
        }
        private NamedCutsceneStatement ParseNamedCutscene(SourceSpan span)
        {
            var id = WordToken(); SkipHeaderNewLines(); var title = ParseSimpleExpression(); var args = new List<DslExpression>();
            while (Current.Kind != DslTokenKind.EndOfFile)
            {
                SkipHeaderNewLines();
                if (Is(":") || Current.Kind == DslTokenKind.EndOfFile) break;
                // Named-cutscene arguments are normally simple atoms, but a
                // generated C# array is emitted as the existing SEG list
                // literal. Parse that structured expression instead of
                // letting ParseSimpleExpression stall on '['.
                args.Add(Current.Kind == DslTokenKind.LBracket ? Expression() : ParseSimpleExpression());
            }
            Need(":"); return new NamedCutsceneStatement(id.Text, title, args, ParseBody(false), span) { IdSpan = id.Span };
        }
        private void SkipHeaderNewLines() { while (Current.Kind == DslTokenKind.NewLine) Take(); }
        private NarImgStatement ParseNarImg(SourceSpan span)
        {
            var path = ParseSimpleExpression(); string? size = null; var show = false;
            while (!Is(":") && Current.Kind != DslTokenKind.NewLine && Current.Kind != DslTokenKind.EndOfFile)
            {
                var modifier = Word();
                if (modifier == "size") size = Word();
                else if (modifier == "show-in-text") show = true;
                else Error($"Unknown nar-img modifier '{modifier}'.");
            }
            Need(":"); return new NarImgStatement(path, size, show, RawTextAfterKeyword(), span);
        }
        private DslExpression ParseSimpleExpression()
        {
            var span = Current.Span;
            if (Current.Kind == DslTokenKind.String) return new LiteralExpression(Take().Text, "string", span);
            if (Current.Kind == DslTokenKind.Number) return new LiteralExpression(Take().Text, "number", span);
            if (Is("null")) { Take(); return new LiteralExpression("null", "null", span); }
            var token = WordToken(); return new IdentifierExpression(token.Text, token.Span);
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
        private DslExpression RawTextAfterKeyword()
        {
            var start = Current.Span.Start;
            var end = sourceText.IndexOf('\n', start);
            if (end < 0) end = sourceText.Length;
            while (position < tokens.Count && tokens[position].Kind != DslTokenKind.NewLine && tokens[position].Kind != DslTokenKind.EndOfFile) position++;
            var text = StripComment(sourceText.Substring(Math.Min(start, sourceText.Length), Math.Max(0, end - Math.Min(start, sourceText.Length)))).Trim();
            if (text.Length == 0) Error("Narrative text cannot be empty.");
            return new LiteralExpression(text, "raw-string", Current.Span);
        }
        private DslExpression RawText(int start)
        {
            var end = sourceText.IndexOf('\n', start); if (end < 0) end = sourceText.Length;
            while (position < tokens.Count && tokens[position].Kind != DslTokenKind.NewLine && tokens[position].Kind != DslTokenKind.EndOfFile) position++;
            var actualStart = Math.Min(start, sourceText.Length);
            var text = StripComment(sourceText.Substring(actualStart, Math.Max(0, end - actualStart))).Trim();
            if (text.Length == 0) Error("Dialogue text cannot be empty.");
            return new LiteralExpression(text, text.Length >= 2 && text[0] == '"' && text[text.Length - 1] == '"' ? "string" : "raw-string", SpanAt(actualStart, text.Length));
        }
        private static string StripComment(string text)
        {
            for (var i = 0; i + 1 < text.Length; i++)
                if (text[i] == '/' && text[i + 1] == '/' && (i == 0 || char.IsWhiteSpace(text[i - 1])))
                    return text.Substring(0, i);
            return text;
        }
        private DslExpression ParseCallAfterKeyword(SourceSpan span) { Error("The 'call' keyword is no longer part of the DSL syntax."); var name = Word(); return new CallExpression(name, Array.Empty<DslArgument>(), span); }
        private bool CanStartArgument()
        {
            profile.Count("CanStartArgument");
            return (Current.Kind is DslTokenKind.Identifier or DslTokenKind.Number or DslTokenKind.String or DslTokenKind.LParen or DslTokenKind.LBracket)
                && Current.Text is not ("and" or "or" or "if" or "then" or "else" or "elif" or "end" or "when" or "with" or "for" or "here")
                && (!parsingListComprehensionClause || Current.Text != "select");
        }
        private DslArgument ParseArgument()
        {
            profile.Count("ParseArgument");
            var started = Stopwatch.GetTimestamp();
            try
            {
                var span = Current.Span;
                if (Current.Kind == DslTokenKind.Identifier && position + 2 < tokens.Count && tokens[position + 1].Kind == DslTokenKind.Colon && tokens[position + 2].Kind != DslTokenKind.NewLine && tokens[position + 2].Kind != DslTokenKind.EndOfFile)
                {
                    profile.Count("named-arguments");
                    var name = Take().Text; Take();
                    var previousNamedArgumentMode = parsingCallArgument;
                    parsingCallArgument = true;
                    try { return new DslArgument(name, Expression(), span); }
                    finally { parsingCallArgument = previousNamedArgumentMode; }
                }
                var previous = parsingCallArgument;
                parsingCallArgument = true;
                try { return new DslArgument(null, Prefix(), span); }
                finally { parsingCallArgument = previous; }
            }
            finally { profile.Add("ParseArgument", Stopwatch.GetTimestamp() - started, 0); }
        }
        private DslExpression Expression(int minimumPrecedence = 0)
        {
            profile.Count("expressions");
            return profile.Measure("Expression", () => ExpressionCore(minimumPrecedence));
        }
        private DslExpression ExpressionCore(int minimumPrecedence = 0)
        {
            var left = Prefix();
            while (true)
            {
                ConsumeExpressionContinuation();
                var precedence = Precedence(Current.Text);
                if (precedence <= minimumPrecedence) break;
                var op = Take().Text;
                profile.Count("binary-expressions");
                if (IsComparison(op) && ContainsComparison(left))
                    diagnostics.Add(new DslDiagnostic("SEGDSL105", "Chained comparisons are not supported; use an explicit logical expression.", Current.Span));
                left = new BinaryExpression(op, left, Expression(precedence), left.Span);
            }
            return left;
        }
        private void ConsumeExpressionContinuation()
        { if (Current.Kind != DslTokenKind.NewLine) return; var next = position; while (tokens[next].Kind == DslTokenKind.NewLine) next++; if (Precedence(tokens[next].Text) > 0) while (position < next) Take(); }
        private static int Precedence(string op) => op switch { "or" => 1, "and" => 2, "not" => 3, "==" or "!=" or ">" or ">=" or "<" or "<=" => 4, "+" or "-" => 5, "*" or "/" or "%" => 6, _ => 0 };
        private static bool IsComparison(string op) => op is "==" or "!=" or ">" or ">=" or "<" or "<=";
        private static bool ContainsComparison(DslExpression expression) => expression is BinaryExpression binary && IsComparison(binary.Operator);
        private DslExpression Prefix()
        {
            var category = Current.Kind switch
            {
                DslTokenKind.String or DslTokenKind.Number => "primary",
                DslTokenKind.LBracket => "list-literal",
                _ => Is("not") ? "unary" : "primary"
            };
            return profile.Measure("Expression." + category, PrefixCore);
        }
        private DslExpression PrefixCore()
        {
            while (Current.Kind == DslTokenKind.NewLine) Take(); var span = Current.Span;
            if (Is("not")) { Take(); return new UnaryExpression("not", Expression(Precedence("not")), span); }
            if (Is("exists")) return ParseExists(span);
            if (Is("if"))
            {
                Take();
                var condition = Expression(); Need("then");
                var whenTrue = Expression(); Need("else");
                var whenFalse = Expression();
                return new ConditionalExpression(condition, whenTrue, whenFalse, span);
            }
            if (Current.Kind == DslTokenKind.LParen)
            {
                Take();
                // Parentheses explicitly re-enter expression parsing. This is
                // what makes `foo (bar a) b` different from `foo bar a b`:
                // the former has one nested call as its first argument,
                // while the latter has three application arguments.
                var previous = parsingCallArgument;
                parsingCallArgument = false;
                try
                {
                    var parenthesized = Expression();
                    Need(")");
                    return new ParenthesizedExpression(parenthesized, span);
                }
                finally { parsingCallArgument = previous; }
            }
            if (Current.Kind == DslTokenKind.LBracket)
            {
                profile.Count("list-literals");
                Take(); SkipTerminators();
                if (Is("from")) return ParseListComprehension(span);
                var elements = new List<DslExpression>();
                while (!Is("]") && Current.Kind != DslTokenKind.EndOfFile)
                {
                    elements.Add(Expression());
                    SkipTerminators();
                    if (!Is(",")) break;
                    Take(); SkipTerminators();
                }
                Need("]");
                DslExpression listExpression = new ListExpression(elements, span);
                while (Is("."))
                {
                    Take();
                    var memberToken = WordToken();
                    if (!parsingCallArgument && CanStartArgument())
                    {
                        var args = new List<DslArgument>();
                        while (CanStartArgument()) args.Add(ParseArgument());
                        listExpression = new CallExpression(memberToken.Text, args, span) { Receiver = listExpression, NameSpan = memberToken.Span };
                    }
                    else
                        listExpression = new MemberAccessExpression(listExpression, memberToken.Text, span) { MemberSpan = memberToken.Span };
                }
                return listExpression;
            }
            if (Current.Kind == DslTokenKind.String) return new LiteralExpression(Take().Text, "string", span);
            if (Current.Kind == DslTokenKind.Number) return new LiteralExpression(Take().Text, "number", span);
            if (Is("true") || Is("false")) return new LiteralExpression(Take().Text, "bool", span);
            if (Is("null")) { Take(); return new LiteralExpression("null", "null", span); }
            if (Is("new-cycle")) { Take(); return new LiteralExpression("new-cycle", "cycle", span); }
            if (Is("ref")) { Take(); return new FunctionReferenceExpression(Word(), span); }
            IdentifierExpression? identifier = null;
            DslExpression expression;
            if (Is("this")) { Take(); expression = new ThisExpression(span); }
            else { var identifierToken = WordToken(); identifier = new IdentifierExpression(identifierToken.Text, identifierToken.Span); expression = identifier; }
            while (Is("."))
            {
                Take(); var memberToken = WordToken();
                if (!parsingCallArgument && CanStartArgument())
                {
                    profile.Count("member-calls");
                    var args = new List<DslArgument>(); while (CanStartArgument()) args.Add(ParseArgument());
                    expression = new CallExpression(memberToken.Text, args, span) { Receiver = expression, NameSpan = memberToken.Span };
                }
                else { profile.Count("member-accesses"); expression = new MemberAccessExpression(expression, memberToken.Text, span) { MemberSpan = memberToken.Span }; }
            }
            if (expression is not IdentifierExpression)
                return expression;
            if (identifier is null) return expression;
            if (Is("not-seen-recently")) { Take(); return new CallExpression("not-seen-recently", new[] { new DslArgument(null, identifier, span), new DslArgument(null, Prefix(), Current.Span) }, span); }
            if (Is("was-seen-at-least-once")) { Take(); return new CallExpression("was-seen-at-least-once", new[] { new DslArgument(null, identifier, span) }, span); }
            // A named argument is still an argument of this call.  The
            // argument parser owns the `name: expression` distinction; do
            // not suppress the whole call merely because its first argument
            // is named (for example `helper flag: true`).
            if (!parsingCallArgument && CanStartArgument())
            {
                profile.Count("calls");
                var args = new List<DslArgument>(); while (CanStartArgument()) args.Add(ParseArgument()); return new CallExpression(identifier.Name, args, span) { NameSpan = identifier.Span };
            }
            return identifier;
        }
        private bool IsNamedArgumentStart()
        {
            profile.Count("IsNamedArgumentStart");
            return Current.Kind == DslTokenKind.Identifier
                && position + 2 < tokens.Count
                && tokens[position + 1].Kind == DslTokenKind.Colon
                && tokens[position + 2].Kind is not (DslTokenKind.NewLine or DslTokenKind.EndOfFile);
        }
        private ExistsExpression ParseExists(SourceSpan span)
        {
            Take(); Need("["); SkipTerminators(); Need("from");
            var collection = ParseCollectionExpression();
            var item = WordToken(); SkipTerminators(); Need("where");
            var predicate = Expression(); SkipTerminators(); Need("]");
            return new ExistsExpression(collection, item.Text, predicate, span) { ItemSpan = item.Span };
        }
        private ListComprehensionExpression ParseListComprehension(SourceSpan span)
        {
            Need("from");
            var collection = ParseCollectionExpression();
            var item = WordToken();
            SkipTerminators(); Need("where");
            var previousClause = parsingListComprehensionClause;
            parsingListComprehensionClause = true;
            DslExpression predicate;
            DslExpression selector;
            try
            {
                predicate = Expression();
                SkipTerminators(); Need("select");
                selector = Expression();
            }
            finally { parsingListComprehensionClause = previousClause; }
            SkipTerminators(); Need("]");
            return new ListComprehensionExpression(collection, item.Text, predicate, selector, span) { ItemSpan = item.Span };
        }
        private DslExpression ParseCollectionExpression()
        {
            var expression = ParseSimpleExpression();
            while (Is("."))
            {
                Take(); var member = WordToken();
                expression = new MemberAccessExpression(expression, member.Text, expression.Span) { MemberSpan = member.Span };
            }
            return expression;
        }
    }
}
