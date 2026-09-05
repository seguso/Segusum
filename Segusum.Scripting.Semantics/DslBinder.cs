using System;
using System.Collections.Generic;
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
public sealed class BoundModel
{
    public Dictionary<DslExpression, BoundValue> Values { get; } = new(ReferenceComparer<DslExpression>.Instance);
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
    private readonly Dictionary<string, ITypeSymbol> namedCutsceneGlobals = new(StringComparer.Ordinal);
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
    private readonly ITypeSymbol? walkPath;
    private ISymbol? lastSymbol;
    private BoundSymbolKind lastKind;
    private string lastCSharpName = "";
    private readonly HashSet<string> currentParameters = new(StringComparer.Ordinal);
    private readonly Dictionary<string, DslSymbolIdentity> activeDslSymbols = new(StringComparer.Ordinal);
    private readonly HashSet<ISymbol> dslRoomChangedTargets = new(SymbolEqualityComparer.Default);
    private readonly HashSet<DslExpression> nullLiterals = new(ReferenceComparer<DslExpression>.Instance);
    private bool suppressDiagnostics;

    public BoundModel Model => model;
    public DslBinder(Compilation compilation, INamedTypeSymbol world, Action<DslDiagnostic> report)
    {
        this.compilation = compilation; this.world = world; this.report = report;
        cycle = compilation.GetTypeByMetadataName("Seg.Cycle"); cycleElementId = compilation.GetTypeByMetadataName("Seg.CycleElemId"); namedCutsceneId = compilation.GetTypeByMetadataName("Seg.NamedCutSceneId");
        logicObj = compilation.GetTypeByMetadataName("Seg.LogicObj"); objective = compilation.GetTypeByMetadataName("Seg.Objective"); room = compilation.GetTypeByMetadataName("Seg.Room"); explanation = compilation.GetTypeByMetadataName("Seg.Explanation"); beforeRoomChangeInput = compilation.GetTypeByMetadataName("Seg.BeforeRoomChangeInput"); walkPath = compilation.GetTypeByMetadataName("Seg.WalkPath");
        dateTime = compilation.GetSpecialType(SpecialType.System_DateTime); dateTimeNullable = compilation.GetSpecialType(SpecialType.System_Nullable_T).Construct(dateTime); textHandlerInput = compilation.GetTypeByMetadataName("Seg.TextHandlerInput");
    }
    public void Bind(IReadOnlyList<DslDeclaration> declarations)
    {
        foreach (var declaration in declarations)
        {
            switch (declaration)
            {
                case StateDeclaration state: AddDslIdentity(state.Name, "state", state.Span); break;
                case FunctionDeclaration function: AddDslIdentity(function.Name, "function", function.Span); break;
                case CycleElementDeclaration element: AddDslIdentity(element.Id, "cycle-element", element.Span); break;
            }
        }
        foreach (var id in declarations.SelectMany(FindNamedCutscenes)) AddDslIdentity(id.Id, "named-cutscene", id.Span);
        foreach (var state in declarations.OfType<StateDeclaration>()) AddGlobal(state.Name, TypeOf(state.Type), state.Span, BoundSymbolKind.State);
        foreach (var cycleDeclaration in declarations.OfType<CycleDeclaration>()) AddGlobal(cycleDeclaration.Variable, cycle, cycleDeclaration.Span, BoundSymbolKind.Cycle);
        foreach (var element in declarations.OfType<CycleElementDeclaration>()) AddGlobal(element.Id, cycleElementId, element.Span, BoundSymbolKind.CycleElementId);
        foreach (var element in declarations.SelectMany(FindNestedElements)) AddGlobal(element.Id, cycleElementId, element.Span, BoundSymbolKind.CycleElementId);
        foreach (var id in declarations.SelectMany(FindNamedCutscenes)) AddNamedCutsceneGlobal(id);
        foreach (var function in declarations.OfType<FunctionDeclaration>()) { var key = NormalizeKey(function.Name); if (functions.ContainsKey(key)) Report("SEGDSL303", "Duplicate DSL function.", function.Span); else functions[key] = function; }
        foreach (var declaration in declarations)
        {
            switch (declaration)
            {
                case StateDeclaration s: BindExpression(s.Initializer, new()); break;
                case FunctionDeclaration f: BindFunction(f); break;
                case HandlerDeclaration h: BindHandler(h); break;
                case CycleElementDeclaration c: BindCycle(c.Cycle, c.Repeat, c.Condition, c.Body, c.Span, new()); break;
                case NextCycleDeclaration n: Require(BindExpression(n.Cycle, new()), cycle, n.Cycle.Span, "next requires a Cycle."); break;
                case BeforeRoomChangeDeclaration b: BindBeforeRoomChange(b); break;
            }
        }
        CheckDuplicateCombines(declarations);
        CheckDuplicateRoomChanged(declarations);
        CheckDuplicateUnaryHandlers(declarations);
        CheckCSharpRoomChangedDuplicates(declarations);
        CheckDuplicateBeforeRoomChange(declarations);
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
    private void AddGlobal(string name, ITypeSymbol? type, SourceSpan span, BoundSymbolKind kind)
    { if (kind == BoundSymbolKind.CycleElementId) { if (!Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(name) || name.Contains('-')) { Report("SEGDSL318", "CycleElementId must be a stable C# identifier and cannot contain '-'.", span); return; } if (cycleElementGlobals.ContainsKey(name)) { Report("SEGDSL314", $"Duplicate CycleElementId '{name}'.", span); return; } if (ResolveCSharpCandidates(name).Count != 0) Report("SEGDSL317", $"CycleElementId '{name}' collides with an existing World member.", span); if (type != null) cycleElementGlobals[name] = type; AddDslIdentity(name, "cycle-element", span); model.References[name] = name; return; } var key = NormalizeKey(name); if (globals.ContainsKey(key)) Report(kind == BoundSymbolKind.NamedCutsceneId ? "SEGDSL324" : "SEGDSL304", $"Duplicate or normalized-colliding global '{name}'.", span); else if (type != null) { globals[key] = type; globalKinds[key] = kind; model.References[name] = Name(name); } }
    private void BindFunction(FunctionDeclaration f)
    { var scope = new Dictionary<string, ITypeSymbol>(StringComparer.Ordinal); currentParameters.Clear(); activeDslSymbols.Clear(); foreach (var p in f.Parameters) { scope[NormalizeKey(p.Name)] = TypeOf(p.Type)!; currentParameters.Add(NormalizeKey(p.Name)); AddLocalIdentity(p.Name, "parameter", f.Span); } BindStatements(f.Body, scope, f.ReturnType == null ? null : TypeOf(f.ReturnType)); currentParameters.Clear(); activeDslSymbols.Clear(); }
    private void BindHandler(HandlerDeclaration h)
    {
        var first = BindName(h.First, h.FirstSpan); var second = h.Second == null ? null : BindName(h.Second, h.SecondSpan ?? h.Span); var target = h.Target == null ? null : BindName(h.Target, h.TargetSpan ?? h.Span);
        if (h.Kind == "combine") { Require(first, logicObj, h.Span, "combine first operand must be LogicObj."); Require(second, logicObj, h.Span, "combine second operand must be LogicObj."); }
        if (h.Kind == "use-for") { Require(first, logicObj, h.Span, "use-for object must be LogicObj."); Require(target, objective, h.Span, "use-for target must be Objective."); }
        if (h.Kind == "use-here") Require(first, logicObj, h.Span, "use-here object must be LogicObj.");
        if (h.Kind == "pickup") Require(first, logicObj, h.Span, "pickup target must be LogicObj.");
        if (h.Kind == "talk-here") Require(first, room, h.Span, "talk-here target must be Room.");
        if (h.Kind is "cancel-text-input" or "submit-text-input") Require(first, compilation.GetTypeByMetadataName("Seg.TextInput"), h.Span, $"{h.Kind} target must be TextInput.");
        if (h.Kind == "room-changed")
        {
            Require(first, room, h.Span, "room-changed target must be Room.");
            if (lastSymbol != null) dslRoomChangedTargets.Add(lastSymbol);
        }
        if (h.Explanation != null) Require(BindExpression(h.Explanation, new()), explanation, h.Explanation.Span, "exp must be Explanation.");
        if (h.Condition != null) Require(BindExpression(h.Condition, new()), compilation.GetSpecialType(SpecialType.System_Boolean), h.Condition.Span, "possible-when must be bool.");
        var previousInputType = inputType;
        inputType = h.Kind == "submit-text-input" ? textHandlerInput : compilation.GetTypeByMetadataName("Seg.HandlerInput");
        inputContextAllowed = h.Kind == "submit-text-input";
        BindStatements(h.Body, new(), null);
        inputType = previousInputType;
        inputContextAllowed = false;
    }
    private void BindCycle(string cycleName, string? repeat, DslExpression? condition, IReadOnlyList<DslStatement> body, SourceSpan span, Dictionary<string, ITypeSymbol>? scope = null)
    { scope ??= new(); Require(BindName(cycleName, span, scope), cycle, span, "add requires a Cycle."); if (repeat != null && repeat is not ("once" or "forever")) Report("SEGDSL316", $"Unknown Repeat modifier '{repeat}'.", span); if (condition != null) Require(BindExpression(condition, scope, dateTimeNullable), compilation.GetSpecialType(SpecialType.System_Boolean), condition.Span, "when must be bool."); BindStatements(body, new(scope), null); }
    private void BindStatements(IEnumerable<DslStatement> statements, Dictionary<string, ITypeSymbol> scope, ITypeSymbol? returnType)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case VariableDeclaration v:
                    var type = BindExpression(v.Initializer, scope); if (type != null) { scope[NormalizeKey(v.Name)] = type; AddLocalIdentity(v.Name, "local", v.Span); } break;
                case AssignmentStatement a:
                    if (a.Receiver != null)
                    {
                        var receiver = BindExpression(a.Receiver, scope);
                        var target = receiver == null ? null : MembersOf(receiver, a.MemberName ?? a.Name).FirstOrDefault(x => Accessible(x, receiver));
                        if (target is not IPropertySymbol { SetMethod: not null } && target is not IFieldSymbol { IsReadOnly: false }) Report("SEGDSL321", $"Member '{a.MemberName ?? a.Name}' is not writable.", a.Span);
                        RequireExpression(a.Value, BindExpression(a.Value, scope), target is IPropertySymbol p ? p.Type : target is IFieldSymbol f ? f.Type : null, "assignment type mismatch.");
                    }
                    else { var targetType = BindName(a.Name, a.Span, scope); RequireExpression(a.Value, BindExpression(a.Value, scope), targetType, "assignment type mismatch."); }
                    break;
                case IncrementStatement i: Require(BindName(i.Name, i.Span, scope), compilation.GetSpecialType(SpecialType.System_Int32), i.Span, "++ requires int."); break;
                case ReturnStatement r: Require(BindExpression(r.Expression, scope), returnType, r.Span, "return type mismatch."); break;
                case CallStatement c: BindExpression(c.Expression, scope); break;
                case NextCycleStatement n: Require(BindExpression(n.Cycle, scope), cycle, n.Span, "next requires a Cycle."); break;
                case AddCycleElementStatement a: BindCycle(a.Cycle, a.Repeat, a.Condition, a.Body, a.Span, scope); break;
                case IfStatement i:
                    foreach (var branch in i.Branches) { Require(BindExpression(branch.Condition, scope), compilation.GetSpecialType(SpecialType.System_Boolean), branch.Condition.Span, "if condition must be bool."); BindStatements(branch.Body, new(scope), returnType); }
                    if (i.ElseBody != null) BindStatements(i.ElseBody, new(scope), returnType); break;
                case DialogueStatement d:
                    Require(BindName(d.Character, d.CharacterSpan, scope), compilation.GetTypeByMetadataName("Seg.Character"), d.Span, "dialogue speaker must be Character.");
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
                case NamedCutsceneStatement n: BindNamedCutscene(n, scope); break;
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
    private void BindNamedCutscene(NamedCutsceneStatement statement, Dictionary<string, ITypeSymbol>? scope = null)
    {
        var idType = BindName(statement.Id, statement.IdSpan, scope);
        Require(idType, namedCutsceneId, statement.IdSpan, "named-cutscene id must be a declared NamedCutSceneId.");
        if (statement.Title is not LiteralExpression { Kind: "string" })
            Report("SEGDSL326", "named-cutscene title must be a quoted string literal.", statement.Title.Span);
        else
            Require(BindExpression(statement.Title, scope ?? new()), compilation.GetSpecialType(SpecialType.System_String), statement.Title.Span, "named-cutscene title must be a string literal.");
        foreach (var argument in statement.Arguments) BindExpression(argument, scope ?? new());
        BindStatements(statement.Body, scope == null ? new() : new(scope), null);
    }
    private void AddNamedCutsceneGlobal(NamedCutsceneStatement statement)
    {
        var key = NormalizeKey(statement.Id);
        if (namedCutsceneGlobals.ContainsKey(key) || globals.ContainsKey(key)) { Report("SEGDSL324", $"Duplicate named-cutscene id '{statement.Id}'.", statement.IdSpan); return; }
        if (ResolveCSharpMembers(statement.Id).Count != 0) { Report("SEGDSL325", $"Named-cutscene id '{statement.Id}' collides with an existing World member; remove the C# member before migrating it.", statement.IdSpan); return; }
        if (namedCutsceneId != null) namedCutsceneGlobals[key] = namedCutsceneId;
        model.References[statement.Id] = Name(statement.Id);
    }
    private ITypeSymbol? BindExpression(DslExpression expression, Dictionary<string, ITypeSymbol> scope, ITypeSymbol? contextualIt = null)
    {
        switch (expression)
        {
            case LiteralExpression l: if (l.Kind == "null") { nullLiterals.Add(l); return null; } return l.Kind is "string" or "raw-string" ? compilation.GetSpecialType(SpecialType.System_String) : l.Kind == "bool" ? compilation.GetSpecialType(SpecialType.System_Boolean) : l.Kind == "cycle" ? cycle : compilation.GetSpecialType(SpecialType.System_Int32);
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
            case ParenthesizedExpression p: return BindExpression(p.Expression, scope, contextualIt);
            case UnaryExpression u: var ut = BindExpression(u.Operand, scope, contextualIt); if (u.Operator == "not") Require(ut, compilation.GetSpecialType(SpecialType.System_Boolean), u.Span, "not requires bool."); return compilation.GetSpecialType(SpecialType.System_Boolean);
            case BinaryExpression b:
                var lt = BindExpression(b.Left, scope, contextualIt); var rt = BindExpression(b.Right, scope, contextualIt);
                if (b.Operator is "and" or "or") { Require(lt, compilation.GetSpecialType(SpecialType.System_Boolean), b.Left.Span, "logical operand must be bool."); Require(rt, compilation.GetSpecialType(SpecialType.System_Boolean), b.Right.Span, "logical operand must be bool."); return compilation.GetSpecialType(SpecialType.System_Boolean); }
                return b.Operator is "==" or "!=" or ">" or ">=" or "<" or "<=" ? compilation.GetSpecialType(SpecialType.System_Boolean) : lt;
            case MemberAccessExpression m:
                var receiverType = BindExpression(m.Receiver, scope, contextualIt);
                if (m.Receiver is IdentifierExpression { Name: "input" } && m.MemberName == "wordsLower")
                {
                    if (!inputContextAllowed) { Report("SEGDSL330", "'input.wordsLower' is only valid inside submit-text-input.", m.Span); return null; }
                    var stringType = compilation.GetSpecialType(SpecialType.System_String);
                    var list = compilation.GetTypeByMetadataName("System.Collections.Generic.List`1")?.Construct(stringType);
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
            default: return null;
        }
    }
    private ITypeSymbol? BindCall(CallExpression call, Dictionary<string, ITypeSymbol> scope, ITypeSymbol? contextualIt)
    {
        if (call.Name == "not-seen-recently") { if (call.Arguments.Count == 2) { var receiver = call.Arguments[0].Expression; Require(BindExpression(receiver, scope, contextualIt), dateTimeNullable, call.Arguments[0].Span, "not-seen-recently receiver must be DateTime?."); Require(BindExpression(call.Arguments[1].Expression, scope), compilation.GetSpecialType(SpecialType.System_Int32), call.Arguments[1].Span, "cooldown must be numeric."); model.DomainOperations[call] = new BoundDomainOperation(BoundDomainOperationKind.NotSeenRecently, receiver, call.Arguments[1].Expression, null); } return compilation.GetSpecialType(SpecialType.System_Boolean); }
        if (call.Name == "was-seen-at-least-once") { if (call.Arguments.Count == 1) { var receiver = call.Arguments[0].Expression; var t = BindExpression(receiver, scope); Require(t, cycleElementId, call.Arguments[0].Span, "was-seen-at-least-once requires CycleElemId."); model.DomainOperations[call] = new BoundDomainOperation(BoundDomainOperationKind.WasSeenAtLeastOnce, receiver, null, null); } return compilation.GetSpecialType(SpecialType.System_Boolean); }
        if (functions.TryGetValue(NormalizeKey(call.Name), out var function)) { var result = BindArgumentList(call, function.Parameters.Select(p => new ParameterInfo(p.Name, TypeOf(p.Type)!, false)).ToArray(), scope, contextualIt); if (result.Call == null) { ReportFailure(call, new[] { result }); return null; } RecordDslReference(call.Name, call.NameSpan, BoundSymbolKind.Function, function.Name, "invocation"); model.Calls[call] = new BoundCall(null, Name(function.Name), result.Call.Arguments, TypeOf(function.ReturnType ?? "void")); return TypeOf(function.ReturnType ?? "void"); }
        var receiverType = call.Receiver == null ? null : BindExpression(call.Receiver, scope, contextualIt);
        var exactMethods = (receiverType == null ? AllMembers(call.Name) : MembersOf(receiverType, call.Name).Where(x => Accessible(x, receiverType))).OfType<IMethodSymbol>().ToArray();
        var fallbackMembers = receiverType == null ? DslNames.Candidates(call.Name).Skip(1).SelectMany(AllMembers) : MembersOf(receiverType).Where(x => Accessible(x, receiverType) && NormalizeKey(x.Name) == NormalizeKey(call.Name));
        var extensionMethods = receiverType == null ? Enumerable.Empty<IMethodSymbol>() : ExtensionMethodsOf(receiverType, call.Name);
        var methods = (exactMethods.Length != 0 ? exactMethods : fallbackMembers.OfType<IMethodSymbol>().Concat(extensionMethods)).Where(m => NormalizeKey(m.Name) == NormalizeKey(call.Name)).GroupBy(m => m.ToDisplayString()).Select(g => g.First()).ToArray();
        if (methods.Length == 0) { Report("SEGDSL305", $"Unknown function or method '{call.Name}'.", call.Span); return null; }
        var results = methods.Select(m => TryBind(call, m, scope, contextualIt)).ToArray();
        var applicable = results.Where(x => x.Call != null).OrderBy(x => x.Score).ToArray();
        if (applicable.Length == 0) { ReportFailure(call, results); return null; }
        var bestScore = applicable[0].Score; var best = applicable.Where(x => x.Score == bestScore).ToArray();
        if (best.Length != 1) { Report("SEGDSL306", $"Call to '{call.Name}' is ambiguous.", call.Span); return null; }
        var boundCall = best[0].Call! with { Receiver = best[0].Call!.Method?.IsExtensionMethod == true ? null : call.Receiver };
        RecordReference(call.Name, call.NameSpan, BoundSymbolKind.CSharpMethod, boundCall.Method, null, "invocation");
        model.Calls[call] = boundCall; return boundCall.ReturnType;
    }
    private sealed record ParameterInfo(string Name, ITypeSymbol Type, bool Optional, IParameterSymbol? Symbol = null);
    private CandidateResult TryBind(CallExpression call, IMethodSymbol method, Dictionary<string, ITypeSymbol> scope, ITypeSymbol? contextualIt)
    {
        var parameters = method.Parameters.Select(p => new ParameterInfo(p.Name, p.Type, p.IsOptional, p)).ToArray();
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
            if (argument.Name != null) { namedSeen = true; var exact = parameters.Where(p => string.Equals(p.Name, argument.Name, StringComparison.Ordinal)).ToArray(); var normalized = exact.Length == 0 ? parameters.Where(p => NormalizeKey(p.Name) == NormalizeKey(argument.Name)).ToArray() : exact; if (normalized.Length > 1) return Failure(CallFailureKind.UnknownNamedArgument, argument.Span, $"Ambiguous named argument '{argument.Name}'."); var parameter = normalized.SingleOrDefault(); if (parameter == null) return Failure(CallFailureKind.UnknownNamedArgument, argument.Span, $"Unknown named argument '{argument.Name}'."); if (!used.Add(parameter.Name)) return Failure(CallFailureKind.DuplicateNamedArgument, argument.Span, $"Duplicate named argument '{argument.Name}'."); var actual = BindExpression(argument.Expression, scope, contextualIt); var conversion = Classify(actual, parameter.Type); if (!IsCompatible(argument.Expression, actual, parameter.Type)) return Failure(CallFailureKind.IncompatibleArgument, argument.Expression.Span, $"Argument '{argument.Name}' has incompatible type."); score += ConversionScore(conversion); result.Add(new BoundArgument(argument, parameter.Symbol, parameter.Name)); }
            else { if (namedSeen) return Failure(CallFailureKind.PositionalAfterNamed, argument.Span, "Positional arguments cannot follow a named argument."); if (positionalIndex >= parameters.Count) return Failure(CallFailureKind.TooManyArguments, argument.Span, "Too many arguments."); var parameter = parameters[positionalIndex++]; used.Add(parameter.Name); var actual = BindExpression(argument.Expression, scope, contextualIt); var conversion = Classify(actual, parameter.Type); if (!IsCompatible(argument.Expression, actual, parameter.Type)) return Failure(CallFailureKind.IncompatibleArgument, argument.Expression.Span, "Argument has incompatible type."); score += ConversionScore(conversion); result.Add(new BoundArgument(argument, parameter.Symbol, parameter.Name)); }
        }
        var missing = parameters.FirstOrDefault(p => !p.Optional && !used.Contains(p.Name)); if (missing != null) return Failure(CallFailureKind.MissingRequiredArgument, call.Span, $"Required argument '{missing.Name}' is missing.");
        score += parameters.Count(p => p.Optional && !used.Contains(p.Name)) * 10;
        return new CandidateResult(new BoundCall(null, "", result, null), CallFailureKind.None, call.Span, "", score);
    }
    private CandidateResult Failure(CallFailureKind kind, SourceSpan span, string detail) => new(null, kind, span, detail, int.MaxValue);
    private Conversion Classify(ITypeSymbol? actual, ITypeSymbol expected) => actual == null ? default : Microsoft.CodeAnalysis.CSharp.CSharpExtensions.ClassifyConversion(compilation, actual, expected);
    private static int ConversionScore(Conversion conversion) => conversion.IsIdentity ? 0 : conversion.IsImplicit ? 1 : 1000;
    private void ReportFailure(CallExpression call, IReadOnlyList<CandidateResult> results)
    { var failure = results.Where(x => x.Call == null).OrderBy(x => FailurePriority(x.FailureKind)).FirstOrDefault(); if (failure == null) { Report("SEGDSL306", $"No overload of '{call.Name}' accepts these arguments.", call.Span); return; } Report(failure.FailureKind switch { CallFailureKind.UnknownNamedArgument => "SEGDSL307", CallFailureKind.DuplicateNamedArgument => "SEGDSL308", CallFailureKind.PositionalAfterNamed => "SEGDSL310", CallFailureKind.IncompatibleArgument => "SEGDSL309", CallFailureKind.MissingRequiredArgument => "SEGDSL306", _ => "SEGDSL306" }, failure.FailureDetail, failure.FailureSpan); }
    private static int FailurePriority(CallFailureKind kind) => kind switch { CallFailureKind.UnknownNamedArgument => 0, CallFailureKind.DuplicateNamedArgument => 1, CallFailureKind.PositionalAfterNamed => 2, CallFailureKind.IncompatibleArgument => 3, CallFailureKind.MissingRequiredArgument => 4, _ => 5 };
    private bool Compatible(ITypeSymbol? actual, ITypeSymbol expected) => actual != null && Microsoft.CodeAnalysis.CSharp.CSharpExtensions.ClassifyConversion(compilation, actual, expected).IsImplicit;
    private bool IsCompatible(DslExpression expression, ITypeSymbol? actual, ITypeSymbol? expected) => expected != null && ((nullLiterals.Contains(expression) && (expected.IsReferenceType || expected.OriginalDefinition.SpecialType == SpecialType.System_Nullable_T)) || Compatible(actual, expected));
    private void RequireExpression(DslExpression expression, ITypeSymbol? actual, ITypeSymbol? expected, string message) { if (!IsCompatible(expression, actual, expected)) Report("SEGDSL313", message, expression.Span); }
    private ITypeSymbol? BindName(string name, SourceSpan span, Dictionary<string, ITypeSymbol>? scope = null)
    {
        lastSymbol = null; lastKind = BoundSymbolKind.Local; lastCSharpName = Name(name);
        if (name == "it") { lastKind = BoundSymbolKind.ContextualIt; lastCSharpName = "it"; RecordName(name, span); return dateTimeNullable; }
        var key = NormalizeKey(name);
        if (scope != null && scope.TryGetValue(key, out var local)) { lastKind = currentParameters.Contains(key) ? BoundSymbolKind.Parameter : BoundSymbolKind.Local; model.References[name] = Name(name); RecordName(name, span); return local; }
        if (cycleElementGlobals.TryGetValue(name, out var cycleElement)) { lastKind = BoundSymbolKind.CycleElementId; lastCSharpName = name; model.References[name] = name; RecordName(name, span); return cycleElement; }
        if (namedCutsceneGlobals.TryGetValue(key, out var namedCutscene)) { lastKind = BoundSymbolKind.NamedCutsceneId; lastCSharpName = Name(name); model.References[name] = lastCSharpName; RecordName(name, span); return namedCutscene; }
        if (globals.TryGetValue(key, out var global)) { lastKind = globalKinds[key]; lastCSharpName = Name(name); model.References[name] = lastCSharpName; RecordName(name, span); return global; }
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
    private IReadOnlyList<ISymbol> ResolveCSharpMembers(string name) => AllMembers(name).ToArray();
    private static ITypeSymbol? MemberType(ISymbol symbol) => symbol switch { IFieldSymbol f => f.Type, IPropertySymbol p => p.Type, IMethodSymbol m => m.ReturnType, _ => null };
    private IEnumerable<IMethodSymbol> ExtensionMethodsOf(ITypeSymbol receiverType, string name)
    {
        var methods = new List<IMethodSymbol>();
        foreach (var metadataName in new[] { "Seg.Utils", "System.Linq.Enumerable" })
        {
            var type = compilation.GetTypeByMetadataName(metadataName);
            if (type == null) continue;
            methods.AddRange(type.GetMembers(name).OfType<IMethodSymbol>().Where(x => x.IsExtensionMethod && x.IsStatic));
        }
        methods.AddRange(compilation.GetSymbolsWithName(name, SymbolFilter.Member).OfType<IMethodSymbol>().Where(x => x.IsExtensionMethod && x.IsStatic));
        foreach (var method in methods.GroupBy(x => x.ToDisplayString()).Select(x => x.First()))
        {
            var candidate = method;
            if (candidate.IsGenericMethod && candidate.TypeParameters.Length == 1 && TryGetEnumerableElement(receiverType, out var element))
                candidate = candidate.Construct(element);
            if (candidate.Parameters.Length != 0) yield return candidate;
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
    {
        for (INamedTypeSymbol? t = world; t != null; t = t.BaseType)
            foreach (var member in string.IsNullOrEmpty(name) ? t.GetMembers() : t.GetMembers(name))
                if (Accessible(member))
                    yield return member;
    }
    private bool Accessible(ISymbol member) => !SegusumGeneratedSource.IsGenerated(member) && compilation.IsSymbolAccessibleWithin(member, world, world);
    private bool Accessible(ISymbol member, ITypeSymbol receiverType)
    {
        if (!Accessible(member)) return false;
        if (member.DeclaredAccessibility is not (Accessibility.Protected or Accessibility.ProtectedAndInternal or Accessibility.ProtectedOrInternal)) return true;
        return IsSameOrDerived(receiverType, world);
    }
    private static bool IsSameOrDerived(ITypeSymbol candidate, INamedTypeSymbol baseType)
    {
        for (var type = candidate as INamedTypeSymbol; type != null; type = type.BaseType)
            if (SymbolEqualityComparer.Default.Equals(type, baseType)) return true;
        return false;
    }
    private static bool IsDerivedFrom(INamedTypeSymbol type, INamedTypeSymbol baseType) { for (var t = type.BaseType; t != null; t = t.BaseType) if (SymbolEqualityComparer.Default.Equals(t, baseType)) return true; return false; }
    private static IEnumerable<ISymbol> MembersOf(ITypeSymbol type, string? name = null) { for (var t = type as INamedTypeSymbol; t != null; t = t.BaseType) foreach (var member in name == null ? t.GetMembers() : t.GetMembers(name)) yield return member; }
    private ITypeSymbol? TypeOf(string name) => name switch { "int" => compilation.GetSpecialType(SpecialType.System_Int32), "bool" => compilation.GetSpecialType(SpecialType.System_Boolean), "string" => compilation.GetSpecialType(SpecialType.System_String), _ => compilation.GetTypeByMetadataName(name.StartsWith("Seg.", StringComparison.Ordinal) ? name : "Seg." + name) ?? compilation.GetTypeByMetadataName(name) };
    private static string NormalizeKey(string name) => DslNames.Camel(name).ToUpperInvariant();
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
        foreach (var tree in compilation.SyntaxTrees)
        {
            var model = compilation.GetSemanticModel(tree);
            if (SegusumGeneratedSource.IsGenerated(tree)) continue;
            foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<Microsoft.CodeAnalysis.CSharp.Syntax.InvocationExpressionSyntax>())
            {
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
