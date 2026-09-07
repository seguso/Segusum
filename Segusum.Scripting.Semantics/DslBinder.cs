using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Segusum.Scripting.Core;

namespace Segusum.Scripting.Semantics;

public enum BoundSymbolKind { Local, Parameter, State, Function, Cycle, CycleElementId, NamedCutsceneId, CSharpField, CSharpProperty, CSharpMethod, ContextualIt }
public sealed record BoundValue(ITypeSymbol? Type, string CSharpName, ISymbol? Symbol, BoundSymbolKind Kind);
public sealed record BoundArgument(DslArgument Source, IParameterSymbol? Parameter, string ParameterName);
public sealed record BoundCall(IMethodSymbol? Method, string TargetName, IReadOnlyList<BoundArgument> Arguments, ITypeSymbol? ReturnType, DslExpression? Receiver = null);
public enum CallFailureKind { None, UnknownNamedArgument, DuplicateNamedArgument, PositionalAfterNamed, IncompatibleArgument, MissingRequiredArgument, TooManyArguments }
public sealed record CandidateResult(BoundCall? Call, CallFailureKind FailureKind, SourceSpan FailureSpan, string FailureDetail, int Score);
public enum BoundDomainOperationKind { NotSeenRecently, WasSeenAtLeastOnce }
public sealed record BoundDomainOperation(BoundDomainOperationKind Kind, DslExpression Receiver, DslExpression? Argument, IMethodSymbol? Method);
public sealed record DslSymbolIdentity(string Name, string Kind, SourceSpan DeclarationSpan);
public sealed record DslSemanticReference(string Path, SourceSpan Span, BoundSymbolKind Kind, ISymbol? CSharpSymbol, DslSymbolIdentity? DslSymbol, string ReferenceKind);
public sealed class DslBinderProfile
{
    private readonly Dictionary<string, (long Calls, long Ticks)> counters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, (long Calls, long Ticks)> phases = new(StringComparer.Ordinal);
    public void Add(string name, long ticks) => Add(counters, name, ticks);
    public void AddPhase(string name, long ticks) => Add(phases, name, ticks);
    public void Count(string name) => Add(name, 0);
    public void Count(string name, long amount) => Add(counters, name, 0, amount);
    public T Measure<T>(string name, Func<T> action)
    {
        var started = Stopwatch.GetTimestamp();
        try { return action(); }
        finally { Add(name, Stopwatch.GetTimestamp() - started); }
    }
    public void MeasureAction(string name, Action action)
    {
        var started = Stopwatch.GetTimestamp();
        try { action(); }
        finally { Add(name, Stopwatch.GetTimestamp() - started); }
    }
    public (long Calls, double Milliseconds) Get(string name)
    {
        if (!counters.TryGetValue(name, out var value)) return (0, 0);
        return (value.Calls, value.Ticks * 1000.0 / Stopwatch.Frequency);
    }
    public (long Calls, double Milliseconds) GetPhase(string name)
    {
        if (!phases.TryGetValue(name, out var value)) return (0, 0);
        return (value.Calls, value.Ticks * 1000.0 / Stopwatch.Frequency);
    }
    public IEnumerable<T> MeasureEnumerable<T>(string name, IEnumerable<T> source)
    {
        var started = Stopwatch.GetTimestamp();
        counters.TryGetValue(name, out var value);
        value.Calls++;
        counters[name] = value;
        try { foreach (var item in source) yield return item; }
        finally { AddTicks(name, Stopwatch.GetTimestamp() - started); }
    }
    public string Format()
    {
        static string FormatTable(Dictionary<string, (long Calls, long Ticks)> values)
            => string.Join("; ", values.OrderByDescending(x => x.Value.Ticks).Select(x => $"{x.Key}:calls={x.Value.Calls},ms={x.Value.Ticks * 1000.0 / Stopwatch.Frequency:0.0}"));
        return $"phases=[{FormatTable(phases)}] hotspots=[{FormatTable(counters)}]";
    }
    private void Add(Dictionary<string, (long Calls, long Ticks)> target, string name, long ticks)
        => Add(target, name, ticks, 1);
    private void Add(Dictionary<string, (long Calls, long Ticks)> target, string name, long ticks, long calls)
    {
        target.TryGetValue(name, out var value);
        value.Calls += calls;
        value.Ticks += ticks;
        target[name] = value;
    }
    private void AddTicks(string name, long ticks)
    {
        counters.TryGetValue(name, out var value);
        value.Ticks += ticks;
        counters[name] = value;
    }
}
public sealed class BoundModel
{
    public Dictionary<DslExpression, BoundValue> Values { get; } = new(ReferenceComparer<DslExpression>.Instance);
    public Dictionary<DslExpression, ITypeSymbol?> ExpressionTypes { get; } = new(ReferenceComparer<DslExpression>.Instance);
    public Dictionary<DslExpression, BoundCall> Calls { get; } = new(ReferenceComparer<DslExpression>.Instance);
    public Dictionary<DslExpression, BoundDomainOperation> DomainOperations { get; } = new(ReferenceComparer<DslExpression>.Instance);
    public Dictionary<string, string> References { get; } = new(StringComparer.Ordinal);
    public Dictionary<DslExpression, DslSemanticReference> SemanticReferences { get; } = new(ReferenceComparer<DslExpression>.Instance);
    public List<DslSemanticReference> SemanticReferenceList { get; } = new();
    public Dictionary<string, DslSymbolIdentity> DslSymbolsByName { get; } = new(StringComparer.Ordinal);
    public Dictionary<DslSymbolIdentity, SourceSpan> DslDefinitions { get; } = new();
    // SemanticReferenceList is the authoritative tooling view.  References is
    // retained for the emitter's legacy name substitution map.
    public IReadOnlyList<DslSemanticReference> ReferencesByNode => SemanticReferenceList;
}
internal sealed class ReferenceComparer<T> : IEqualityComparer<T> where T : class
{
    public static readonly ReferenceComparer<T> Instance = new();
    public bool Equals(T? x, T? y) => ReferenceEquals(x, y);
    public int GetHashCode(T obj) => RuntimeHelpers.GetHashCode(obj);
}

public sealed class DslBinder
{
    private readonly Compilation compilation;
    private readonly INamedTypeSymbol world;
    private readonly Action<DslDiagnostic> report;
    private readonly Dictionary<string, ITypeSymbol> globals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ITypeSymbol> cycleElementGlobals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SourceSpan> cycleElementDeclarationSpans = new(StringComparer.Ordinal);
    private readonly HashSet<string> cycleElementDuplicateReports = new(StringComparer.Ordinal);
    private readonly Dictionary<string, ITypeSymbol> namedCutsceneGlobals = new(StringComparer.Ordinal);
    private readonly Dictionary<string, string> namedCutsceneMetadata = new(StringComparer.Ordinal);
    private readonly Dictionary<string, BoundSymbolKind> globalKinds = new(StringComparer.Ordinal);
    private readonly Dictionary<string, FunctionDeclaration> functions = new(StringComparer.Ordinal);
    private readonly BoundModel model = new();
    private readonly INamedTypeSymbol? cycle;
    private readonly INamedTypeSymbol? cycleElementId;
    private readonly INamedTypeSymbol? namedCutsceneId;
    private readonly ITypeSymbol? dateTime;
    private readonly ITypeSymbol? dateTimeNullable;
    private readonly INamedTypeSymbol? textHandlerInput;
    private readonly INamedTypeSymbol? logicObj;
    private readonly INamedTypeSymbol? objective;
    private readonly INamedTypeSymbol? room;
    private readonly INamedTypeSymbol? explanation;
    private readonly ITypeSymbol? beforeRoomChangeInput;
    private readonly ITypeSymbol? roomChangedInput;
    private readonly ITypeSymbol? actionContext;
    private readonly ITypeSymbol? cutScene;
    private readonly ITypeSymbol? walkPath;
    private ISymbol? lastSymbol;
    private BoundSymbolKind lastKind;
    private string lastCSharpName = "";
    private readonly HashSet<string> currentParameters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DslSymbolIdentity> activeDslSymbols = new(StringComparer.Ordinal);
    private readonly HashSet<ISymbol> dslRoomChangedTargets = new(SymbolEqualityComparer.Default);
    private readonly HashSet<DslExpression> nullLiterals = new(ReferenceComparer<DslExpression>.Instance);
    private readonly Dictionary<string, INamedTypeSymbol?> typesBySimpleName = new(StringComparer.Ordinal);
    private readonly Dictionary<string, List<INamedTypeSymbol>> typeCandidatesBySimpleName = new(StringComparer.Ordinal);
    private bool typeCandidatesBuilt;
    private readonly Dictionary<string, IReadOnlyList<ISymbol>> worldMembersByName = new(StringComparer.Ordinal);
    private readonly object worldMembersGate = new();
    private readonly Dictionary<string, IReadOnlyList<ISymbol>> resolvedCSharpMembersByName = new(StringComparer.Ordinal);
    private readonly Dictionary<ITypeSymbol, Dictionary<string, IReadOnlyList<ISymbol>>> membersByReceiverType = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<ISymbol, Dictionary<ITypeSymbol, bool>> accessibilityByReceiver = new(SymbolEqualityComparer.Default);
    private readonly Dictionary<ISymbol, bool> accessibilityBySymbol = new(SymbolEqualityComparer.Default);
    private readonly object accessibilityGate = new();
    private readonly CSharpSemanticIndexCache? semanticIndexes;
    private bool typeIndexBuilt;
    private bool suppressDiagnostics;
    private readonly DslBinderProfile profile = new();

