using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Segusum.Scripting.Core;

namespace Segusum.Scripting.Generator;

internal enum BoundSymbolKind { Local, Parameter, State, Function, Cycle, CycleElementId, CSharpField, CSharpProperty, CSharpMethod, ContextualIt }
internal sealed record BoundValue(ITypeSymbol? Type, string CSharpName, ISymbol? Symbol, BoundSymbolKind Kind);
internal sealed record BoundArgument(DslArgument Source, IParameterSymbol? Parameter, string ParameterName);
internal sealed record BoundCall(IMethodSymbol? Method, string TargetName, IReadOnlyList<BoundArgument> Arguments, ITypeSymbol? ReturnType);
internal enum BoundDomainOperationKind { NotSeenRecently, WasSeenAtLeastOnce }
internal sealed record BoundDomainOperation(BoundDomainOperationKind Kind, DslExpression Receiver, DslExpression? Argument, IMethodSymbol? Method);
internal sealed class BoundModel
{
    public Dictionary<DslExpression, BoundValue> Values { get; } = new(ReferenceComparer<DslExpression>.Instance);
    public Dictionary<DslExpression, BoundCall> Calls { get; } = new(ReferenceComparer<DslExpression>.Instance);
    public Dictionary<DslExpression, BoundDomainOperation> DomainOperations { get; } = new(ReferenceComparer<DslExpression>.Instance);
    public Dictionary<string, string> References { get; } = new(StringComparer.Ordinal);
}
internal sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
{
    public static readonly ReferenceComparer<T> Instance = new();
    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
    public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
}

internal sealed class DslBinder
{
    private readonly Compilation compilation;
    private readonly INamedTypeSymbol world;
    private readonly Action<DslDiagnostic> report;
    private readonly Dictionary<string, ITypeSymbol> globals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BoundSymbolKind> globalKinds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FunctionDeclaration> functions = new(StringComparer.Ordinal);
    private readonly BoundModel model = new();
    private readonly INamedTypeSymbol? cycle;
    private readonly INamedTypeSymbol? cycleElementId;
    private readonly ITypeSymbol? dateTimeNullable;
    private readonly INamedTypeSymbol? logicObj;
    private readonly INamedTypeSymbol? objective;
    private readonly INamedTypeSymbol? explanation;
    private ISymbol? lastSymbol;
    private BoundSymbolKind lastKind;
    private string lastCSharpName = "";
    private readonly HashSet<string> currentParameters = new(StringComparer.Ordinal);

