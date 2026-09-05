using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Segusum.Scripting.Core;
using Segusum.Scripting.Semantics;

namespace Segusum.Scripting.Generator;

[Generator]
public sealed class SegusumGenerator : IIncrementalGenerator
{
    private static readonly DiagnosticDescriptor Error = new("SEGDSL200", "Segusum DSL error", "{0}", "Segusum DSL", DiagnosticSeverity.Error, true);
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var files = context.AdditionalTextsProvider.Where(x => x.Path.EndsWith(".seg", StringComparison.OrdinalIgnoreCase))
            .Select((x, ct) => new DslSource(x.Path, x.GetText(ct)?.ToString() ?? ""));
        context.RegisterSourceOutput(files.Collect().Combine(context.CompilationProvider), (sp, pair) => Generate(sp, pair.Left, pair.Right));
    }
    private static void Generate(SourceProductionContext sp, ImmutableArray<DslSource> sources, Compilation compilation)
    {
        var parsed = sources.Select(x => (Source: x, Result: DslParser.Parse(x))).ToArray();
        foreach (var item in parsed) foreach (var diagnostic in item.Result.Diagnostics) sp.ReportDiagnostic(Diagnostic.Create(Error, ToLocation(diagnostic.Span), diagnostic.Message));
        var worldAttribute = compilation.GetTypeByMetadataName("Seg.SegusumWorldAttribute");
        var worldMap = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        foreach (var type in compilation.Assembly.GlobalNamespace.GetNamespaceMembers().SelectMany(NamespaceTypes))
        {
            var attribute = type.GetAttributes().FirstOrDefault(x => SymbolEqualityComparer.Default.Equals(x.AttributeClass, worldAttribute));
            if (attribute == null) continue;
            var id = attribute.ConstructorArguments.Length == 1 && attribute.ConstructorArguments[0].Value is string value ? value : "";
            var location = type.Locations.FirstOrDefault() ?? Location.None;
            if (string.IsNullOrWhiteSpace(id)) { sp.ReportDiagnostic(Diagnostic.Create(Error, location, "SegusumWorld id cannot be empty.")); continue; }
            if (!DerivesWorldBase(type)) { sp.ReportDiagnostic(Diagnostic.Create(Error, location, $"World '{id}' must derive from Seg.WorldBase.")); continue; }
            if (!IsPartial(type)) { sp.ReportDiagnostic(Diagnostic.Create(Error, location, $"World '{id}' must be declared partial.")); continue; }
            if (worldMap.ContainsKey(id)) sp.ReportDiagnostic(Diagnostic.Create(Error, location, $"Duplicate SegusumWorld id '{id}'."));
            else worldMap[id] = type;
        }
        if (parsed.Any(x => x.Result.Document.WorldId == null)) return;
        var groupedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var group in parsed.GroupBy(x => x.Result.Document.WorldId!, StringComparer.Ordinal))
        {
            if (!worldMap.TryGetValue(group.Key, out var world))
            {
                sp.ReportDiagnostic(Diagnostic.Create(Error, Location.None, $"Unknown SegusumWorld id '{group.Key}'."));
                continue;
            }
            groupedIds.Add(group.Key);
            GenerateWorld(sp, group.SelectMany(x => x.Result.Document.Declarations).ToArray(), compilation, world, group.Key);
        }
        foreach (var entry in worldMap.Where(x => !groupedIds.Contains(x.Key))) GenerateWorld(sp, Array.Empty<DslDeclaration>(), compilation, entry.Value, entry.Key);
    }
    private static void GenerateWorld(SourceProductionContext sp, IReadOnlyList<DslDeclaration> declarations, Compilation compilation, INamedTypeSymbol world, string worldId)
    {
        var semanticDiagnostics = new List<DslDiagnostic>();
        var binder = new DslBinder(compilation, world, semanticDiagnostics.Add);
        binder.Bind(declarations);
        foreach (var diagnostic in semanticDiagnostics) sp.ReportDiagnostic(Diagnostic.Create(Error, ToLocation(diagnostic.Span), diagnostic.Message));
        if (semanticDiagnostics.Count != 0) return;
        var sb = new StringBuilder(SegusumGeneratedSource.Marker + "\n#nullable enable\n#line hidden\nusing Seg;\nusing System.Linq;\nusing static Seg.Utils;\n");
        sb.Append("namespace ").Append(world.ContainingNamespace.ToDisplayString()).Append(";\n\n");
        sb.Append("partial class ").Append(world.Name).AppendLine("\n{");
        foreach (var state in declarations.OfType<StateDeclaration>()) { EmitLine(sb, state.Span); sb.Append(" private ").Append(Type(state.Type)).Append(' ').Append(Name(state.Name)).Append(" = ").Append(Emit(state.Initializer, binder.Model)).AppendLine(";"); EmitDefaultLine(sb); }
        foreach (var cycle in declarations.OfType<CycleDeclaration>()) { EmitLine(sb, cycle.Span); sb.Append(" private readonly Cycle ").Append(Name(cycle.Variable)).AppendLine(" = new Cycle();"); EmitDefaultLine(sb); }
        foreach (var element in declarations.SelectMany(AllCycleElements).GroupBy(x => Name(x.Id), StringComparer.Ordinal).Select(x => x.First()))
        { EmitLine(sb, element.Span); sb.Append(" public CycleElemId ").Append(Name(element.Id)).AppendLine(" { get; set; } = new();"); EmitDefaultLine(sb); }
        foreach (var id in declarations.SelectMany(AllNamedCutscenes).GroupBy(x => Name(x.Id), StringComparer.Ordinal).Select(x => x.First()))
        { EmitLine(sb, id.Span); sb.Append(" public NamedCutSceneId ").Append(Name(id.Id)).Append(" = new NamedCutSceneId { serId = \"").Append(EscapeString(id.Id)).Append("\", titleUntranslated = ").Append(Emit(id.Title, binder.Model)).AppendLine(".translatable() };"); EmitDefaultLine(sb); }
        foreach (var function in declarations.OfType<FunctionDeclaration>()) EmitFunction(sb, function, binder.Model);
        foreach (var before in declarations.OfType<BeforeRoomChangeDeclaration>().Take(1)) EmitBeforeRoomChange(sb, before, binder.Model);
        sb.AppendLine("#line hidden\n protected override void configureGeneratedActionHandlers()\n {");
        foreach (var handler in declarations.OfType<HandlerDeclaration>()) EmitHandler(sb, handler, sp, binder.Model);
        foreach (var element in declarations.OfType<CycleElementDeclaration>()) EmitCycleElement(sb, element, sp, "  ", binder.Model);
        foreach (var next in declarations.OfType<NextCycleDeclaration>()) { EmitLine(sb, next.Span); sb.Append("  execNextInCycle(").Append(Emit(next.Cycle, binder.Model)).AppendLine(");"); EmitDefaultLine(sb); }
        sb.AppendLine(" #line hidden\n }\n}\n#line default");
        sp.AddSource("Segusum.Generated." + SanitizeHint(worldId) + ".g.cs", sb.ToString());
    }
    private static Location ToLocation(SourceSpan span) => Location.Create(span.Path, new Microsoft.CodeAnalysis.Text.TextSpan(span.Start, span.Length), new Microsoft.CodeAnalysis.Text.LinePositionSpan(new(span.Line - 1, span.Column - 1), new(span.Line - 1, span.Column - 1 + Math.Max(1, span.Length))));
    private static IEnumerable<INamedTypeSymbol> NamespaceTypes(INamespaceSymbol ns) { foreach (var t in ns.GetTypeMembers()) yield return t; foreach (var n in ns.GetNamespaceMembers()) foreach (var t in NamespaceTypes(n)) yield return t; }
    private static bool DerivesWorldBase(INamedTypeSymbol type) { for (var b = type.BaseType; b != null; b = b.BaseType) if (b.ToDisplayString() == "Seg.WorldBase") return true; return false; }
    private static bool IsPartial(INamedTypeSymbol type) => type.DeclaringSyntaxReferences.Any(x => x.GetSyntax() is Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax declaration && declaration.Modifiers.Any(Microsoft.CodeAnalysis.CSharp.SyntaxKind.PartialKeyword));
    private static string SanitizeHint(string value) => new(value.Select(x => char.IsLetterOrDigit(x) ? x : '_').ToArray());
    private static void EmitFunction(StringBuilder sb, FunctionDeclaration function, BoundModel model)
    {
        EmitLine(sb, function.Span);
        sb.Append(" private ").Append(Type(function.ReturnType ?? "void")).Append(' ').Append(Name(function.Name)).Append('(').Append(string.Join(",", function.Parameters.Select(x => Type(x.Type) + " " + Name(x.Name)))).AppendLine(")\n {");
        EmitDefaultLine(sb);
        foreach (var statement in function.Body) EmitStatement(sb, statement, "  ", null, model);
        sb.AppendLine(" #line hidden\n }");
        EmitDefaultLine(sb);
    }
    private static void EmitBeforeRoomChange(StringBuilder sb, BeforeRoomChangeDeclaration declaration, BoundModel model)
    {
        EmitLine(sb, declaration.Span);
        sb.AppendLine(" private void beforeRoomChangeSegusum(Room from, Room to, WalkPath fromToSegment, WalkPath fullPath, BeforeRoomChangeInput e)");
        sb.AppendLine(" {"); EmitDefaultLine(sb);
        foreach (var statement in declaration.Body) EmitStatement(sb, statement, "  ", "e", model);
        sb.AppendLine(" #line hidden\n }"); EmitDefaultLine(sb);
    }
    private static void EmitHandler(StringBuilder sb, HandlerDeclaration handler, SourceProductionContext sp, BoundModel model)
    {
        EmitLine(sb, handler.Span);
        if (handler.Kind == "combine") sb.Append("  addHandlerCombine(").Append(EmitIdentifier(handler.First, model)).Append(',').Append(EmitIdentifier(handler.Second!, model)).Append(',').Append(Emit(handler.Phrase ?? new LiteralExpression("\"\"", "string", handler.Span), model));
        else if (handler.Kind == "use-for") sb.Append("  addHandlerUseFor(").Append(EmitIdentifier(handler.First, model)).Append(',').Append(EmitIdentifier(handler.Target!, model));
        else if (handler.Kind == "room-changed") sb.Append("  addRoomChangedHandler(").Append(EmitIdentifier(handler.First, model));
        else if (handler.Kind == "pickup") sb.Append("  addHandlerPickUp(").Append(EmitIdentifier(handler.First, model));
        else if (handler.Kind == "talk-here") sb.Append("  addHandlerTalkHere(").Append(EmitIdentifier(handler.First, model));
        else if (handler.Kind == "cancel-text-input") sb.Append("  addHandlerCancelTextInput(").Append(EmitIdentifier(handler.First, model));
        else if (handler.Kind == "submit-text-input") sb.Append("  addHandlerSubmitTextInput(").Append(EmitIdentifier(handler.First, model));
        else sb.Append("  addHandlerUseHere(").Append(EmitIdentifier(handler.First, model));
        if (handler.Explanation != null && handler.Kind == "use-for") sb.Append(", ").Append(Emit(handler.Explanation, model));
        else if (handler.Explanation != null && handler.Kind == "combine") sb.Append(", explanation: ").Append(Emit(handler.Explanation, model));
        else if (handler.Explanation != null) sp.ReportDiagnostic(Diagnostic.Create(Error, ToLocation(handler.Explanation.Span), "Explanation is not supported by the use-here runtime API."));
        if (handler.Kind == "use-here" && handler.Phrase != null) sb.Append(", ").Append(Emit(handler.Phrase, model));
        sb.Append(", handler: e => {\n"); EmitDefaultLine(sb); foreach (var statement in handler.Body) EmitStatement(sb, statement, "   ", "e", model); sb.Append("#line hidden\n  }");
        if (handler.Condition != null) { sb.Append(", isPossibleNow: () =>\n"); EmitLine(sb, handler.Condition.Span); sb.Append(Emit(handler.Condition, model)).AppendLine(); EmitDefaultLine(sb); }
        sb.AppendLine(");"); EmitDefaultLine(sb);
    }
    private static void EmitCycleElement(StringBuilder sb, CycleElementDeclaration element, SourceProductionContext sp, string indent, BoundModel model)
    { EmitAdd(sb, element.Cycle, element.Id, element.Important, element.Repeat, element.Condition, element.Body, element.Span, sp, indent, model); }
    private static void EmitAdd(StringBuilder sb, string cycle, string id, bool important, string? repeat, DslExpression? condition, IReadOnlyList<DslStatement> body, SourceSpan span, SourceProductionContext sp, string indent, BoundModel model)
    {
        EmitLine(sb, span);
        sb.Append(indent).Append(EmitIdentifier(cycle, model)).Append(".addToCycle(").Append(EmitIdentifier(id, model));
        if (important) sb.Append(", Importance.Important");
        if (repeat is "once" or "forever") sb.Append(", Repeat.").Append(repeat == "once" ? "OnlyOnce" : "Forever");
        if (condition != null) { sb.Append(", x =>\n"); EmitLine(sb, condition.Span); sb.Append(Emit(condition, model)).AppendLine(); EmitDefaultLine(sb); }
        sb.Append(", x => {\n"); EmitDefaultLine(sb); foreach (var statement in body) EmitStatement(sb, statement, indent + "  ", null, model); sb.Append("#line hidden\n").Append(indent).AppendLine("});"); EmitDefaultLine(sb);
    }
    private static void EmitStatement(StringBuilder sb, DslStatement statement, string indent, string? input, BoundModel model)
    {
        EmitLine(sb, statement.Span);
        switch (statement)
        {
            case VariableDeclaration v: sb.Append(indent).Append("var ").Append(Name(v.Name)).Append(" = ").Append(Emit(v.Initializer, model)).AppendLine(";"); break;
            case AssignmentStatement a: sb.Append(indent).Append(a.Receiver == null ? Name(a.Name) : Emit(a.Receiver, model) + "." + Name(a.MemberName ?? a.Name)).Append(a.Operator).Append(Emit(a.Value, model)).AppendLine(";"); break;
            case IncrementStatement i: sb.Append(indent).Append(Name(i.Name)).AppendLine("++;"); break;
            case ReturnStatement r: sb.Append(indent).Append("return ").Append(Emit(r.Expression, model)).AppendLine(";"); break;
            case CallStatement c: sb.Append(indent).Append(Emit(c.Expression, model)).AppendLine(";"); break;
            case NarStatement n: sb.Append(indent).Append("narText(").Append(Emit(n.Text, model)).AppendLine(");"); break;
            case NarRoomStatement n: sb.Append(indent).Append("narRoom(").Append(Emit(n.Text, model)).Append(", curRoom, false, false);").AppendLine(); break;
            case NarImgStatement n:
                sb.Append(indent).Append("narImg(").Append(Emit(n.Text, model)).Append(", ").Append(Emit(n.ImagePath, model));
                if (n.Size == "medium") sb.Append(", NarSize.Medium"); else if (n.Size == "fullscreen") sb.Append(", NarSize.FullScreen");
                if (n.ShowInText) sb.Append(", alsoShowGraphicsInTextMode: true");
                sb.AppendLine(");"); break;
            case DialogueStatement d:
                sb.Append(indent).Append("dial(").Append(EmitIdentifier(d.Character, model)).Append(',').Append(Emit(d.Text, model));
                if (d.Insta != null) sb.Append(", insta: ").Append(Emit(d.Insta, model));
                sb.AppendLine(");"); break;
            case TextInputStatement t: sb.Append(indent).Append(input ?? "e").Append(".textInputToShow = ").Append(Emit(t.TextInput, model)).AppendLine(";"); break;
            case MarkHappenedOnceStatement mark: sb.Append(indent).Append("setIfNeverHappened(ref ").Append(Emit(mark.Target, model)).AppendLine(");"); break;
            case MarkHappenedStatement mark: sb.Append(indent).Append(Emit(mark.Target, model)).AppendLine(" = System.DateTime.Now;"); break;
            case NamedCutsceneStatement n:
                sb.Append(indent).Append("using (namedCutScene(").Append(EmitIdentifier(n.Id, model));
                if (n.Arguments.Count != 0) sb.Append(", ").Append(string.Join(", ", n.Arguments.Select(x => Emit(x, model))));
                sb.AppendLine("))"); sb.Append(indent).AppendLine("{");
                foreach (var child in n.Body) EmitStatement(sb, child, indent + "  ", input, model);
                sb.Append(indent).AppendLine("}"); break;
            case NextCycleStatement n: sb.Append(indent).Append("execNextInCycle(").Append(Emit(n.Cycle, model)).AppendLine(");"); break;
            case AddCycleElementStatement a: EmitAdd(sb, a.Cycle, a.Id, a.Important, a.Repeat, a.Condition, a.Body, a.Span, default, indent, model); break;
            case IfStatement i:
                for (var index = 0; index < i.Branches.Count; index++) { sb.Append(indent).Append(index == 0 ? "if (" : "else if (").Append(Emit(i.Branches[index].Condition, model)).AppendLine(")\n" + indent + "{"); foreach (var child in i.Branches[index].Body) EmitStatement(sb, child, indent + "  ", input, model); sb.Append(indent).AppendLine("}"); }
                if (i.ElseBody != null) { sb.Append(indent).AppendLine("else\n" + indent + "{"); foreach (var child in i.ElseBody) EmitStatement(sb, child, indent + "  ", input, model); sb.Append(indent).AppendLine("}"); } break;
            case MakesNoSenseStatement when input != null: sb.Append(indent).Append(input).AppendLine(".makesNoSenseAtThisTime = true;"); break;
            case FinishGameStatement when input != null: sb.Append(indent).Append(input).AppendLine(".gameFinished = true;"); break;
            case DoNotAdvanceTimeStatement when input != null: sb.Append(indent).Append(input).AppendLine(".timeMustAdvance = false;"); break;
            case PreventRoomChangeStatement when input != null: sb.Append(indent).Append(input).AppendLine(".canChangeRoom = false;"); break;
            default: sb.Append(indent).AppendLine("/* SEGDSL: unsupported statement */"); break;
        }
        EmitDefaultLine(sb);
    }
    private static void EmitLine(StringBuilder sb, SourceSpan span) => sb.Append("#line ").Append(Math.Max(1, span.Line)).Append(" \"").Append(EscapeLinePath(span.Path)).AppendLine("\"");
    private static void EmitDefaultLine(StringBuilder sb) => sb.AppendLine("#line default");
    private static string EscapeLinePath(string path) => path.Replace("\\", "\\\\").Replace("\"", "\\\"");
    private static string EmitIdentifier(string name, BoundModel model) => model.References.TryGetValue(name, out var resolved) ? resolved : Name(name);
    private static string Emit(DslExpression expression, BoundModel model) => expression switch
    {
        IdentifierExpression i => model.Values.TryGetValue(i, out var value) ? (value.Kind is BoundSymbolKind.CSharpMethod or BoundSymbolKind.Function ? value.CSharpName + "()" : value.CSharpName) : Name(i.Name), LiteralExpression l => l.Kind == "cycle" ? "new Cycle()" : l.Kind == "raw-string" ? "\"" + EscapeString(l.Value) + "\"" : l.Value,
        ParenthesizedExpression p => "(" + Emit(p.Expression, model) + ")", UnaryExpression u => EmitUnary(u, model),
        BinaryExpression b => Emit(b.Left, model) + " " + (b.Operator == "and" ? "&&" : b.Operator == "or" ? "||" : b.Operator) + " " + Emit(b.Right, model),
        CallExpression c when model.DomainOperations.TryGetValue(c, out var domain) && domain.Kind == BoundDomainOperationKind.NotSeenRecently => Emit(domain.Receiver, model) + ".notSeenRecently(" + Emit(domain.Argument!, model) + ")",
        CallExpression c when model.DomainOperations.TryGetValue(c, out var seen) && seen.Kind == BoundDomainOperationKind.WasSeenAtLeastOnce => "wasSeenAtLeastOnce(" + Emit(seen.Receiver, model) + ")",
        ExistsExpression e => "System.Linq.Enumerable.Any(" + Emit(e.Collection, model) + ", " + Name(e.ItemName) + " => " + Emit(e.Predicate, model) + ")",
        MemberAccessExpression m when model.Values.TryGetValue(m, out var words) && words.CSharpName == "splittaInputEFaiLower(e)" => words.CSharpName,
        MemberAccessExpression m when model.Values.TryGetValue(m, out var member) && member.Kind == BoundSymbolKind.CSharpMethod => Emit(m.Receiver, model) + "." + member.CSharpName + "()",
        MemberAccessExpression m when model.Values.TryGetValue(m, out var property) => Emit(m.Receiver, model) + "." + property.CSharpName,
        CallExpression c when model.Calls.TryGetValue(c, out var bound) => (bound.Receiver == null ? bound.TargetName : Emit(bound.Receiver, model) + "." + bound.TargetName) + "(" + string.Join(",", bound.Arguments.Select(a => (a.Source.Name == null ? "" : Name(a.ParameterName) + ": ") + Emit(a.Source.Expression, model))) + ")",
        _ => "default"
    };
    private static string EmitUnary(UnaryExpression expression, BoundModel model)
    {
        var operand = Emit(expression.Operand, model);
        if (expression.Operator == "not" && expression.Operand is BinaryExpression)
            operand = "(" + operand + ")";
        return (expression.Operator == "not" ? "!" : expression.Operator) + operand;
    }
    private static string Name(string name) { var x = name.Contains('-') ? DslNames.Camel(name) : name; return x is "object" or "string" or "int" or "bool" ? "@" + x : x; }
    private static string EscapeString(string value) => value.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\r", "\\r").Replace("\n", "\\n");
    private static string Type(string type) => type == "int" ? "int" : type == "bool" ? "bool" : type == "string" ? "string" : type == "DateTime" ? "System.DateTime" : type;
    private static IEnumerable<CycleElementDeclaration> AllCycleElements(DslDeclaration declaration) => declaration switch
    {
        CycleElementDeclaration e => new[] { e }.Concat(FindNested(e.Body)),
        HandlerDeclaration h => FindNested(h.Body),
        FunctionDeclaration f => FindNested(f.Body),
        _ => Enumerable.Empty<CycleElementDeclaration>()
    };
    private static IEnumerable<CycleElementDeclaration> FindNested(IEnumerable<DslStatement> statements) => statements.SelectMany(s => s switch
    {
        AddCycleElementStatement a => new[] { new CycleElementDeclaration(a.Cycle, a.Id, a.Important, a.Repeat, a.Condition, a.Body, a.Span) }.Concat(FindNested(a.Body)),
        IfStatement i => i.Branches.SelectMany(x => FindNested(x.Body)).Concat(i.ElseBody == null ? Enumerable.Empty<CycleElementDeclaration>() : FindNested(i.ElseBody)),
        NamedCutsceneStatement n => FindNested(n.Body),
        _ => Enumerable.Empty<CycleElementDeclaration>()
    });
    private static IEnumerable<NamedCutsceneStatement> AllNamedCutscenes(DslDeclaration declaration) => declaration switch
    {
        HandlerDeclaration h => FindNamedCutscenes(h.Body), FunctionDeclaration f => FindNamedCutscenes(f.Body), CycleElementDeclaration c => FindNamedCutscenes(c.Body), _ => Enumerable.Empty<NamedCutsceneStatement>()
    };
    private static IEnumerable<NamedCutsceneStatement> FindNamedCutscenes(IEnumerable<DslStatement> statements) => statements.SelectMany(s => s switch
    {
        NamedCutsceneStatement n => new[] { n }.Concat(FindNamedCutscenes(n.Body)), IfStatement i => i.Branches.SelectMany(x => FindNamedCutscenes(x.Body)).Concat(i.ElseBody == null ? Enumerable.Empty<NamedCutsceneStatement>() : FindNamedCutscenes(i.ElseBody)), AddCycleElementStatement a => FindNamedCutscenes(a.Body), _ => Enumerable.Empty<NamedCutsceneStatement>()
    });
}