    public BoundModel Model => model;
    public DslBinderProfile Profile => profile;
    public DslBinder(Compilation compilation, INamedTypeSymbol world, Action<DslDiagnostic> report, CSharpSemanticIndexCache? semanticIndexes = null)
    {
        this.semanticIndexes = semanticIndexes;
        this.compilation = compilation; this.world = world; this.report = report;
        cycle = GetTypeByMetadataName("Seg.Cycle"); cycleElementId = GetTypeByMetadataName("Seg.CycleElemId"); namedCutsceneId = GetTypeByMetadataName("Seg.NamedCutSceneId");
        logicObj = GetTypeByMetadataName("Seg.LogicObj"); objective = GetTypeByMetadataName("Seg.Objective"); room = GetTypeByMetadataName("Seg.Room"); explanation = GetTypeByMetadataName("Seg.Explanation"); beforeRoomChangeInput = GetTypeByMetadataName("Seg.BeforeRoomChangeInput"); roomChangedInput = GetTypeByMetadataName("Seg.RoomChangedInput"); walkPath = GetTypeByMetadataName("Seg.WalkPath"); actionContext = GetTypeByMetadataName("Seg.ActionContext"); cutScene = GetTypeByMetadataName("Seg.CutScene");
        dateTime = compilation.GetSpecialType(SpecialType.System_DateTime); dateTimeNullable = compilation.GetSpecialType(SpecialType.System_Nullable_T).Construct(dateTime); textHandlerInput = GetTypeByMetadataName("Seg.TextHandlerInput");
    }
    public void Bind(IReadOnlyList<DslDeclaration> declarations)
    {
        var phase = Stopwatch.GetTimestamp();
        // Register every cycle-element symbol before binding any predicate.
        // Predicates may refer to a sibling declared later in the document;
        // binding declarations one by one would otherwise give that forward
        // reference an unknown type and produce cascading bool/argument
        // diagnostics.
        foreach (var declaration in declarations)
        {
            if (declaration is CycleElementDeclaration cycleElement)
            {
                EnsureCycleElementGlobal(cycleElement.Id, cycleElement.IdSpan);
                PredeclareCycleElements(cycleElement.Body);
            }
            else
            {
                PredeclareCycleElements(DeclarationBody(declaration));
            }
        }
        foreach (var declaration in declarations)
        {
            switch (declaration)
            {
                case StateDeclaration state: AddDslIdentity(state.Name, "state", state.Span); break;
                case FunctionDeclaration function: AddDslIdentity(function.Name, "function", function.Span); break;
                case CycleElementDeclaration element: AddDslIdentity(element.Id, "cycle-element", element.Span); break;
            }
        }
        profile.AddPhase("first declaration identity pass", Stopwatch.GetTimestamp() - phase);
        phase = Stopwatch.GetTimestamp();
        foreach (var id in profile.MeasureEnumerable("FindNamedCutscenes", declarations.SelectMany(FindNamedCutscenes))) AddDslIdentity(id.Id, "named-cutscene", id.Span);
        profile.AddPhase("FindNamedCutscenes", Stopwatch.GetTimestamp() - phase);
        phase = Stopwatch.GetTimestamp();
        foreach (var state in declarations.OfType<StateDeclaration>()) AddGlobal(state.Name, TypeOf(state.Type), state.Span, BoundSymbolKind.State);
        foreach (var cycleDeclaration in declarations.OfType<CycleDeclaration>()) AddGlobal(cycleDeclaration.Variable, cycle, cycleDeclaration.Span, BoundSymbolKind.Cycle);
        foreach (var element in declarations.OfType<CycleElementDeclaration>()) AddGlobal(element.Id, cycleElementId, element.Span, BoundSymbolKind.CycleElementId);
        foreach (var element in declarations.SelectMany(FindNestedElements)) AddGlobal(element.Id, cycleElementId, element.Span, BoundSymbolKind.CycleElementId);
        foreach (var id in profile.MeasureEnumerable("FindNamedCutscenes", declarations.SelectMany(FindNamedCutscenes))) AddNamedCutsceneGlobal(id);
        profile.AddPhase("AddGlobal states/cycles/elements", Stopwatch.GetTimestamp() - phase);
        phase = Stopwatch.GetTimestamp();
        foreach (var function in declarations.OfType<FunctionDeclaration>()) { var key = NormalizeKey(function.Name); if (functions.ContainsKey(key)) Report("SEGDSL303", "Duplicate DSL function.", function.Span); else functions[key] = function; }
        profile.AddPhase("functions registration", Stopwatch.GetTimestamp() - phase);
        phase = Stopwatch.GetTimestamp();
        foreach (var declaration in declarations)
        {
            switch (declaration)
            {
                case StateDeclaration s: BindExpression(s.Initializer, new()); break;
                case FunctionDeclaration f: profile.MeasureAction("BindFunction", () => BindFunction(f)); break;
                case HandlerDeclaration h: profile.MeasureAction("BindHandler", () => BindHandler(h)); break;
                case CycleElementDeclaration c: BindCycle(c.Cycle, c.Id, c.Repeat, c.Condition, c.Body, c.Span, new()); break;
                case NextCycleDeclaration n: Require(BindExpression(n.Cycle, new()), cycle, n.Cycle.Span, "next requires a Cycle."); break;
                case BeforeRoomChangeDeclaration b: profile.MeasureAction("BindBeforeRoomChange", () => BindBeforeRoomChange(b)); break;
                case AfterActionExecutedDeclaration a: profile.MeasureAction("BindAfterActionExecuted", () => BindAfterActionExecuted(a)); break;
            }
        }
        profile.AddPhase("Bind declarations total", Stopwatch.GetTimestamp() - phase);
        profile.MeasureAction("CheckDuplicateCombines", () => CheckDuplicateCombines(declarations));
        profile.MeasureAction("CheckDuplicateRoomChanged", () => CheckDuplicateRoomChanged(declarations));
        profile.MeasureAction("CheckDuplicateUnaryHandlers", () => CheckDuplicateUnaryHandlers(declarations));
        profile.MeasureAction("CheckCSharpRoomChangedDuplicates", () => CheckCSharpRoomChangedDuplicates(declarations));
        profile.MeasureAction("CheckDuplicateBeforeRoomChange", () => CheckDuplicateBeforeRoomChange(declarations));
        profile.MeasureAction("CheckDuplicateAfterActionExecuted", () => CheckDuplicateAfterActionExecuted(declarations));
    }

    private static IReadOnlyList<DslStatement> DeclarationBody(DslDeclaration declaration) => declaration switch
    {
        FunctionDeclaration f => f.Body,
        HandlerDeclaration h => h.Body,
        BeforeRoomChangeDeclaration b => b.Body,
        AfterActionExecutedDeclaration a => a.Body,
        _ => Array.Empty<DslStatement>()
    };