    public BoundModel Model => model;
    public DslBinder(Compilation compilation, INamedTypeSymbol world, Action<DslDiagnostic> report)
    {
        this.compilation = compilation; this.world = world; this.report = report;
        cycle = compilation.GetTypeByMetadataName("Seg.Cycle"); cycleElementId = compilation.GetTypeByMetadataName("Seg.CycleElemId");
        logicObj = compilation.GetTypeByMetadataName("Seg.LogicObj"); objective = compilation.GetTypeByMetadataName("Seg.Objective"); explanation = compilation.GetTypeByMetadataName("Seg.Explanation");
        var dateTime = compilation.GetSpecialType(SpecialType.System_DateTime); dateTimeNullable = compilation.GetSpecialType(SpecialType.System_Nullable_T).Construct(dateTime);
    }
    public void Bind(IReadOnlyList<DslDeclaration> declarations)
    {
        foreach (var state in declarations.OfType<StateDeclaration>()) AddGlobal(state.Name, TypeOf(state.Type), state.Span, BoundSymbolKind.State);
        foreach (var cycleDeclaration in declarations.OfType<CycleDeclaration>()) AddGlobal(cycleDeclaration.Variable, cycle, cycleDeclaration.Span, BoundSymbolKind.Cycle);
        foreach (var element in declarations.OfType<CycleElementDeclaration>()) AddGlobal(element.Id, cycleElementId, element.Span, BoundSymbolKind.CycleElementId);
        foreach (var element in declarations.SelectMany(FindNestedElements)) AddGlobal(element.Id, cycleElementId, element.Span, BoundSymbolKind.CycleElementId);
        foreach (var function in declarations.OfType<FunctionDeclaration>()) { var key = NormalizeKey(function.Name); if (functions.ContainsKey(key)) Report("SEGDSL303", "Duplicate DSL function.", function.Span); else functions[key] = function; }
        foreach (var declaration in declarations)
        {
            switch (declaration)
            {
                case StateDeclaration s: BindExpression(s.Initializer, new()); break;
                case FunctionDeclaration f: BindFunction(f); break;
                case HandlerDeclaration h: BindHandler(h); break;
                case CycleElementDeclaration c: BindCycle(c.Cycle, c.Repeat, c.Condition, c.Body, c.Span); break;
                case NextCycleDeclaration n: Require(BindExpression(n.Cycle, new()), cycle, n.Cycle.Span, "next requires a Cycle."); break;
            }
        }
        CheckDuplicateElements(declarations);
        CheckDuplicateCombines(declarations);
    }
    private void AddGlobal(string name, ITypeSymbol? type, SourceSpan span, BoundSymbolKind kind)
    { var key = NormalizeKey(name); if (globals.ContainsKey(key)) Report("SEGDSL304", $"Duplicate or normalized-colliding global '{name}'.", span); else if (type != null) { if (kind == BoundSymbolKind.CycleElementId && ResolveCSharpCandidates(name).Count != 0) Report("SEGDSL317", $"CycleElementId '{name}' collides with an existing World member.", span); globals[key] = type; globalKinds[key] = kind; model.References[name] = Name(name); } }
    private void BindFunction(FunctionDeclaration f)
    { var scope = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal); currentParameters.Clear(); foreach (var p in f.Parameters) { scope[NormalizeKey(p.Name)] = TypeOf(p.Type)!; currentParameters.Add(NormalizeKey(p.Name)); } BindStatements(f.Body, scope, f.ReturnType == null ? null : TypeOf(f.ReturnType)); currentParameters.Clear(); }
    private void BindHandler(HandlerDeclaration h)
    {
        var first = BindName(h.First, h.Span); var second = h.Second == null ? null : BindName(h.Second, h.Span); var target = h.Target == null ? null : BindName(h.Target, h.Span);
        if (h.Kind == "combine") { Require(first, logicObj, h.Span, "combine first operand must be LogicObj."); Require(second, logicObj, h.Span, "combine second operand must be LogicObj."); }
        if (h.Kind == "use-for") { Require(first, logicObj, h.Span, "use-for object must be LogicObj."); Require(target, objective, h.Span, "use-for target must be Objective."); }
        if (h.Kind == "use-here") Require(first, logicObj, h.Span, "use-here object must be LogicObj.");
        if (h.Explanation != null) Require(BindExpression(h.Explanation, new()), explanation, h.Explanation.Span, "exp must be Explanation.");
        if (h.Condition != null) Require(BindExpression(h.Condition, new()), compilation.GetSpecialType(SpecialType.System_Boolean), h.Condition.Span, "possible-when must be bool.");
        BindStatements(h.Body, new(), null);
    }
    private void BindCycle(string cycleName, string? repeat, DslExpression? condition, IReadOnlyList<DslStatement> body, SourceSpan span)
    { Require(BindName(cycleName, span), cycle, span, "add requires a Cycle."); if (repeat != null && repeat is not ("once" or "forever")) Report("SEGDSL316", $"Unknown Repeat modifier '{repeat}'.", span); if (condition != null) Require(BindExpression(condition, new(), dateTimeNullable), compilation.GetSpecialType(SpecialType.System_Boolean), condition.Span, "when must be bool."); BindStatements(body, new(), null); }
    private void BindStatements(IEnumerable<DslStatement> statements, Dictionary<string, ITypeSymbol> scope, ITypeSymbol? returnType)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case VariableDeclaration v:
                    var type = BindExpression(v.Initializer, scope); if (type != null) scope[NormalizeKey(v.Name)] = type; break;
                case AssignmentStatement a: Require(BindName(a.Name, a.Span, scope), BindExpression(a.Value, scope), a.Span, "assignment type mismatch."); break;
                case IncrementStatement i: Require(BindName(i.Name, i.Span, scope), compilation.GetSpecialType(SpecialType.System_Int32), i.Span, "++ requires int."); break;
                case ReturnStatement r: Require(BindExpression(r.Expression, scope), returnType, r.Span, "return type mismatch."); break;
                case CallStatement c: BindExpression(c.Expression, scope); break;
                case NextCycleStatement n: Require(BindExpression(n.Cycle, scope), cycle, n.Span, "next requires a Cycle."); break;
                case AddCycleElementStatement a: BindCycle(a.Cycle, a.Repeat, a.Condition, a.Body, a.Span); break;
                case IfStatement i:
                    foreach (var branch in i.Branches) { Require(BindExpression(branch.Condition, scope), compilation.GetSpecialType(SpecialType.System_Boolean), branch.Condition.Span, "if condition must be bool."); BindStatements(branch.Body, new(scope), returnType); }
                    if (i.ElseBody != null) BindStatements(i.ElseBody, new(scope), returnType); break;
                case DialogueStatement d: Require(BindName(d.Character, d.Span, scope), compilation.GetTypeByMetadataName("Seg.Character"), d.Span, "dialogue speaker must be Character."); Require(BindExpression(d.Text, scope), compilation.GetSpecialType(SpecialType.System_String), d.Text.Span, "dialogue text must be string."); break;
                case NarStatement n: Require(BindExpression(n.Text, scope), compilation.GetSpecialType(SpecialType.System_String), n.Text.Span, "nar text must be string."); break;
                case NarRoomStatement n: Require(BindExpression(n.Text, scope), compilation.GetSpecialType(SpecialType.System_String), n.Text.Span, "nar-room text must be string."); break;
            }
        }
    }
    private ITypeSymbol? BindExpression(DslExpression expression, Dictionary<string, ITypeSymbol> scope, ITypeSymbol? contextualIt = null)
    {
        switch (expression)
        {
            case LiteralExpression l: return l.Kind == "string" ? compilation.GetSpecialType(SpecialType.System_String) : l.Kind == "bool" ? compilation.GetSpecialType(SpecialType.System_Boolean) : l.Kind == "cycle" ? cycle : compilation.GetSpecialType(SpecialType.System_Int32);
            case IdentifierExpression i:
                if (i.Name == "it" && contextualIt != null) { model.Values[i] = new BoundValue(contextualIt, "x", null, BoundSymbolKind.ContextualIt); return contextualIt; }
                var resolved = BindName(i.Name, i.Span, scope);
                if (resolved != null) model.Values[i] = new BoundValue(resolved, lastCSharpName, lastSymbol, lastKind);
                return resolved;
            case ParenthesizedExpression p: return BindExpression(p.Expression, scope, contextualIt);
            case UnaryExpression u: var ut = BindExpression(u.Operand, scope, contextualIt); if (u.Operator == "not") Require(ut, compilation.GetSpecialType(SpecialType.System_Boolean), u.Span, "not requires bool."); return compilation.GetSpecialType(SpecialType.System_Boolean);
            case BinaryExpression b:
                var lt = BindExpression(b.Left, scope, contextualIt); var rt = BindExpression(b.Right, scope, contextualIt);
                if (b.Operator is "and" or "or") { Require(lt, compilation.GetSpecialType(SpecialType.System_Boolean), b.Left.Span, "logical operand must be bool."); Require(rt, compilation.GetSpecialType(SpecialType.System_Boolean), b.Right.Span, "logical operand must be bool."); return compilation.GetSpecialType(SpecialType.System_Boolean); }
                return b.Operator is "==" or "!=" or ">" or ">=" or "<" or "<=" ? compilation.GetSpecialType(SpecialType.System_Boolean) : lt;
            case CallExpression c: return BindCall(c, scope, contextualIt);
            default: return null;
        }
    }
    private ITypeSymbol? BindCall(CallExpression call, Dictionary<string, ITypeSymbol> scope, ITypeSymbol? contextualIt)
    {
        if (call.Name == "not-seen-recently") { if (call.Arguments.Count == 2) { var receiver = call.Arguments[0].Expression; Require(BindExpression(receiver, scope, contextualIt), dateTimeNullable, call.Arguments[0].Span, "not-seen-recently receiver must be DateTime?."); Require(BindExpression(call.Arguments[1].Expression, scope), compilation.GetSpecialType(SpecialType.System_Int32), call.Arguments[1].Span, "cooldown must be numeric."); model.DomainOperations[call] = new BoundDomainOperation(BoundDomainOperationKind.NotSeenRecently, receiver, call.Arguments[1].Expression, null); } return compilation.GetSpecialType(SpecialType.System_Boolean); }
        if (call.Name == "was-seen-at-least-once") { if (call.Arguments.Count == 1) { var receiver = call.Arguments[0].Expression; var t = BindExpression(receiver, scope); Require(t, cycleElementId, call.Arguments[0].Span, "was-seen-at-least-once requires CycleElemId."); model.DomainOperations[call] = new BoundDomainOperation(BoundDomainOperationKind.WasSeenAtLeastOnce, receiver, null, null); } return compilation.GetSpecialType(SpecialType.System_Boolean); }
        if (functions.TryGetValue(NormalizeKey(call.Name), out var function)) { var bound = BindArgumentList(call, function.Parameters.Select(p => new ParameterInfo(p.Name, TypeOf(p.Type)!, false)).ToArray(), scope, contextualIt); if (bound == null) return null; model.Calls[call] = new BoundCall(null, Name(function.Name), bound, TypeOf(function.ReturnType ?? "void")); return TypeOf(function.ReturnType ?? "void"); }
        var methods = AllMembers(call.Name).OfType<IMethodSymbol>().Concat(DslNames.Candidates(call.Name).Skip(1).SelectMany(AllMembers).OfType<IMethodSymbol>()).Where(m => NormalizeKey(m.Name) == NormalizeKey(call.Name)).GroupBy(m => m.ToDisplayString()).Select(g => g.First()).ToArray();
        if (methods.Length == 0) { Report("SEGDSL305", $"Unknown function or method '{call.Name}'.", call.Span); return null; }
        var candidates = methods.Select(m => TryBind(call, m, scope, contextualIt)).Where(x => x != null).Cast<BoundCall>().ToArray();
        if (candidates.Length != 1) { Report("SEGDSL306", candidates.Length == 0 ? $"No overload of '{call.Name}' accepts these arguments." : $"Call to '{call.Name}' is ambiguous.", call.Span); return null; }
        model.Calls[call] = candidates[0]; return candidates[0].ReturnType;
    }
    private sealed record ParameterInfo(string Name, ITypeSymbol Type, bool Optional, IParameterSymbol? Symbol = null);
    private BoundCall? TryBind(CallExpression call, IMethodSymbol method, Dictionary<string, ITypeSymbol> scope, ITypeSymbol? contextualIt)
    { var parameters = method.Parameters.Select(p => new ParameterInfo(p.Name, p.Type, p.IsOptional, p)).ToArray(); var bound = BindArgumentList(call, parameters, scope, contextualIt); return bound == null ? null : new BoundCall(method, method.Name, bound, method.ReturnType); }
    private List<BoundArgument>? BindArgumentList(CallExpression call, IReadOnlyList<ParameterInfo> parameters, Dictionary<string, ITypeSymbol> scope, ITypeSymbol? contextualIt = null)
    {
        var result = new List<BoundArgument>(); var used = new HashSet<string>(StringComparer.Ordinal); var namedSeen = false; var positionalIndex = 0;
        foreach (var argument in call.Arguments)
        {
            if (argument.Name != null) { namedSeen = true; var exact = parameters.Where(p => string.Equals(p.Name, argument.Name, StringComparison.Ordinal)).ToArray(); var normalized = exact.Length == 0 ? parameters.Where(p => NormalizeKey(p.Name) == NormalizeKey(argument.Name)).ToArray() : exact; if (normalized.Length > 1) { Report("SEGDSL311", $"Ambiguous named argument '{argument.Name}'.", argument.Span); return null; } var parameter = normalized.SingleOrDefault(); if (parameter == null) { Report("SEGDSL307", $"Unknown named argument '{argument.Name}'.", argument.Span); return null; } if (!used.Add(parameter.Name)) { Report("SEGDSL308", $"Duplicate named argument '{argument.Name}'.", argument.Span); return null; } if (!Compatible(BindExpression(argument.Expression, scope, contextualIt), parameter.Type)) { Report("SEGDSL309", $"Argument '{argument.Name}' has incompatible type.", argument.Expression.Span); return null; } result.Add(new BoundArgument(argument, parameter.Symbol, parameter.Name)); }
            else { if (namedSeen) { Report("SEGDSL310", "Positional arguments cannot follow a named argument.", argument.Span); return null; } while (positionalIndex < parameters.Count && used.Contains(parameters[positionalIndex].Name)) positionalIndex++; if (positionalIndex >= parameters.Count) return null; var parameter = parameters[positionalIndex++]; used.Add(parameter.Name); if (!Compatible(BindExpression(argument.Expression, scope, contextualIt), parameter.Type)) return null; result.Add(new BoundArgument(argument, parameter.Symbol, parameter.Name)); }
        }
        if (parameters.Any(p => !p.Optional && !used.Contains(p.Name))) return null;
        return result;
    }
    private bool Compatible(ITypeSymbol? actual, ITypeSymbol expected) => actual != null && Microsoft.CodeAnalysis.CSharp.CSharpExtensions.ClassifyConversion(compilation, actual, expected).IsImplicit;
    private ITypeSymbol? BindName(string name, SourceSpan span, Dictionary<string, ITypeSymbol>? scope = null)
    {
        lastSymbol = null; lastKind = BoundSymbolKind.Local; lastCSharpName = Name(name);
        if (name == "it") { lastKind = BoundSymbolKind.ContextualIt; lastCSharpName = "it"; return dateTimeNullable; }
        var key = NormalizeKey(name);
        if (scope != null && scope.TryGetValue(key, out var local)) { lastKind = currentParameters.Contains(key) ? BoundSymbolKind.Parameter : BoundSymbolKind.Local; model.References[name] = Name(name); return local; }
        if (globals.TryGetValue(key, out var global)) { lastKind = globalKinds[key]; lastCSharpName = Name(name); model.References[name] = lastCSharpName; return global; }
        var exact = ResolveCSharpMembers(name);
        var candidates = exact.Count != 0 ? exact : DslNames.Candidates(name).Skip(1).SelectMany(AllMembers).ToArray();
        if (candidates.Count == 1)
        {
            lastSymbol = candidates[0]; lastKind = candidates[0] switch { IFieldSymbol => BoundSymbolKind.CSharpField, IPropertySymbol => BoundSymbolKind.CSharpProperty, IMethodSymbol => BoundSymbolKind.CSharpMethod, _ => BoundSymbolKind.Local }; lastCSharpName = candidates[0].Name; model.References[name] = lastCSharpName;
            return MemberType(candidates[0]);
        }
        if (candidates.Count > 1) Report("SEGDSL311", $"Ambiguous name '{name}'.", span); else Report("SEGDSL312", $"Unknown identifier '{name}'.", span); return null;
    }
    private IReadOnlyList<ISymbol> ResolveCSharpMembers(string name) => AllMembers(name).ToArray();
    private static ITypeSymbol? MemberType(ISymbol symbol) => symbol switch { IFieldSymbol f => f.Type, IPropertySymbol p => p.Type, IMethodSymbol m => m.ReturnType, _ => null };
    private IEnumerable<ISymbol> AllMembers(string name) { for (INamedTypeSymbol? t = world; t != null; t = t.BaseType) foreach (var member in t.GetMembers(name)) yield return member; }
    private ITypeSymbol? TypeOf(string name) => name switch { "int" => compilation.GetSpecialType(SpecialType.System_Int32), "bool" => compilation.GetSpecialType(SpecialType.System_Boolean), "string" => compilation.GetSpecialType(SpecialType.System_String), _ => compilation.GetTypeByMetadataName(name.StartsWith("Seg.", StringComparison.Ordinal) ? name : "Seg." + name) ?? compilation.GetTypeByMetadataName(name) };
    private static string NormalizeKey(string name) => DslNames.Camel(name).ToUpperInvariant();
    private void Require(ITypeSymbol? actual, ITypeSymbol? expected, SourceSpan span, string message) { if (actual == null || expected == null || !Compatible(actual, expected)) Report("SEGDSL313", message, span); }
    private void Report(string id, string message, SourceSpan span) => report(new DslDiagnostic(id, message, span));
    private static string Name(string name) => name.Contains('-') ? DslNames.Camel(name) : name;
    private void CheckDuplicateElements(IEnumerable<DslDeclaration> declarations) { var ids = declarations.SelectMany(AllElements).GroupBy(x => NormalizeKey(x.Id), StringComparer.Ordinal); foreach (var group in ids.Where(x => x.Count() > 1)) foreach (var item in group.Skip(1)) Report("SEGDSL314", $"Duplicate CycleElementId '{item.Id}'.", item.Span); }
    private void CheckDuplicateCombines(IEnumerable<DslDeclaration> declarations) { var combines = declarations.OfType<HandlerDeclaration>().Where(x => x.Kind == "combine").GroupBy(x => NormalizeKey(x.First) + "\0" + NormalizeKey(x.Second!)); foreach (var group in combines.Where(x => x.Count() > 1)) foreach (var item in group.Skip(1)) Report("SEGDSL315", "Duplicate combine handler.", item.Span); }
    private static IEnumerable<CycleElementDeclaration> FindNestedElements(DslDeclaration declaration) => declaration switch
    { HandlerDeclaration h => FindNested(h.Body), FunctionDeclaration f => FindNested(f.Body), _ => Enumerable.Empty<CycleElementDeclaration>() };
    private static IEnumerable<CycleElementDeclaration> FindNested(IEnumerable<DslStatement> statements) => statements.SelectMany(s => s switch { AddCycleElementStatement a => new[] { new CycleElementDeclaration(a.Cycle, a.Id, a.Important, a.Repeat, a.Condition, a.Body, a.Span) }.Concat(FindNested(a.Body)), IfStatement i => i.Branches.SelectMany(x => FindNested(x.Body)).Concat(i.ElseBody == null ? Enumerable.Empty<CycleElementDeclaration>() : FindNested(i.ElseBody)), _ => Enumerable.Empty<CycleElementDeclaration>() });
    private static IEnumerable<CycleElementDeclaration> AllElements(DslDeclaration declaration) => declaration switch
    {
        CycleElementDeclaration e => new[] { e }.Concat(FindNested(e.Body)),
        HandlerDeclaration h => FindNested(h.Body),
        FunctionDeclaration f => FindNested(f.Body),
        _ => Enumerable.Empty<CycleElementDeclaration>()
    };
    private IReadOnlyList<ISymbol> ResolveCSharpCandidates(string name)
    {
        var exact = ResolveCSharpMembers(name);
        return exact.Count != 0 ? exact : DslNames.Candidates(name).Skip(1).SelectMany(AllMembers).ToArray();
    }
}