    private void PredeclareCycleElements(IEnumerable<DslStatement> statements)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case AddCycleElementStatement add:
                    EnsureCycleElementGlobal(add.Id, add.IdSpan);
                    PredeclareCycleElements(add.Body);
                    break;
                case IfStatement conditional:
                    foreach (var branch in conditional.Branches) PredeclareCycleElements(branch.Body);
                    if (conditional.ElseBody != null) PredeclareCycleElements(conditional.ElseBody);
                    break;
                case NamedCutsceneStatement cutscene:
                    PredeclareCycleElements(cutscene.Body);
                    break;
            }
        }
    }
    private void BindBeforeRoomChange(BeforeRoomChangeDeclaration declaration)
    {
        var previous = new Dictionary<string, DslSymbolIdentity>(activeDslSymbols, StringComparer.Ordinal);
        activeDslSymbols.Clear();
        var scope = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal)
        {
            [NormalizeKey("from")] = room!,
            [NormalizeKey("to")] = room!,
            [NormalizeKey("fromToSegment")] = walkPath!,
            [NormalizeKey("fullPath")] = walkPath!,
            [NormalizeKey("e")] = beforeRoomChangeInput!
        };
        AddLocalIdentity("from", "contextual", declaration.Span);
        AddLocalIdentity("to", "contextual", declaration.Span);
        AddLocalIdentity("fromToSegment", "contextual", declaration.Span);
        AddLocalIdentity("fullPath", "contextual", declaration.Span);
        AddLocalIdentity("e", "contextual", declaration.Span);
        var oldInput = inputType; var oldAllowed = inputContextAllowed;
        inputType = beforeRoomChangeInput; inputContextAllowed = false;
        BindStatements(declaration.Body, scope, null);
        inputType = oldInput; inputContextAllowed = oldAllowed;
        activeDslSymbols.Clear(); foreach (var item in previous) activeDslSymbols[item.Key] = item.Value;
    }
    private void BindAfterActionExecuted(AfterActionExecutedDeclaration declaration)
    {
        var previous = new Dictionary<string, DslSymbolIdentity>(activeDslSymbols, StringComparer.Ordinal);
        activeDslSymbols.Clear();
        var scope = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal)
        {
            [NormalizeKey("cs")] = cutScene!,
            [NormalizeKey("actionContext")] = actionContext!
        };
        AddLocalIdentity("cs", "contextual", declaration.Span);
        AddLocalIdentity("actionContext", "contextual", declaration.Span);
        var oldInput = inputType; var oldAllowed = inputContextAllowed;
        inputType = null; inputContextAllowed = false;
        BindStatements(declaration.Body, scope, null);
        inputType = oldInput; inputContextAllowed = oldAllowed;
        activeDslSymbols.Clear(); foreach (var item in previous) activeDslSymbols[item.Key] = item.Value;
    }
    private void AddGlobal(string name, ITypeSymbol? type, SourceSpan span, BoundSymbolKind kind)
    { if (kind == BoundSymbolKind.CycleElementId) { if (!Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(name) || name.Contains('-')) { Report("SEGDSL318", "CycleElementId must be a stable C# identifier and cannot contain '-'.", span); return; } if (cycleElementGlobals.ContainsKey(name)) { model.References[name] = name; return; } var existing = ResolveCSharpCandidates(name).FirstOrDefault(); if (existing != null) { var existingType = existing switch { IFieldSymbol field => field.Type, IPropertySymbol property => property.Type, _ => null }; if (existingType != null) cycleElementGlobals[name] = existingType; } else if (type != null) cycleElementGlobals[name] = type; AddDslIdentity(name, "cycle-element", span); model.References[name] = name; return; } var key = NormalizeKey(name); if (globals.ContainsKey(key)) { if (globalKinds.TryGetValue(key, out var existingKind) && existingKind == kind) { model.References[name] = Name(name); return; } Report(kind == BoundSymbolKind.NamedCutsceneId ? "SEGDSL324" : "SEGDSL304", $"Duplicate or normalized-colliding global '{name}'.", span); } else if (type != null) { globals[key] = type; globalKinds[key] = kind; model.References[name] = Name(name); } }
    private void BindFunction(FunctionDeclaration f)
    {
        var scope = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal);
        currentParameters.Clear();
        activeDslSymbols.Clear();
        foreach (var p in f.Parameters)
        {
            var parameterType = TypeOf(p.Type);
            if (parameterType == null)
                Report("SEGDSL313", $"Unknown SEG type '{p.Type}' for parameter '{p.Name}'.", f.Span);
            scope[NormalizeKey(p.Name)] = parameterType ?? compilation.GetSpecialType(SpecialType.System_Object);
            currentParameters.Add(NormalizeKey(p.Name));
            AddLocalIdentity(p.Name, "parameter", f.Span);
        }
        ITypeSymbol? returnType = null;
        if (f.ReturnType != null)
        {
            returnType = TypeOf(f.ReturnType);
            if (returnType == null)
                Report("SEGDSL313", $"Unknown SEG return type '{f.ReturnType}'.", f.Span);
        }
        BindStatements(f.Body, scope, returnType);
        currentParameters.Clear();
        activeDslSymbols.Clear();
    }
    private void BindHandler(HandlerDeclaration h)
    {
        var first = BindName(h.First, h.FirstSpan); var second = h.Second == null ? null : BindName(h.Second, h.SecondSpan ?? h.Span); var target = h.Target == null ? null : BindName(h.Target, h.TargetSpan ?? h.Span);
        if (h.Kind == "combine") { Require(first, logicObj, h.Span, "combine first operand must be LogicObj."); Require(second, logicObj, h.Span, "combine second operand must be LogicObj."); }
        if (h.Kind == "use-for") { Require(first, logicObj, h.Span, "use-for object must be LogicObj."); Require(target, objective, h.Span, "use-for target must be Objective."); }
        if (h.Kind == "use-here") Require(first, logicObj, h.Span, "use-here object must be LogicObj.");
        if (h.Kind == "pickup") Require(first, logicObj, h.Span, "pickup target must be LogicObj.");
        if (h.Kind == "talk-here") Require(first, room, h.Span, "talk-here target must be Room.");
        if (h.Kind is "cancel-text-input" or "submit-text-input") Require(first, GetTypeByMetadataName("Seg.TextInput"), h.Span, $"{h.Kind} target must be TextInput.");
        if (h.Kind == "room-changed")
        {
            Require(first, room, h.Span, "room-changed target must be Room.");
            if (lastSymbol != null) dslRoomChangedTargets.Add(lastSymbol);
        }
        if (h.Explanation != null) Require(BindExpression(h.Explanation, new()), explanation, h.Explanation.Span, "exp must be Explanation.");
        if (h.Condition != null) Require(BindExpression(h.Condition, new()), compilation.GetSpecialType(SpecialType.System_Boolean), h.Condition.Span, "possible-when must be bool.");
        var previousInputType = inputType;
        inputType = h.Kind == "submit-text-input" ? textHandlerInput : GetTypeByMetadataName("Seg.HandlerInput");
        inputContextAllowed = h.Kind == "submit-text-input";
        var handlerScope = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal);
        // Every action handler receives its runtime input as the implicit
        // `e` parameter.  Room-changed historically also exposes `i`; keep
        // that alias only for that handler kind.
        if (inputType != null)
        {
            handlerScope[NormalizeKey("e")] = inputType;
            AddLocalIdentity("e", "contextual", h.Span);
        }
        if (h.Kind == "room-changed" && roomChangedInput != null)
        {
            handlerScope[NormalizeKey("i")] = roomChangedInput;
            handlerScope[NormalizeKey("e")] = roomChangedInput;
            AddLocalIdentity("i", "contextual", h.Span);
            AddLocalIdentity("e", "contextual", h.Span);
        }
        BindStatements(h.Body, handlerScope, null);
        inputType = previousInputType;
        inputContextAllowed = false;
    }
    private void BindCycle(string cycleName, string? elementId, string? repeat, DslExpression? condition, IReadOnlyList<DslStatement> body, SourceSpan span, Dictionary<string, ITypeSymbol>? scope = null)
    { scope ??= new(); Require(BindName(cycleName, span, scope), cycle, span, "add requires a Cycle."); if (repeat != null && repeat is not ("once" or "forever")) Report("SEGDSL316", $"Unknown Repeat modifier '{repeat}'.", span); if (condition != null) Require(BindExpression(condition, scope, dateTimeNullable), compilation.GetSpecialType(SpecialType.System_Boolean), condition.Span, "when must be bool."); BindStatements(body, new(scope), null); }
    private void BindStatements(IEnumerable<DslStatement> statements, Dictionary<string, ITypeSymbol> scope, ITypeSymbol? returnType)
    {
        // Cycle predicates may refer to a later sibling (for example the
        // predicate of cielo1 mentions cielo2). Intern all sibling IDs before
        // binding any predicate so forward references retain CycleElemId type.
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case VariableDeclaration v:
                    var type = v.Type == null ? BindExpression(v.Initializer, scope) : TypeOf(v.Type);
                    if (type != null) { scope[NormalizeKey(v.Name)] = type; AddLocalIdentity(v.Name, "local", v.Span); }
                    if (v.Type != null && v.Initializer is LiteralExpression { Kind: "null" }) nullLiterals.Add(v.Initializer);
                    break;
                case AssignmentStatement a:
                    if (a.Receiver != null)
                    {
                        var receiver = BindExpression(a.Receiver, scope);
                        var target = receiver == null ? null : MembersOf(receiver, a.MemberName ?? a.Name).FirstOrDefault(x => Accessible(x, receiver));
                        if (target is not IPropertySymbol { SetMethod: not null } && target is not IFieldSymbol { IsReadOnly: false }) Report("SEGDSL321", $"Member '{a.MemberName ?? a.Name}' is not writable.", a.Span);
                        if (target != null)
                            RecordReference(a.MemberName ?? a.Name, a.MemberSpan ?? a.Span, target is IMethodSymbol ? BoundSymbolKind.CSharpMethod : BoundSymbolKind.CSharpProperty, target, null, "member-name");
                        RequireExpression(a.Value, BindExpression(a.Value, scope), target is IPropertySymbol p ? p.Type : target is IFieldSymbol f ? f.Type : null, "assignment type mismatch.");
                    }
                    else { var targetType = BindName(a.Name, a.NameSpan, scope); RequireExpression(a.Value, BindExpression(a.Value, scope), targetType, "assignment type mismatch."); }
                    break;
                case IncrementStatement i: Require(BindName(i.Name, i.NameSpan, scope), compilation.GetSpecialType(SpecialType.System_Int32), i.Span, "++ requires int."); break;
                case ReturnStatement r:
                    if (r.Expression == null)
                    {
                        if (returnType != null) Report("SEGDSL313", "A bare ret is only valid in a void function.", r.Span);
                    }
                    else RequireExpression(r.Expression, BindExpression(r.Expression, scope), returnType, "return type mismatch.");
                    break;
                case CallStatement c: BindExpression(c.Expression, scope); break;
                case NextCycleStatement n: Require(BindExpression(n.Cycle, scope), cycle, n.Span, "next requires a Cycle."); break;
                case AddCycleElementStatement a: BindCycle(a.Cycle, a.Id, a.Repeat, a.Condition, a.Body, a.Span, scope); break;
                case IfStatement i:
                    foreach (var branch in i.Branches) { Require(BindExpression(branch.Condition, scope), compilation.GetSpecialType(SpecialType.System_Boolean), branch.Condition.Span, "if condition must be bool."); BindStatements(branch.Body, new(scope), returnType); }
                    if (i.ElseBody != null) BindStatements(i.ElseBody, new(scope), returnType); break;
                case DialogueStatement d:
                    Require(BindName(d.Character, d.CharacterSpan, scope), GetTypeByMetadataName("Seg.Character"), d.Span, "dialogue speaker must be Character.");
                    Require(BindExpression(d.Text, scope), compilation.GetSpecialType(SpecialType.System_String), d.Text.Span, "dialogue text must be string.");
                    if (d.Insta != null)
                    {
                        Require(BindExpression(d.Insta, scope), compilation.GetSpecialType(SpecialType.System_String), d.Insta.Span, "insta argument must be string.");
                        if (d.Text is LiteralExpression { Kind: "string" } literal)
                        {
                            var max = MaxPlaceholder(literal.Value);
                            if (max > 0 && max != 1) Report("SEGDSL332", $"Dialogue placeholder '{{{max}}}' requires at least {max} insta arguments; the runtime API currently accepts one.", d.Text.Span);
                        }
                    }
                    break;
                case NarStatement n: Require(BindExpression(n.Text, scope), compilation.GetSpecialType(SpecialType.System_String), n.Text.Span, "nar text must be string."); break;
                case NarRoomStatement n: Require(BindExpression(n.Text, scope), compilation.GetSpecialType(SpecialType.System_String), n.Text.Span, "nar-room text must be string."); break;
                case NarImgStatement n:
                    Require(BindExpression(n.ImagePath, scope), compilation.GetSpecialType(SpecialType.System_String), n.ImagePath.Span, "nar-img path must be string.");
                    Require(BindExpression(n.Text, scope), compilation.GetSpecialType(SpecialType.System_String), n.Text.Span, "nar-img text must be string.");
                    if (n.Size is not (null or "medium" or "fullscreen")) Report("SEGDSL322", "nar-img size must be 'medium' or 'fullscreen'.", n.Span);
                    break;
                case TextInputStatement t:
                    if (inputType == null) Report("SEGDSL323", "text-input is only valid inside an action handler.", t.Span);
                    else { var expected = MembersOf(inputType, "textInputToShow").OfType<IFieldSymbol>().FirstOrDefault()?.Type ?? MembersOf(inputType, "textInputToShow").OfType<IPropertySymbol>().FirstOrDefault()?.Type; RequireExpression(t.TextInput, BindExpression(t.TextInput, scope), expected, "text-input type mismatch."); }
                    break;
                case PreventRoomChangeStatement p:
                    if (beforeRoomChangeInput == null || inputType != beforeRoomChangeInput) Report("SEGDSL333", "prevent-room-change is only valid inside before-room-change.", p.Span);
                    break;
                case NamedCutsceneStatement n: BindNamedCutscene(n, scope, returnType); break;
                case MarkHappenedOnceStatement mark:
                    if (mark.Target is not IdentifierExpression)
                    {
                        Report("SEGDSL327", "mark-happened-once requires an assignable flag field.", mark.Target.Span);
                        break;
                    }
                    Require(BindExpression(mark.Target, scope), dateTime, mark.Target.Span, "mark-happened-once target must be DateTime.");
                    break;
                case MarkHappenedStatement mark:
                    if (mark.Target is not IdentifierExpression)
                    {
                        Report("SEGDSL328", "mark-happened requires an assignable timestamp field.", mark.Target.Span);
                        break;
                    }
                    Require(BindExpression(mark.Target, scope), dateTime, mark.Target.Span, "mark-happened target must be DateTime.");
                    break;
            }
        }
    }
    private ITypeSymbol? inputType;
    private bool inputContextAllowed;
    private void BindNamedCutscene(NamedCutsceneStatement statement, Dictionary<string, ITypeSymbol>? scope = null, ITypeSymbol? returnType = null)
    {
        var idType = BindName(statement.Id, statement.IdSpan, scope);
        Require(idType, namedCutsceneId, statement.IdSpan, "named-cutscene id must be a declared NamedCutSceneId.");
        if (statement.Title is not LiteralExpression { Kind: "string" })
            Report("SEGDSL326", "named-cutscene title must be a quoted string literal.", statement.Title.Span);
        else
            Require(BindExpression(statement.Title, scope ?? new()), compilation.GetSpecialType(SpecialType.System_String), statement.Title.Span, "named-cutscene title must be a string literal.");
        foreach (var argument in statement.Arguments) BindExpression(argument, scope ?? new());
        BindStatements(statement.Body, scope == null ? new() : new(scope), returnType);
    }
    private void AddNamedCutsceneGlobal(NamedCutsceneStatement statement)
    {
        var key = NormalizeKey(statement.Id);
        RegisterNamedCutsceneMetadata(key, statement);
        var existing = ResolveCSharpMembers(statement.Id).FirstOrDefault();
        if (existing != null)
        {
            var existingType = existing switch { IFieldSymbol field => field.Type, IPropertySymbol property => property.Type, _ => null };
            if (existingType != null && namedCutsceneId != null) namedCutsceneGlobals[key] = existingType;
            model.References[statement.Id] = Name(statement.Id);
            return;
        }
        if (namedCutsceneGlobals.ContainsKey(key)) { model.References[statement.Id] = Name(statement.Id); return; }
        if (globals.ContainsKey(key)) { Report("SEGDSL324", $"Duplicate named-cutscene id '{statement.Id}'.", statement.IdSpan); return; }
        if (namedCutsceneId != null) namedCutsceneGlobals[key] = namedCutsceneId;
        model.References[statement.Id] = Name(statement.Id);
    }
    private void EnsureCycleElementGlobal(string id, SourceSpan span)
    {
        if (cycleElementGlobals.ContainsKey(id))
        {
            var location = $"{span.Path}\0{span.Start}";
            if (cycleElementDeclarationSpans.TryGetValue(id, out var first) && (first.Path != span.Path || first.Start != span.Start) && cycleElementDuplicateReports.Add(location))
                Report("SEGDSL336", $"Duplicate CycleElementId declaration '{id}'.", span);
            return;
        }
        var existing = ResolveCSharpMembers(id).FirstOrDefault();
        var existingType = existing switch { IFieldSymbol field => field.Type, IPropertySymbol property => property.Type, _ => null };
        cycleElementGlobals[id] = existingType ?? cycleElementId!;
        cycleElementDeclarationSpans[id] = span;
        AddDslIdentity(id, "cycle-element", span);
        model.References[id] = id;
    }
    private void RegisterNamedCutsceneMetadata(string key, NamedCutsceneStatement statement)
    {
        if (statement.Title is not LiteralExpression title) return;
        if (namedCutsceneMetadata.TryGetValue(key, out var previousTitle))
        {
            if (!string.Equals(previousTitle, title.Value, StringComparison.Ordinal))
                Report("SEGDSL324", $"NamedCutSceneId '{statement.Id}' is reused with incompatible titles.", statement.IdSpan);
            return;
        }
        namedCutsceneMetadata[key] = title.Value;
    }
    private ITypeSymbol? BindExpression(DslExpression expression, Dictionary<string, ITypeSymbol> scope, ITypeSymbol? contextualIt = null)
    {
        var type = profile.Measure("BindExpression", () => BindExpressionCore(expression, scope, contextualIt));
        model.ExpressionTypes[expression] = type;
        return type;
    }
    private ITypeSymbol? BindExpressionCore(DslExpression expression, Dictionary<string, ITypeSymbol> scope, ITypeSymbol? contextualIt = null)
    {
        switch (expression)
        {
            case LiteralExpression l: if (l.Kind == "null") { nullLiterals.Add(l); return null; } return l.Kind is "string" or "raw-string" ? compilation.GetSpecialType(SpecialType.System_String) : l.Kind == "bool" ? compilation.GetSpecialType(SpecialType.System_Boolean) : l.Kind == "cycle" ? cycle : compilation.GetSpecialType(SpecialType.System_Int32);
            case ListExpression list:
            {
                if (list.Elements.Count == 0) { Report("SEGDSL313", "List literals must contain at least one element.", list.Span); return null; }
                var elementTypes = list.Elements
                    .Select(element => BindExpression(element, scope, contextualIt))
                    .ToArray();
                var listElementType = FindCommonAssignableType(elementTypes);
                if (listElementType == null)
                {
                    foreach (var element in list.Elements)
                        Report("SEGDSL309", "List elements must have a compatible type.", element.Span);
                    return null;
                }
                return compilation.CreateArrayTypeSymbol(listElementType);
            }
            case IdentifierExpression i:
                if (i.Name == "it" && contextualIt != null) { model.Values[i] = new BoundValue(contextualIt, "x", null, BoundSymbolKind.ContextualIt); return contextualIt; }
                if (i.Name == "input")
                {
                    if (!inputContextAllowed) { Report("SEGDSL330", "'input' is only valid inside submit-text-input.", i.Span); return null; }
                    model.Values[i] = new BoundValue(textHandlerInput, "e", null, BoundSymbolKind.Local); return textHandlerInput;
                }
        if (functions.TryGetValue(NormalizeKey(i.Name), out var function) && function.Parameters.Count == 0) { var functionType = TypeOf(function.ReturnType ?? "void"); model.Values[i] = new BoundValue(functionType, Name(function.Name), null, BoundSymbolKind.Function); lastSymbol = null; lastCSharpName = Name(function.Name); lastKind = BoundSymbolKind.Function; RecordName(i.Name, i.Span); return functionType; }
                var resolved = BindName(i.Name, i.Span, scope);
                if (resolved != null) model.Values[i] = new BoundValue(resolved, lastCSharpName, lastSymbol, lastKind);
                return resolved;
            case ThisExpression current:
                model.Values[current] = new BoundValue(world, "this", world, BoundSymbolKind.Local);
                return world;
            case ParenthesizedExpression p: return BindExpression(p.Expression, scope, contextualIt);
            case ConditionalExpression conditional:
                var conditionType = BindExpression(conditional.Condition, scope, contextualIt);
                Require(conditionType, compilation.GetSpecialType(SpecialType.System_Boolean), conditional.Condition.Span, "conditional condition must be bool.");
                var trueType = BindExpression(conditional.WhenTrue, scope, contextualIt);
                var falseType = BindExpression(conditional.WhenFalse, scope, contextualIt);
                if (trueType != null && falseType != null && !Compatible(trueType, falseType) && !Compatible(falseType, trueType))
                    Report("SEGDSL313", "Conditional branches must have compatible types.", conditional.Span);
                return trueType ?? falseType;
            case UnaryExpression u: var ut = BindExpression(u.Operand, scope, contextualIt); if (u.Operator == "not") Require(ut, compilation.GetSpecialType(SpecialType.System_Boolean), u.Span, "not requires bool."); return compilation.GetSpecialType(SpecialType.System_Boolean);
            case BinaryExpression b:
                var lt = BindExpression(b.Left, scope, contextualIt); var rt = BindExpression(b.Right, scope, contextualIt);
                if (b.Operator is "and" or "or") { Require(lt, compilation.GetSpecialType(SpecialType.System_Boolean), b.Left.Span, "logical operand must be bool."); Require(rt, compilation.GetSpecialType(SpecialType.System_Boolean), b.Right.Span, "logical operand must be bool."); return compilation.GetSpecialType(SpecialType.System_Boolean); }
                return b.Operator is "==" or "!=" or ">" or ">=" or "<" or "<=" ? compilation.GetSpecialType(SpecialType.System_Boolean) : lt;
            case MemberAccessExpression m:
                if (m.Receiver is IdentifierExpression typeName && TryGetTypeBySimpleNameAndMember(typeName.Name, m.MemberName, out var staticType))
                {
                    var staticMember = profile.MeasureEnumerable("Roslyn.GetMembers", staticType.GetMembers(m.MemberName))
                        .FirstOrDefault(x => x switch
                        {
                            IFieldSymbol field => field.IsStatic,
                            IPropertySymbol property => property.IsStatic,
                            IMethodSymbol method => method.IsStatic,
                            _ => false
                        } && IsAccessibleStaticMember(x, staticType));
                    if (staticMember != null)
                    {
                        RecordReference(m.MemberName, m.MemberSpan, staticMember is IMethodSymbol ? BoundSymbolKind.CSharpMethod : BoundSymbolKind.CSharpProperty, staticMember, null, "member-name");
                        model.Values[m] = new BoundValue(MemberType(staticMember), typeName.Name + "." + staticMember.Name, staticMember, staticMember is IMethodSymbol ? BoundSymbolKind.CSharpMethod : BoundSymbolKind.CSharpProperty);
                        return MemberType(staticMember);
                    }
                }
                var receiverType = BindExpression(m.Receiver, scope, contextualIt);
                if (m.Receiver is IdentifierExpression { Name: "input" } && m.MemberName == "wordsLower")
                {
                    if (!inputContextAllowed) { Report("SEGDSL330", "'input.wordsLower' is only valid inside submit-text-input.", m.Span); return null; }
                    var stringType = compilation.GetSpecialType(SpecialType.System_String);
                    var list = GetTypeByMetadataName("System.Collections.Generic.List`1")?.Construct(stringType);
                    model.Values[m] = new BoundValue(list, "splittaInputEFaiLower(e)", null, BoundSymbolKind.CSharpProperty); return list;
                }
                var member = receiverType == null ? null : MembersOf(receiverType, m.MemberName).FirstOrDefault(x => Accessible(x, receiverType));
                if (member == null && receiverType != null)
                {
                    var extensions = ExtensionMethodsOf(receiverType, m.MemberName).Where(x => x.Parameters.Length == 1).ToArray();
                    if (extensions.Length == 1)
                    {
                        var extension = extensions[0];
                        RecordReference(m.MemberName, m.MemberSpan, BoundSymbolKind.CSharpMethod, extension, null, "member-name");
                        model.Values[m] = new BoundValue(extension.ReturnType, extension.Name, extension, BoundSymbolKind.CSharpMethod);
                        return extension.ReturnType;
                    }
                }
                if (member == null) { Report("SEGDSL312", $"Unknown or inaccessible member '{m.MemberName}'.", m.Span); return null; }
                RecordReference(m.MemberName, m.MemberSpan, member is IMethodSymbol ? BoundSymbolKind.CSharpMethod : BoundSymbolKind.CSharpProperty, member, null, "member-name");
                var memberType = MemberType(member); model.Values[m] = new BoundValue(memberType, m.MemberName, member, member is IMethodSymbol ? BoundSymbolKind.CSharpMethod : BoundSymbolKind.CSharpProperty); return memberType;
            case FunctionReferenceExpression r: Report("SEGDSL320", "Function references are reserved but not implemented yet.", r.Span); return null;
            case CallExpression c: return BindCall(c, scope, contextualIt);
            case ExistsExpression e:
                var collectionType = BindExpression(e.Collection, scope, contextualIt);
                var elementType = collectionType is INamedTypeSymbol named && named.IsGenericType && named.TypeArguments.Length == 1 ? named.TypeArguments[0] : null;
                if (elementType == null) { Report("SEGDSL331", "exists requires a typed collection.", e.Collection.Span); return null; }
                var existsScope = new Dictionary<string, ITypeSymbol>(scope, StringComparer.Ordinal) { [NormalizeKey(e.ItemName)] = elementType };
                var hadExistingItem = activeDslSymbols.TryGetValue(e.ItemName, out var previousItem);
                AddLocalIdentity(e.ItemName, "local", e.ItemSpan);
                Require(BindExpression(e.Predicate, existsScope), compilation.GetSpecialType(SpecialType.System_Boolean), e.Predicate.Span, "exists predicate must be bool.");
                if (hadExistingItem) activeDslSymbols[e.ItemName] = previousItem!; else activeDslSymbols.Remove(e.ItemName);
                return compilation.GetSpecialType(SpecialType.System_Boolean);
            case ListComprehensionExpression query:
                var queryCollectionType = BindExpression(query.Collection, scope, contextualIt);
                var queryElementType = queryCollectionType is INamedTypeSymbol queryNamed && queryNamed.IsGenericType && queryNamed.TypeArguments.Length == 1
                    ? queryNamed.TypeArguments[0]
                    : queryCollectionType is IArrayTypeSymbol queryArray ? queryArray.ElementType : null;
                if (queryElementType == null)
                {
                    Report("SEGDSL332", "List comprehension requires a typed collection.", query.Collection.Span);
                    return null;
                }
                var queryScope = new Dictionary<string, ITypeSymbol>(scope, StringComparer.Ordinal) { [NormalizeKey(query.ItemName)] = queryElementType };
                var hadQueryItem = activeDslSymbols.TryGetValue(query.ItemName, out var previousQueryItem);
                AddLocalIdentity(query.ItemName, "local", query.ItemSpan);
                Require(BindExpression(query.Predicate, queryScope), compilation.GetSpecialType(SpecialType.System_Boolean), query.Predicate.Span, "list comprehension predicate must be bool.");
                var selectorType = BindExpression(query.Selector, queryScope, contextualIt);
                if (hadQueryItem) activeDslSymbols[query.ItemName] = previousQueryItem!; else activeDslSymbols.Remove(query.ItemName);
                return selectorType == null ? null : compilation.CreateArrayTypeSymbol(selectorType);
            default: return null;
        }
    }
    private ITypeSymbol? BindCall(CallExpression call, Dictionary<string, ITypeSymbol> scope, ITypeSymbol? contextualIt)
        => profile.Measure("BindCall", () => BindCallCore(call, scope, contextualIt));
    private ITypeSymbol? BindCallCore(CallExpression call, Dictionary<string, ITypeSymbol> scope, ITypeSymbol? contextualIt)
    {
        if (call.Name == "not-seen-recently") { if (call.Arguments.Count == 2) { var receiver = call.Arguments[0].Expression; Require(BindExpression(receiver, scope, contextualIt), dateTimeNullable, call.Arguments[0].Span, "not-seen-recently receiver must be DateTime?."); Require(BindExpression(call.Arguments[1].Expression, scope), compilation.GetSpecialType(SpecialType.System_Int32), call.Arguments[1].Span, "cooldown must be numeric."); model.DomainOperations[call] = new BoundDomainOperation(BoundDomainOperationKind.NotSeenRecently, receiver, call.Arguments[1].Expression, null); } return compilation.GetSpecialType(SpecialType.System_Boolean); }
        if (call.Name == "was-seen-at-least-once") { if (call.Arguments.Count == 1) { var receiver = call.Arguments[0].Expression; var t = BindExpression(receiver, scope); Require(t, cycleElementId, call.Arguments[0].Span, "was-seen-at-least-once requires CycleElemId."); model.DomainOperations[call] = new BoundDomainOperation(BoundDomainOperationKind.WasSeenAtLeastOnce, receiver, null, null); } return compilation.GetSpecialType(SpecialType.System_Boolean); }
        if (functions.TryGetValue(NormalizeKey(call.Name), out var function)) { var result = BindArgumentList(call, function.Parameters.Select(p => new ParameterInfo(p.Name, TypeOf(p.Type), false)).ToArray(), scope, contextualIt); if (result.Call == null) { ReportFailure(call, new[] { result }); return null; } RecordDslReference(call.Name, call.NameSpan, BoundSymbolKind.Function, function.Name, "invocation"); model.Calls[call] = new BoundCall(null, Name(function.Name), result.Call.Arguments, TypeOf(function.ReturnType ?? "void")); return TypeOf(function.ReturnType ?? "void"); }
        var staticReceiver = call.Receiver is IdentifierExpression staticName && TryGetTypeBySimpleNameAndMember(staticName.Name, call.Name, out var staticType) ? staticType : null;
        var receiverType = staticReceiver != null ? staticReceiver : call.Receiver == null ? null : BindExpression(call.Receiver, scope, contextualIt);
        var exactMethods = staticReceiver != null
            ? staticReceiver.GetMembers(call.Name).OfType<IMethodSymbol>().Where(x => x.IsStatic).ToArray()
            : (receiverType == null ? AllMembers(call.Name) : MembersOf(receiverType, call.Name).Where(x => Accessible(x, receiverType))).OfType<IMethodSymbol>().ToArray();
        var fallbackMembers = receiverType == null ? DslNames.Candidates(call.Name).Skip(1).SelectMany(AllMembers) : MembersOf(receiverType).Where(x => Accessible(x, receiverType) && NormalizeKey(x.Name) == NormalizeKey(call.Name));
        var extensionMethods = receiverType == null ? Enumerable.Empty<IMethodSymbol>() : ExtensionMethodsOf(receiverType, call.Name);
        var methods = (exactMethods.Length != 0 ? exactMethods : fallbackMembers.OfType<IMethodSymbol>().Concat(extensionMethods)).Where(m => NormalizeKey(m.Name) == NormalizeKey(call.Name)).GroupBy(m => m.ToDisplayString()).Select(g => g.First()).ToArray();
        if (methods.Length == 0) { Report("SEGDSL305", $"Unknown function or method '{call.Name}'.", call.Span); return null; }
        var results = methods.Select(m => TryBind(call, m, scope, contextualIt)).ToArray();
        var applicable = results.Where(x => x.Call != null).OrderBy(x => x.Score).ToArray();
        if (applicable.Length == 0) { ReportFailure(call, results); return null; }
        var bestScore = applicable[0].Score; var best = applicable.Where(x => x.Score == bestScore).ToArray();
        if (best.Length != 1) { Report("SEGDSL306", $"Call to '{call.Name}' is ambiguous.", call.Span); return null; }
        // Preserve a static type receiver (Debug.Assert, CycleMemory.foo, ...)
        // so the C# generator cannot accidentally emit only the member name.
        var boundCall = best[0].Call! with { Receiver = best[0].Call!.Method?.IsExtensionMethod == true ? null : call.Receiver };
        RecordReference(call.Name, call.NameSpan, BoundSymbolKind.CSharpMethod, boundCall.Method, null, "invocation");
        model.Calls[call] = boundCall; return boundCall.ReturnType;
    }
    private sealed record ParameterInfo(string Name, ITypeSymbol? Type, bool Optional, bool IsParams = false, IParameterSymbol? Symbol = null);
    private CandidateResult TryBind(CallExpression call, IMethodSymbol method, Dictionary<string, ITypeSymbol> scope, ITypeSymbol? contextualIt)
        => profile.Measure("TryBind", () => TryBindCore(call, method, scope, contextualIt));
    private CandidateResult TryBindCore(CallExpression call, IMethodSymbol method, Dictionary<string, ITypeSymbol> scope, ITypeSymbol? contextualIt)
    {
        var parameters = method.Parameters.Select(p => new ParameterInfo(p.Name, p.Type, p.IsOptional, p.IsParams, p)).ToArray();
        var bindCall = method.IsExtensionMethod
            ? new CallExpression(call.Name, new[] { new DslArgument(null, call.Receiver!, call.Span) }.Concat(call.Arguments).ToArray(), call.Span)
            : call;
        var previous = suppressDiagnostics; suppressDiagnostics = true;
        try
        {
            var result = BindArgumentList(bindCall, parameters, scope, contextualIt);
            if (result.Call == null) return result;
            var target = method.IsExtensionMethod ? method.ContainingType.ToDisplayString() + "." + method.Name : method.Name;
            return result with { Call = new BoundCall(method, target, result.Call.Arguments, method.ReturnType, method.IsExtensionMethod ? null : call.Receiver) };
        }
        finally { suppressDiagnostics = previous; }
    }
    private CandidateResult BindArgumentList(CallExpression call, IReadOnlyList<ParameterInfo> parameters, Dictionary<string, ITypeSymbol> scope, ITypeSymbol? contextualIt = null)
    {
        var result = new List<BoundArgument>(); var used = new HashSet<string>(StringComparer.Ordinal); var namedSeen = false; var positionalIndex = 0; var score = 0;
        foreach (var argument in call.Arguments)
        {
            if (argument.Name != null) { namedSeen = true; var exact = parameters.Where(p => string.Equals(p.Name, argument.Name, StringComparison.Ordinal)).ToArray(); var normalized = exact.Length == 0 ? parameters.Where(p => NormalizeKey(p.Name) == NormalizeKey(argument.Name)).ToArray() : exact; if (normalized.Length > 1) return Failure(CallFailureKind.UnknownNamedArgument, argument.Span, $"Ambiguous named argument '{argument.Name}'."); var parameter = normalized.SingleOrDefault(); if (parameter == null) return Failure(CallFailureKind.UnknownNamedArgument, argument.Span, $"Unknown named argument '{argument.Name}'."); if (!used.Add(parameter.Name)) return Failure(CallFailureKind.DuplicateNamedArgument, argument.Span, $"Duplicate named argument '{argument.Name}'."); var actual = BindExpression(argument.Expression, scope, contextualIt); var expected = ParamsElementType(parameter) ?? parameter.Type; var conversion = Classify(actual, expected); if (!IsCompatible(argument.Expression, actual, expected)) return Failure(CallFailureKind.IncompatibleArgument, argument.Expression.Span, $"Argument '{argument.Name}' has incompatible type (expected {expected?.ToDisplayString() ?? "<unknown>"}, actual {actual?.ToDisplayString() ?? "<unknown>"})."); score += ConversionScore(conversion); result.Add(new BoundArgument(argument, parameter.Symbol, parameter.Name)); }
            else { if (namedSeen) return Failure(CallFailureKind.PositionalAfterNamed, argument.Span, "Positional arguments cannot follow a named argument."); var parameterIndex = positionalIndex; if (parameterIndex >= parameters.Count) { if (parameters.Count == 0 || !parameters[parameters.Count - 1].IsParams) return Failure(CallFailureKind.TooManyArguments, argument.Span, "Too many arguments."); parameterIndex = parameters.Count - 1; } else positionalIndex++; var parameter = parameters[parameterIndex]; if (parameter.IsParams) positionalIndex = parameters.Count; used.Add(parameter.Name); var actual = BindExpression(argument.Expression, scope, contextualIt); var expected = ParamsElementType(parameter) ?? parameter.Type; var conversion = Classify(actual, expected); if (!IsCompatible(argument.Expression, actual, expected)) return Failure(CallFailureKind.IncompatibleArgument, argument.Expression.Span, $"Argument has incompatible type (expected {expected?.ToDisplayString() ?? "<unknown>"}, actual {actual?.ToDisplayString() ?? "<unknown>"})."); score += ConversionScore(conversion); result.Add(new BoundArgument(argument, parameter.Symbol, parameter.Name)); }
        }
        var missing = parameters.FirstOrDefault(p => !p.Optional && !p.IsParams && !used.Contains(p.Name)); if (missing != null) return Failure(CallFailureKind.MissingRequiredArgument, call.Span, $"Required argument '{missing.Name}' is missing.");
        score += parameters.Count(p => p.Optional && !used.Contains(p.Name)) * 10;
        return new CandidateResult(new BoundCall(null, "", result, null), CallFailureKind.None, call.Span, "", score);
    }
    private static ITypeSymbol? ParamsElementType(ParameterInfo parameter)
        => parameter.IsParams && parameter.Type is IArrayTypeSymbol array ? array.ElementType : null;
    private CandidateResult Failure(CallFailureKind kind, SourceSpan span, string detail) => new(null, kind, span, detail, int.MaxValue);
    private Conversion Classify(ITypeSymbol? actual, ITypeSymbol? expected) => actual == null || expected == null ? default : Microsoft.CodeAnalysis.CSharp.CSharpExtensions.ClassifyConversion(compilation, actual, expected);
    private static int ConversionScore(Conversion conversion) => conversion.IsIdentity ? 0 : conversion.IsImplicit ? 1 : 1000;
    private void ReportFailure(CallExpression call, IReadOnlyList<CandidateResult> results)
    { var failure = results.Where(x => x.Call == null).OrderBy(x => FailurePriority(x.FailureKind)).FirstOrDefault(); if (failure == null) { Report("SEGDSL306", $"No overload of '{call.Name}' accepts these arguments.", call.Span); return; } Report(failure.FailureKind switch { CallFailureKind.UnknownNamedArgument => "SEGDSL307", CallFailureKind.DuplicateNamedArgument => "SEGDSL308", CallFailureKind.PositionalAfterNamed => "SEGDSL310", CallFailureKind.IncompatibleArgument => "SEGDSL309", CallFailureKind.MissingRequiredArgument => "SEGDSL306", _ => "SEGDSL306" }, failure.FailureDetail, failure.FailureSpan); }
    private static int FailurePriority(CallFailureKind kind) => kind switch { CallFailureKind.UnknownNamedArgument => 0, CallFailureKind.DuplicateNamedArgument => 1, CallFailureKind.PositionalAfterNamed => 2, CallFailureKind.IncompatibleArgument => 3, CallFailureKind.MissingRequiredArgument => 4, _ => 5 };
    private bool Compatible(ITypeSymbol? actual, ITypeSymbol expected) => profile.Measure("Compatible/type checks", () => actual != null && Microsoft.CodeAnalysis.CSharp.CSharpExtensions.ClassifyConversion(compilation, actual, expected).IsImplicit);
    private bool IsCompatible(DslExpression expression, ITypeSymbol? actual, ITypeSymbol? expected) => expected != null && ((nullLiterals.Contains(expression) && (expected.IsReferenceType || expected.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)) || Compatible(actual, expected));
    private void RequireExpression(DslExpression expression, ITypeSymbol? actual, ITypeSymbol? expected, string message) { if (!IsCompatible(expression, actual, expected)) Report("SEGDSL313", message, expression.Span); }
    private ITypeSymbol? BindName(string name, SourceSpan span, Dictionary<string, ITypeSymbol>? scope = null)
        => profile.Measure("BindName", () => BindNameCore(name, span, scope));
    private ITypeSymbol? BindNameCore(string name, SourceSpan span, Dictionary<string, ITypeSymbol>? scope = null)
    {
        lastSymbol = null; lastKind = BoundSymbolKind.Local; lastCSharpName = Name(name);
        if (name == "it") { lastKind = BoundSymbolKind.ContextualIt; lastCSharpName = "it"; RecordName(name, span); return dateTimeNullable; }
        var key = NormalizeKey(name);
        profile.Count("BindName local/global lookup");
        if (scope != null && scope.TryGetValue(key, out var local)) { profile.Count("BindName local hit"); lastKind = currentParameters.Contains(key) ? BoundSymbolKind.Parameter : BoundSymbolKind.Local; model.References[name] = Name(name); RecordName(name, span); return local; }
        profile.Count("BindName DSL global lookup");
        if (cycleElementGlobals.TryGetValue(name, out var cycleElement)) { profile.Count("BindName DSL global hit"); lastKind = BoundSymbolKind.CycleElementId; lastCSharpName = name; model.References[name] = name; RecordName(name, span); return cycleElement; }
        if (namedCutsceneGlobals.TryGetValue(key, out var namedCutscene)) { profile.Count("BindName DSL global hit"); lastKind = BoundSymbolKind.NamedCutsceneId; lastCSharpName = Name(name); model.References[name] = lastCSharpName; RecordName(name, span); return namedCutscene; }
        if (globals.TryGetValue(key, out var global)) { profile.Count("BindName DSL global hit"); lastKind = globalKinds[key]; lastCSharpName = Name(name); model.References[name] = lastCSharpName; RecordName(name, span); return global; }
        profile.Count("BindName world lookup");
        var exact = ResolveCSharpMembers(name);
        var candidates = exact.Count != 0 ? exact : DslNames.Candidates(name).Skip(1).SelectMany(AllMembers).ToArray();
        if (candidates.Count > 1 && candidates.All(x => x is IMethodSymbol))
        {
            var zeroArg = candidates.OfType<IMethodSymbol>().Where(x => x.Parameters.Length == 0).ToArray();
            if (zeroArg.Length == 1) candidates = zeroArg;
        }
        if (candidates.Count == 1)
        {
            lastSymbol = candidates[0]; lastKind = candidates[0] switch { IFieldSymbol => BoundSymbolKind.CSharpField, IPropertySymbol => BoundSymbolKind.CSharpProperty, IMethodSymbol => BoundSymbolKind.CSharpMethod, _ => BoundSymbolKind.Local }; lastCSharpName = candidates[0].Name; model.References[name] = lastCSharpName; RecordName(name, span);
            return MemberType(candidates[0]);
        }
        if (candidates.Count > 1) Report("SEGDSL311", $"Ambiguous name '{name}'.", span); else Report("SEGDSL312", $"Unknown identifier '{name}'.", span); return null;
    }
    private void AddDslIdentity(string name, string kind, SourceSpan span)
    {
        var identity = new DslSymbolIdentity(name, kind, span);
        model.DslDefinitions[identity] = span;
        if (!model.DslSymbolsByName.ContainsKey(name)) model.DslSymbolsByName.Add(name, identity);
    }
    private void AddLocalIdentity(string name, string kind, SourceSpan span)
    {
        var identity = new DslSymbolIdentity(name, kind, span);
        model.DslDefinitions[identity] = span;
        activeDslSymbols[name] = identity;
    }
    private void RecordName(string name, SourceSpan span) => RecordReference(name, span, lastKind, lastSymbol, null, "name");
    private void RecordDslReference(string name, SourceSpan span, BoundSymbolKind kind, string declaredName, string referenceKind)
    {
        var dslSymbol = activeDslSymbols.TryGetValue(name, out var local) ? local
            : model.DslSymbolsByName.TryGetValue(declaredName, out var exact) ? exact
            : model.DslSymbolsByName.Values.FirstOrDefault(x => x.Name == declaredName);
        RecordReference(name, span, kind, null, dslSymbol, referenceKind);
    }
    private void RecordReference(string name, SourceSpan span, BoundSymbolKind kind, ISymbol? symbol, DslSymbolIdentity? dslSymbol, string referenceKind)
    {
        dslSymbol ??= kind is BoundSymbolKind.CSharpField or BoundSymbolKind.CSharpProperty or BoundSymbolKind.CSharpMethod ? null
            : activeDslSymbols.TryGetValue(name, out var local) ? local
            : model.DslSymbolsByName.TryGetValue(name, out var exact) ? exact
            : model.DslSymbolsByName.Values.FirstOrDefault(x => NormalizeKey(x.Name) == NormalizeKey(name));
        model.SemanticReferenceList.Add(new DslSemanticReference(span.Path, span, kind, symbol, dslSymbol, referenceKind));
    }
    private static string NormalizeSymbolId(string name) => name;
    public ITypeSymbol? ResolveCompletionType(string name)
    {
        var key = NormalizeKey(name);
        if (globals.TryGetValue(key, out var global)) return global;
        if (cycleElementGlobals.TryGetValue(name, out var element)) return element;
        if (namedCutsceneGlobals.TryGetValue(key, out var namedCutscene)) return namedCutscene;
        if (functions.TryGetValue(key, out var function) && function.Parameters.Count == 0) return TypeOf(function.ReturnType ?? "void");
        var candidates = ResolveCSharpMembers(name);
        if (candidates.Count == 1) return MemberType(candidates[0]);
        var normalized = DslNames.Candidates(name).Skip(1).SelectMany(AllMembers).Distinct(SymbolEqualityComparer.Default).ToArray();
        return normalized.Length == 1 ? MemberType(normalized[0]) : null;
    }
    public IReadOnlyList<ISymbol> GetAccessibleMembers(ITypeSymbol receiverType)
        => MembersOf(receiverType).Where(x => x is IFieldSymbol or IPropertySymbol or IMethodSymbol)
            .Where(x => IsCompletionMember(x) && Accessible(x, receiverType)).GroupBy(x => x.Name, StringComparer.Ordinal).Select(x => x.First())
            .OrderBy(x => x.Name, StringComparer.Ordinal).ToArray();
    public IReadOnlyList<ISymbol> GetAccessibleWorldMembers()
        => AllMembers(string.Empty).Where(IsCompletionMember).ToArray();
    private static bool IsCompletionMember(ISymbol symbol)
        => !symbol.IsImplicitlyDeclared && symbol is not IMethodSymbol { MethodKind: MethodKind.Constructor or MethodKind.PropertyGet or MethodKind.PropertySet or MethodKind.EventAdd or MethodKind.EventRemove };
    private IReadOnlyList<ISymbol> ResolveCSharpMembers(string name)
    {
        if (resolvedCSharpMembersByName.TryGetValue(name, out var cached))
        {
            profile.Count("ResolveCSharpMembers cache hit");
            return cached;
        }
        profile.Count("ResolveCSharpMembers cache miss");
        var resolved = profile.Measure("ResolveCSharpMembers", () => AllMembers(name).ToArray());
        resolvedCSharpMembersByName[name] = resolved;
        return resolved;
    }
    private static ITypeSymbol? MemberType(ISymbol symbol) => symbol switch { IFieldSymbol f => f.Type, IPropertySymbol p => p.Type, IMethodSymbol m => m.ReturnType, _ => null };

    private bool IsAccessibleStaticMember(ISymbol member, INamedTypeSymbol receiverType)
    {
        if (Accessible(member)) return true;
        // Public members of a public referenced enum/type are valid static
        // accesses even when Roslyn's source-based accessibility check is
        // evaluated against a generated partial World context.
        return receiverType.DeclaredAccessibility == Accessibility.Public
            && member.DeclaredAccessibility == Accessibility.Public
            && !SegusumGeneratedSource.IsGenerated(receiverType)
            && !SegusumGeneratedSource.IsGenerated(member);
    }

    private ITypeSymbol? FindCommonAssignableType(IReadOnlyList<ITypeSymbol?> types)
    {
        if (types.Count == 0 || types.Any(x => x == null)) return null;
        var concrete = types.Cast<ITypeSymbol>().ToArray();
        var candidates = new List<ITypeSymbol>();
        foreach (var type in concrete)
        {
            candidates.Add(type);
            for (var baseType = type.BaseType; baseType != null; baseType = baseType.BaseType)
                candidates.Add(baseType);
            candidates.AddRange(type.AllInterfaces);
        }
        var compatible = candidates
            .Distinct(SymbolEqualityComparer.Default)
            .OfType<ITypeSymbol>()
            .Where(candidate => candidate.SpecialType != SpecialType.System_Object)
            .Where(candidate => concrete.All(type => Compatible(type, candidate)))
            .ToArray();
        if (compatible.Length == 0) return null;
        // Prefer the most specific common type; never use object merely as a
        // fallback when a meaningful shared base/interface exists.
        // Prefer the most specific common type.  A candidate is more
        // specific when values of that type can still be assigned to every
        // other compatible candidate (for example LogicObj is more specific
        // than its base Mentionable).  The previous predicate tested the
        // inverse relation and therefore widened homogeneous LogicObj lists
        // to Mentionable[] unnecessarily.
        return compatible.FirstOrDefault(candidate => compatible.All(other =>
            SymbolEqualityComparer.Default.Equals(candidate, other)
            || Compatible(candidate, other))) ?? compatible[0];
    }
    private IEnumerable<IMethodSymbol> ExtensionMethodsOf(ITypeSymbol receiverType, string name)
        => profile.MeasureEnumerable("ExtensionMethodsOf", ExtensionMethodsOfCore(receiverType, name));
    private IEnumerable<IMethodSymbol> ExtensionMethodsOfCore(ITypeSymbol receiverType, string name)
    {
        foreach (var metadataName in new[] { "Seg.Utils", "System.Linq.Enumerable" })
        {
            var type = GetTypeByMetadataName(metadataName);
            if (type == null)
                continue;

            foreach (var method in profile.MeasureEnumerable("Roslyn.GetMembers", type.GetMembers(name))
                         .OfType<IMethodSymbol>()
                         .Where(x => x.IsExtensionMethod && x.IsStatic))
            {
                var candidate = method;

                if (candidate.IsGenericMethod
                    && candidate.TypeParameters.Length == 1
                    && TryGetEnumerableElement(receiverType, out var element))
                {
                    candidate = candidate.Construct(element);
                }

                if (candidate.Parameters.Length != 0)
                    yield return candidate;
            }
        }
    }
    private static bool TryGetEnumerableElement(ITypeSymbol type, out ITypeSymbol element)
    {
        if (type is INamedTypeSymbol named && named.IsGenericType && named.Name == "IEnumerable" && named.TypeArguments.Length == 1)
        { element = named.TypeArguments[0]; return true; }
        foreach (var iface in type.AllInterfaces)
            if (iface.IsGenericType && iface.Name == "IEnumerable" && iface.TypeArguments.Length == 1)
            { element = iface.TypeArguments[0]; return true; }
        element = null!; return false;
    }
    private IEnumerable<ISymbol> AllMembers(string name)
        => profile.MeasureEnumerable("AllMembers", GetWorldMembers(name));
    private IReadOnlyList<ISymbol> GetWorldMembers(string name)
    {
        lock (worldMembersGate)
        {
            if (worldMembersByName.TryGetValue(name, out var cached))
            {
                profile.Count("WorldMembersCacheHit");
                return cached;
            }
            profile.Count("WorldMembersCacheMiss");
            var members = new List<ISymbol>();
            for (INamedTypeSymbol? t = world; t != null; t = t.BaseType)
                foreach (var member in profile.MeasureEnumerable("Roslyn.GetMembers", string.IsNullOrEmpty(name) ? t.GetMembers() : t.GetMembers(name)))
                    if (Accessible(member))
                        members.Add(member);
            worldMembersByName[name] = members;
            return members;
        }
    }
    private bool Accessible(ISymbol member)
    {
        lock (accessibilityGate)
        {
            if (accessibilityBySymbol.TryGetValue(member, out var cached)) return cached;
            var result = profile.Measure("Accessible", () => !SegusumGeneratedSource.IsGenerated(member) && profile.Measure("Roslyn.IsSymbolAccessibleWithin", () => compilation.IsSymbolAccessibleWithin(member, world, world)));
            accessibilityBySymbol[member] = result;
            return result;
        }
    }
    private bool Accessible(ISymbol member, ITypeSymbol receiverType)
    {
        if (accessibilityByReceiver.TryGetValue(member, out var byType) && byType.TryGetValue(receiverType, out var cached))
        {
            profile.Count("Accessible(receiver) cache hit");
            return cached;
        }
        profile.Count("Accessible(receiver) cache miss");
        var result = Accessible(member) &&
            (member.DeclaredAccessibility is not (Accessibility.Protected or Accessibility.ProtectedAndInternal or Accessibility.ProtectedOrInternal) || IsSameOrDerived(receiverType, world));
        if (!accessibilityByReceiver.TryGetValue(member, out byType)) accessibilityByReceiver[member] = byType = new Dictionary<ITypeSymbol, bool>(SymbolEqualityComparer.Default);
        byType[receiverType] = result;
        return result;
    }
    private static bool IsSameOrDerived(ITypeSymbol candidate, INamedTypeSymbol baseType)
    {
        for (var type = candidate as INamedTypeSymbol; type != null; type = type.BaseType)
            if (SymbolEqualityComparer.Default.Equals(type, baseType)) return true;
        return false;
    }
    private static bool IsDerivedFrom(INamedTypeSymbol type, INamedTypeSymbol baseType) { for (var t = type.BaseType; t != null; t = t.BaseType) if (SymbolEqualityComparer.Default.Equals(t, baseType)) return true; return false; }
    private IEnumerable<ISymbol> MembersOf(ITypeSymbol type, string? name = null)
    {
        var key = name ?? "\0";
        if (membersByReceiverType.TryGetValue(type, out var byName) && byName.TryGetValue(key, out var cached))
        {
            profile.Count("MembersOf cache hit");
            return profile.MeasureEnumerable("MembersOf", cached);
        }
        profile.Count("MembersOf cache miss");
        var members = MembersOfCore(type, name).ToArray();
        if (!membersByReceiverType.TryGetValue(type, out byName)) membersByReceiverType[type] = byName = new Dictionary<string, IReadOnlyList<ISymbol>>(StringComparer.Ordinal);
        byName[key] = members;
        return profile.MeasureEnumerable("MembersOf", members);
    }
    private IEnumerable<ISymbol> MembersOfCore(ITypeSymbol type, string? name = null)
    {
        if (type is IArrayTypeSymbol)
        {
            var arrayType = GetTypeByMetadataName("System.Array");
            if (arrayType != null)
                foreach (var member in profile.MeasureEnumerable("Roslyn.GetMembers", name == null ? arrayType.GetMembers() : arrayType.GetMembers(name))) yield return member;
            yield break;
        }
        for (var t = type as INamedTypeSymbol; t != null; t = t.BaseType)
            foreach (var member in profile.MeasureEnumerable("Roslyn.GetMembers", name == null ? t.GetMembers() : t.GetMembers(name))) yield return member;
    }
    private bool TryGetTypeBySimpleName(string name, out INamedTypeSymbol type)
    {
        var started = Stopwatch.GetTimestamp();
        profile.Count("TryGetTypeBySimpleName");
        try
        {
            EnsureTypeIndex();
            if (typesBySimpleName.TryGetValue(name, out var candidate) && candidate != null)
            { type = candidate; return true; }
            type = null!;
            return false;
        }
        finally { profile.Add("TryGetTypeBySimpleName", Stopwatch.GetTimestamp() - started); }
    }
    private bool TryGetTypeBySimpleNameAndMember(string name, string memberName, out INamedTypeSymbol type)
    {
        // Prefer an unambiguous metadata lookup when the receiver is written
        // as a type name.  This avoids losing enum/static members in a large
        // compilation where the simple-name candidate index contains more
        // context than the current source file requires.
        var direct = GetTypeByMetadataName(name)
            ?? GetTypeByMetadataName("Seg." + name)
            ?? GetTypeByMetadataName("System." + name);
        if (direct != null && direct.GetMembers(memberName).Length != 0)
        {
            type = direct;
            return true;
        }
        EnsureTypeCandidates();
        if (typeCandidatesBySimpleName.TryGetValue(name, out var candidates))
        {
            var matching = candidates.Where(x => x.GetMembers(memberName).Length != 0).ToArray();
            if (matching.Length == 1) { type = matching[0]; return true; }
        }
        return TryGetTypeBySimpleName(name, out type);
    }
    private void EnsureTypeCandidates()
    {
        if (typeCandidatesBuilt) return;
        typeCandidatesBuilt = true;
        VisitAllTypesForCandidates(compilation.GlobalNamespace);
        foreach (var assembly in compilation.SourceModule.ReferencedAssemblySymbols)
            VisitAllTypesForCandidates(assembly.GlobalNamespace);
    }
    private void VisitAllTypesForCandidates(INamespaceSymbol current)
    {
        foreach (var member in current.GetMembers())
        {
            if (member is INamespaceSymbol child) VisitAllTypesForCandidates(child);
            else if (member is INamedTypeSymbol type) VisitTypeForCandidates(type);
        }
    }
    private void VisitTypeForCandidates(INamedTypeSymbol type)
    {
        if (!typeCandidatesBySimpleName.TryGetValue(type.Name, out var list)) typeCandidatesBySimpleName[type.Name] = list = new();
        if (!list.Any(x => SymbolEqualityComparer.Default.Equals(x, type))) list.Add(type);
        foreach (var nested in type.GetTypeMembers()) VisitTypeForCandidates(nested);
    }
    private void EnsureTypeIndex()
    {
        profile.Count("EnsureTypeIndex");
        if (typeIndexBuilt) return;
        var started = Stopwatch.GetTimestamp();
        typeIndexBuilt = true;
        if (semanticIndexes != null)
        {
            var cachedIndex = semanticIndexes.GetTypeIndex(world);
            profile.Count("EnsureTypeIndex cached copy entries", cachedIndex.Count);
            foreach (var item in cachedIndex) typesBySimpleName[item.Key] = item.Value;
            profile.Add("EnsureTypeIndex cached", Stopwatch.GetTimestamp() - started);
            return;
        }
        VisitNamespace(compilation.GlobalNamespace);
        profile.Add("EnsureTypeIndex build", Stopwatch.GetTimestamp() - started);
    }
    private void VisitNamespace(INamespaceSymbol current)
    {
        profile.Count("VisitNamespace");
        foreach (var member in profile.MeasureEnumerable("Roslyn.GetMembers", current.GetMembers()))
        {
            if (member is INamespaceSymbol childNamespace) VisitNamespace(childNamespace);
            else if (member is INamedTypeSymbol type) VisitType(type);
        }
    }
    private void VisitType(INamedTypeSymbol type)
    {
        profile.Count("VisitType");
        if (!typeCandidatesBySimpleName.TryGetValue(type.Name, out var candidates)) typeCandidatesBySimpleName[type.Name] = candidates = new();
        if (!candidates.Any(x => SymbolEqualityComparer.Default.Equals(x, type))) candidates.Add(type);
        if (Accessible(type))
        {
            if (!typesBySimpleName.TryGetValue(type.Name, out var existing)) typesBySimpleName[type.Name] = type;
            else if (!SymbolEqualityComparer.Default.Equals(existing, type)) typesBySimpleName[type.Name] = null;
        }
        foreach (var nested in type.GetTypeMembers()) VisitType(nested);
    }
    private INamedTypeSymbol? GetTypeByMetadataName(string metadataName)
        => profile.Measure("Roslyn.GetTypeByMetadataName", () => compilation.GetTypeByMetadataName(metadataName));
    private SemanticModel GetSemanticModel(SyntaxTree tree)
        => profile.Measure("Roslyn.GetSemanticModel", () => compilation.GetSemanticModel(tree));
    private SyntaxNode GetRoot(SyntaxTree tree)
        => profile.Measure("Roslyn.GetRoot", () => tree.GetRoot());
    private ITypeSymbol? TypeOf(string name)
    {
        var normalized = name.Trim();
        if (normalized.EndsWith("[]", StringComparison.Ordinal))
        {
            var element = TypeOf(normalized.Substring(0, normalized.Length - 2));
            return element == null ? null : compilation.CreateArrayTypeSymbol(element);
        }
        if (normalized.EndsWith("?", StringComparison.Ordinal))
        {
            var value = TypeOf(normalized.Substring(0, normalized.Length - 1));
            var nullable = GetTypeByMetadataName("System.Nullable`1");
            return value is { IsValueType: true } && nullable != null && value.NullableAnnotation != NullableAnnotation.Annotated
                ? nullable.Construct(value)
                : value;
        }
        if (TrySplitGenericType(normalized, out var genericName, out var arguments))
        {
            var definition = ResolveGenericDefinition(genericName, arguments.Count);
            if (definition == null) return null;
            var typeArguments = arguments.Select(TypeOf).ToArray();
            if (typeArguments.Any(x => x == null) || typeArguments.Any(x => x is IErrorTypeSymbol)) return null;
            try { return definition.Construct(typeArguments.Cast<ITypeSymbol>().ToArray()); }
            catch (ArgumentException) { return null; }
        }
        return normalized switch
        {
            "void" => compilation.GetSpecialType(SpecialType.System_Void),
            "bool" => compilation.GetSpecialType(SpecialType.System_Boolean),
            "byte" => compilation.GetSpecialType(SpecialType.System_Byte),
            "char" => compilation.GetSpecialType(SpecialType.System_Char),
            "decimal" => compilation.GetSpecialType(SpecialType.System_Decimal),
            "double" => compilation.GetSpecialType(SpecialType.System_Double),
            "float" => compilation.GetSpecialType(SpecialType.System_Single),
            "int" => compilation.GetSpecialType(SpecialType.System_Int32),
            "long" => compilation.GetSpecialType(SpecialType.System_Int64),
            "object" => compilation.GetSpecialType(SpecialType.System_Object),
            "short" => compilation.GetSpecialType(SpecialType.System_Int16),
            "string" => compilation.GetSpecialType(SpecialType.System_String),
            "DateTime" => dateTime,
            "DateTime?" => dateTimeNullable,
            _ => ResolveNominalType(normalized)
        };
    }
    private static bool TrySplitGenericType(string text, out string name, out IReadOnlyList<string> arguments)
    {
        name = text; arguments = Array.Empty<string>();
        var open = text.IndexOf('<');
        if (open < 0) return false;
        if (!text.EndsWith(">", StringComparison.Ordinal)) return false;
        var depth = 0; var close = -1;
        for (var i = open; i < text.Length; i++)
        {
            if (text[i] == '<') depth++;
            else if (text[i] == '>' && --depth == 0) { close = i; break; }
        }
        if (close != text.Length - 1 || depth != 0) return false;
        var parts = new List<string>(); var start = open + 1; depth = 0;
        for (var i = open + 1; i < close; i++)
        {
            if (text[i] == '<') depth++;
            else if (text[i] == '>') depth--;
            else if (text[i] == ',' && depth == 0) { parts.Add(text.Substring(start, i - start).Trim()); start = i + 1; }
        }
        parts.Add(text.Substring(start, close - start).Trim());
        if (parts.Any(string.IsNullOrWhiteSpace)) return false;
        name = text.Substring(0, open).Trim(); arguments = parts;
        return true;
    }
    private INamedTypeSymbol? ResolveGenericDefinition(string name, int arity)
    {
        var metadataName = name.Contains('.') ? name + "`" + arity : null;
        var candidates = metadataName == null
            ? new[] { "System.Collections.Generic." + name + "`" + arity, "System." + name + "`" + arity, "Seg." + name + "`" + arity, name + "`" + arity }
            : new[] { metadataName };
        foreach (var candidate in candidates)
        {
            var type = GetTypeByMetadataName(candidate);
            if (type != null) return type;
        }
        // The fallback is deliberately on the bare generic name, never on the
        // source spelling containing angle brackets.
        return TryGetTypeBySimpleName(name, out var simple) && simple.Arity == arity ? simple : null;
    }
    private ITypeSymbol? ResolveNominalType(string name)
    {
        var metadataCandidates = name.StartsWith("Seg.", StringComparison.Ordinal)
            ? new[] { name }
            : new[] { "Seg." + name, name, "System." + name };
        foreach (var candidate in metadataCandidates)
        {
            var type = GetTypeByMetadataName(candidate);
            if (type != null) return type;
        }
        return TryGetTypeBySimpleName(name, out var simpleType) ? simpleType : null;
    }
    private string NormalizeKey(string name) => profile.Measure("NormalizeKey", () => DslNames.Camel(name).ToUpperInvariant());
    private void Require(ITypeSymbol? actual, ITypeSymbol? expected, SourceSpan span, string message) { if (actual == null || expected == null || !Compatible(actual, expected)) Report("SEGDSL313", message, span); }
    private void Report(string id, string message, SourceSpan span) { if (!suppressDiagnostics) report(new DslDiagnostic(id, message, span)); }
    private static string Name(string name) => name.Contains('-') ? DslNames.Camel(name) : name;
    private static int MaxPlaceholder(string text)
    {
        var max = 0;
        for (var i = 0; i + 2 < text.Length; i++)
        {
            if (text[i] != '{') continue;
            var end = text.IndexOf('}', i + 1);
            if (end <= i + 1) continue;
            if (int.TryParse(text.Substring(i + 1, end - i - 1), out var value)) max = Math.Max(max, value);
            i = end;
        }
        return max;
    }
    private void CheckDuplicateCombines(IEnumerable<DslDeclaration> declarations) { var combines = declarations.OfType<HandlerDeclaration>().Where(x => x.Kind == "combine").GroupBy(x => NormalizeKey(x.First) + "\0" + NormalizeKey(x.Second!)); foreach (var group in combines.Where(x => x.Count() > 1)) foreach (var item in group.Skip(1)) Report("SEGDSL315", "Duplicate combine handler.", item.Span); }
    private void CheckDuplicateBeforeRoomChange(IEnumerable<DslDeclaration> declarations)
    { foreach (var item in declarations.OfType<BeforeRoomChangeDeclaration>().Skip(1)) Report("SEGDSL334", "Duplicate before-room-change declaration for the same world.", item.Span); }
    private void CheckDuplicateAfterActionExecuted(IEnumerable<DslDeclaration> declarations)
    { foreach (var item in declarations.OfType<AfterActionExecutedDeclaration>().Skip(1)) Report("SEGDSL335", "Duplicate after-action-executed declaration for the same world.", item.Span); }
    private void CheckDuplicateRoomChanged(IEnumerable<DslDeclaration> declarations) { foreach (var group in declarations.OfType<HandlerDeclaration>().Where(x => x.Kind == "room-changed").GroupBy(x => NormalizeKey(x.First))) foreach (var item in group.Skip(1)) Report("SEGDSL319", "Duplicate room-changed handler for the same Room.", item.Span); }
    private void CheckDuplicateUnaryHandlers(IEnumerable<DslDeclaration> declarations)
    {
        foreach (var kind in new[] { "pickup", "talk-here", "cancel-text-input", "submit-text-input" })
            foreach (var group in declarations.OfType<HandlerDeclaration>().Where(x => x.Kind == kind).GroupBy(x => NormalizeKey(x.First)))
                foreach (var item in group.Skip(1)) Report("SEGDSL329", $"Duplicate {kind} handler for the same target.", item.Span);
    }
    private void CheckCSharpRoomChangedDuplicates(IEnumerable<DslDeclaration> declarations)
    {
        var handlers = declarations.OfType<HandlerDeclaration>().Where(x => x.Kind == "room-changed").ToArray();
        if (handlers.Length == 0) return;
        if (semanticIndexes != null)
        {
            var targets = semanticIndexes.GetRoomChangedTargets();
            foreach (var handler in handlers)
                if (dslRoomChangedTargets.Any(targets.Contains))
                    Report("SEGDSL319", "Duplicate room-changed handler: the Room is already registered by C#.", handler.Span);
            return;
        }
        var treeCount = 0;
        var invocationCount = 0;
        foreach (var tree in compilation.SyntaxTrees)
        {
            treeCount++;
            var model = GetSemanticModel(tree);
            if (SegusumGeneratedSource.IsGenerated(tree)) continue;
            foreach (var invocation in profile.MeasureEnumerable("Roslyn.DescendantNodes", GetRoot(tree).DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>()))
            {
                invocationCount++;
                if (invocation.Expression is not Microsoft.CodeAnalysis.CSharp.Syntax.IdentifierNameSyntax { Identifier.ValueText: "addRoomChangedHandler" } || invocation.ArgumentList.Arguments.Count == 0) continue;
                var argument = invocation.ArgumentList.Arguments[0].Expression;
                var symbol = model.GetSymbolInfo(argument).Symbol;
                if (symbol == null || !dslRoomChangedTargets.Contains(symbol)) continue;
                foreach (var handler in handlers) Report("SEGDSL319", "Duplicate room-changed handler: the Room is already registered by C#.", handler.Span);
            }
        }
    }
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
    private static IEnumerable<NamedCutsceneStatement> FindNamedCutscenes(DslDeclaration declaration) => declaration switch
    {
        HandlerDeclaration h => FindNamedCutscenes(h.Body), FunctionDeclaration f => FindNamedCutscenes(f.Body), CycleElementDeclaration c => FindNamedCutscenes(c.Body), _ => Enumerable.Empty<NamedCutsceneStatement>()
    };
    private static IEnumerable<NamedCutsceneStatement> FindNamedCutscenes(IEnumerable<DslStatement> statements) => statements.SelectMany(s => s switch
    {
        NamedCutsceneStatement n => new[] { n }.Concat(FindNamedCutscenes(n.Body)),
        IfStatement i => i.Branches.SelectMany(x => FindNamedCutscenes(x.Body)).Concat(i.ElseBody == null ? Enumerable.Empty<NamedCutsceneStatement>() : FindNamedCutscenes(i.ElseBody)),
        AddCycleElementStatement a => FindNamedCutscenes(a.Body), _ => Enumerable.Empty<NamedCutsceneStatement>()
    });
    private IReadOnlyList<ISymbol> ResolveCSharpCandidates(string name)
    {
        var exact = ResolveCSharpMembers(name);
        return exact.Count != 0 ? exact : DslNames.Candidates(name).Skip(1).SelectMany(AllMembers).ToArray();
    }
}
