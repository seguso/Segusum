using System;
using System.Text;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Text;
using Segusum.Scripting.Core;

namespace Segusum.Scripting.Tooling;

public enum RuntimeIdKind { CycleElement, NamedCutScene }

public sealed record RuntimeIdDeclaration(string Name, RuntimeIdKind Kind, string Path, TextSpan Span, string Text);
public sealed record RuntimeMethodDeclaration(string Name, string ReturnType, IReadOnlyList<string> ParameterTypes, string ContainingType, bool IsStatic, string Path, TextSpan Span, string Text);
public sealed record SegMethodDeclaration(string Name, string ReturnType, IReadOnlyList<(string Name, string Type)> Parameters, SourceSpan Span);

public sealed record SegOwnershipReport(
    IReadOnlySet<string> OwnedCycleElementIds,
    IReadOnlySet<string> OwnedNamedCutSceneIds,
    IReadOnlyList<RuntimeIdDeclaration> RuntimeDeclarations,
    IReadOnlyList<string> RemoveFromCSharp,
    IReadOnlyList<string> KeepInCSharp,
    IReadOnlyList<string> MissingReferenceOnlyRuntimeSymbols,
    IReadOnlyList<string> CSharpSymbolsNotUsedBySeg,
    IReadOnlyList<string> SegOwnedWithoutMatchingLegacy,
    IReadOnlyList<string> Ambiguities,
    IReadOnlyList<SegMethodDeclaration> SegMethods,
    IReadOnlyList<RuntimeMethodDeclaration> RuntimeMethods,
    IReadOnlyList<RuntimeMethodDeclaration> MethodsToRemove,
    IReadOnlyList<RuntimeMethodDeclaration> MethodsKept,
    IReadOnlyList<RuntimeMethodDeclaration> MethodsReferencedByActiveCSharp,
    IReadOnlyList<RuntimeMethodDeclaration> AmbiguousMethodMatches,
    IReadOnlyList<string> SegMethodsWithoutMatchingLegacy)
{
    public bool IsUnambiguous => Ambiguities.Count == 0;
}

/// <summary>
/// Computes migration ownership from the parsed SEG AST and Roslyn declarations.
/// It deliberately does not infer ownership from names or from ordinary references.
/// </summary>
public static class SegOwnership
{
    public static SegOwnershipReport Analyze(string segPath, IEnumerable<string> runtimeCSharpFiles, string historicalSourcePath)
    {
        var source = new DslSource(segPath, File.ReadAllText(segPath));
        var parsed = DslParser.Parse(source);
        var ownedCycles = new HashSet<string>(StringComparer.Ordinal);
        var ownedScenes = new HashSet<string>(StringComparer.Ordinal);
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var declaration in parsed.Document.Declarations)
            WalkDeclaration(declaration, ownedCycles, ownedScenes, referenced);

        var runtimeFiles = runtimeCSharpFiles.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var declarations = runtimeFiles
            .Where(File.Exists)
            .SelectMany(ReadRuntimeDeclarations)
            .ToArray();
        var runtimeMethods = runtimeFiles
            .Where(File.Exists)
            .SelectMany(ReadRuntimeMethods)
            .ToArray();
        if (!File.Exists(historicalSourcePath))
            throw new FileNotFoundException("Complete migration history source was not found.", historicalSourcePath);
        var historicalDeclarations = ReadRuntimeDeclarations(historicalSourcePath).ToArray();
        var historicalMethods = ReadRuntimeMethods(historicalSourcePath).ToArray();
        var segMethods = parsed.Document.Declarations.OfType<FunctionDeclaration>()
            .Select(x => new SegMethodDeclaration(x.Name, x.ReturnType ?? "void", x.Parameters, x.Span))
            .ToArray();
        var ambiguities = declarations.GroupBy(x => x.Name, StringComparer.Ordinal)
            .Where(x => x.Count() > 1)
            .Select(x => $"runtime symbol '{x.Key}' has {x.Count()} declarations: {string.Join(", ", x.Select(y => y.Path))}")
            .ToList();
        ambiguities.AddRange(declarations
            .GroupBy(x => (x.Path, x.Span.Start, x.Span.Length))
            .Where(x => x.Count() > 1)
            .Select(x => $"runtime declaration span contains multiple ID variables: {x.Key.Path}:{x.Key.Start}"));

        var owned = ownedCycles.Concat(ownedScenes).ToHashSet(StringComparer.Ordinal);
        var declared = declarations.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var remove = declarations.Where(x => owned.Contains(x.Name)).Select(x => x.Name).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var keep = declarations.Where(x => !owned.Contains(x.Name) && referenced.Contains(x.Name)).Select(x => x.Name).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        var unused = declarations.Where(x => !referenced.Contains(x.Name)).Select(x => x.Name).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();

        var historicalNames = historicalDeclarations.Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var missingReferences = referenced
            .Where(x => !owned.Contains(x) && !declared.Contains(x) && historicalNames.Contains(x))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        var missingLegacy = owned.Where(x => !historicalNames.Contains(x)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (parsed.Diagnostics.Count != 0)
            ambiguities.AddRange(parsed.Diagnostics.Select(x => $"SEG parse diagnostic {x.Id} at {x.Span.Line}:{x.Span.Column}: {x.Message}"));

        var methodsToRemove = new List<RuntimeMethodDeclaration>();
        var ambiguousMethods = new List<RuntimeMethodDeclaration>();
        var methodsWithoutMatch = new List<string>();
        foreach (var segMethod in segMethods)
        {
            var historicalMatches = historicalMethods.Where(x => MethodMatches(segMethod, x)).ToArray();
            if (historicalMatches.Length == 0)
            {
                methodsWithoutMatch.Add(MethodDisplay(segMethod));
                continue;
            }
            if (historicalMatches.Length > 1)
            {
                ambiguities.Add($"SEG method '{MethodDisplay(segMethod)}' matches multiple historical methods: {string.Join(", ", historicalMatches.Select(x => x.Path))}");
                continue;
            }

            // The historical match proves ownership. The active match is
            // optional because a previous workflow run may have removed it.
            var matches = runtimeMethods.Where(x => MethodMatches(segMethod, x)).ToArray();
            if (matches.Length == 1) methodsToRemove.Add(matches[0]);
            else if (matches.Length > 1)
            {
                ambiguousMethods.AddRange(matches);
                ambiguities.Add($"SEG method '{MethodDisplay(segMethod)}' matches multiple active runtime methods: {string.Join(", ", matches.Select(x => x.Path))}");
            }
        }
        var referencedMethods = FindActiveCSharpReferences(runtimeFiles, methodsToRemove);
        // A semantic SEG definition owns the exact matching legacy method even
        // when active C# callers still reference it.  The source generator adds
        // the generated partial World to this same compilation, so those calls
        // bind to the generated method after the legacy implementation is gone.
        // References remain in MethodsReferencedByActiveCSharp for diagnostics.
        var ownedMethodKeys = methodsToRemove.Select(MethodKey).ToHashSet(StringComparer.Ordinal);
        var keptMethods = runtimeMethods.Where(x => !ownedMethodKeys.Contains(MethodKey(x))).ToArray();
        return new(ownedCycles, ownedScenes, declarations, remove, keep, missingReferences, unused, missingLegacy, ambiguities,
            segMethods, runtimeMethods, methodsToRemove, keptMethods, referencedMethods, ambiguousMethods, methodsWithoutMatch);
    }

    public static IReadOnlyList<string> ApplyRemoval(SegOwnershipReport report)
    {
        if (!report.IsUnambiguous) throw new InvalidOperationException(string.Join(Environment.NewLine, report.Ambiguities));
        var owned = report.OwnedCycleElementIds.Concat(report.OwnedNamedCutSceneIds).ToHashSet(StringComparer.Ordinal);
        var idChanges = report.RuntimeDeclarations.Where(x => owned.Contains(x.Name));
        var allChanges = idChanges.Select(x => (x.Path, x.Span))
            .Concat(report.MethodsToRemove.Select(x => (x.Path, x.Span)))
            .GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase);
        var changed = new List<string>();
        foreach (var file in allChanges)
        {
            var text = File.ReadAllText(file.Key);
            foreach (var declaration in file
                .GroupBy(x => (x.Span.Start, x.Span.Length))
                .Select(x => x.First())
                .OrderByDescending(x => x.Span.Start))
                text = text.Remove(declaration.Span.Start, declaration.Span.Length);
            File.WriteAllText(file.Key, text, new UTF8Encoding(false));
            changed.Add(file.Key);
        }
        return changed;
    }

    private static IEnumerable<RuntimeIdDeclaration> ReadRuntimeDeclarations(string path)
    {
        var text = File.ReadAllText(path);
        var root = CSharpSyntaxTree.ParseText(text, path: path).GetRoot();
        foreach (var field in root.DescendantNodes().OfType<FieldDeclarationSyntax>())
        {
            var kind = KindOf(field.Declaration.Type.ToString());
            if (kind == null) continue;
            foreach (var variable in field.Declaration.Variables)
                yield return new(variable.Identifier.ValueText, kind.Value, path, field.Span, field.ToFullString());
        }
        foreach (var property in root.DescendantNodes().OfType<PropertyDeclarationSyntax>())
        {
            var kind = KindOf(property.Type.ToString());
            if (kind != null)
                yield return new(property.Identifier.ValueText, kind.Value, path, property.Span, property.ToFullString());
        }
    }

    private static IEnumerable<RuntimeMethodDeclaration> ReadRuntimeMethods(string path)
    {
        var root = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path).GetRoot();
        foreach (var method in root.DescendantNodes().OfType<MethodDeclarationSyntax>())
        {
            var containing = method.Ancestors().OfType<ClassDeclarationSyntax>().FirstOrDefault()?.Identifier.ValueText ?? "";
            yield return new(method.Identifier.ValueText, method.ReturnType.ToString(), method.ParameterList.Parameters.Select(x => x.Type?.ToString() ?? "").ToArray(), containing,
                method.Modifiers.Any(SyntaxKind.StaticKeyword), path, method.Span, method.ToFullString());
        }
    }

    private static bool MethodMatches(SegMethodDeclaration seg, RuntimeMethodDeclaration runtime)
        => string.Equals(seg.Name, runtime.Name, StringComparison.Ordinal)
            && TypeEquals(seg.ReturnType, runtime.ReturnType)
            && seg.Parameters.Count == runtime.ParameterTypes.Count
            && seg.Parameters.Zip(runtime.ParameterTypes, (a, b) => TypeEquals(a.Type, b)).All(x => x)
            && !runtime.IsStatic;

    private static IReadOnlyList<RuntimeMethodDeclaration> FindActiveCSharpReferences(
        IEnumerable<string> runtimeCSharpFiles,
        IReadOnlyList<RuntimeMethodDeclaration> candidates)
    {
        if (candidates.Count == 0) return Array.Empty<RuntimeMethodDeclaration>();

        var paths = runtimeCSharpFiles.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        using var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId("SegOwnership");
        var solution = workspace.CurrentSolution.AddProject(ProjectInfo.Create(
            projectId,
            VersionStamp.Create(),
            "SegOwnership",
            "SegOwnership",
            LanguageNames.CSharp,
            parseOptions: new CSharpParseOptions(LanguageVersion.Latest),
            compilationOptions: new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));

        var trustedAssemblies = (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") as string ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        foreach (var assembly in trustedAssemblies)
            solution = solution.AddMetadataReference(projectId, MetadataReference.CreateFromFile(assembly));

        foreach (var path in paths)
            solution = solution.AddDocument(DocumentId.CreateNewId(projectId), Path.GetFileName(path), File.ReadAllText(path), filePath: path);

        var project = solution.GetProject(projectId);
        if (project == null) return candidates;

        var compilation = project.GetCompilationAsync().GetAwaiter().GetResult();
        if (compilation == null) return candidates;

        var referenced = new List<RuntimeMethodDeclaration>();
        var migratedMethodKeys = candidates.Select(MethodKey).ToHashSet(StringComparer.Ordinal);
        var declarationsByPath = candidates.GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.ToArray(), StringComparer.OrdinalIgnoreCase);
        foreach (var candidate in candidates)
        {
            var tree = compilation.SyntaxTrees.FirstOrDefault(x =>
                string.Equals(Path.GetFullPath(x.FilePath ?? ""), Path.GetFullPath(candidate.Path), StringComparison.OrdinalIgnoreCase));
            if (tree == null) { referenced.Add(candidate); continue; }

            var root = tree.GetRoot();
            var methodNode = root.DescendantNodes().OfType<MethodDeclarationSyntax>()
                .FirstOrDefault(x => x.Span == candidate.Span);
            if (methodNode == null) { referenced.Add(candidate); continue; }

            var model = compilation.GetSemanticModel(tree);
            var symbol = model.GetDeclaredSymbol(methodNode);
            if (symbol == null) { referenced.Add(candidate); continue; }

            var references = SymbolFinder.FindReferencesAsync(symbol, project.Solution).GetAwaiter().GetResult();
            var hasActiveReference = references
                .SelectMany(x => x.Locations)
                .Any(x => IsActiveCSharpReference(x, candidate, declarationsByPath, migratedMethodKeys));
            if (hasActiveReference) referenced.Add(candidate);
        }
        return referenced;
    }

    private static bool IsActiveCSharpReference(
        ReferenceLocation reference,
        RuntimeMethodDeclaration candidate,
        IReadOnlyDictionary<string, RuntimeMethodDeclaration[]> declarationsByPath,
        IReadOnlySet<string> migratedMethodKeys)
    {
        var path = reference.Document.FilePath;
        if (string.IsNullOrWhiteSpace(path)) return true;
        var fullPath = Path.GetFullPath(path);
        if (string.Equals(fullPath, Path.GetFullPath(candidate.Path), StringComparison.OrdinalIgnoreCase)
            && reference.Location.SourceSpan == candidate.Span)
            return false;

        if (!declarationsByPath.TryGetValue(fullPath, out var declarations)) return true;
        var containing = declarations.FirstOrDefault(x => x.Span.Contains(reference.Location.SourceSpan.Start));
        return containing == null || !migratedMethodKeys.Contains(MethodKey(containing));
    }

    private static bool TypeEquals(string left, string right)
    {
        static string Normalize(string value)
        {
            var result = value.Replace("global::", "", StringComparison.Ordinal).Replace("System.", "", StringComparison.Ordinal).Replace(" ", "", StringComparison.Ordinal);
            return result switch { "Boolean" => "bool", "Int32" => "int", "String" => "string", "Double" => "double", "Single" => "float", "CycleElementId" => "CycleElemId", _ => result };
        }
        return string.Equals(Normalize(left), Normalize(right), StringComparison.Ordinal);
    }

    private static string MethodKey(RuntimeMethodDeclaration method) => method.ContainingType + "." + method.Name + "(" + string.Join(",", method.ParameterTypes.Select(x => x.Trim())) + "):" + method.ReturnType.Trim();
    private static string MethodDisplay(SegMethodDeclaration method) => method.Name + "(" + string.Join(", ", method.Parameters.Select(x => x.Type)) + "): " + method.ReturnType;
    private static string MethodDisplay(RuntimeMethodDeclaration method) => method.Name + "(" + string.Join(", ", method.ParameterTypes) + "): " + method.ReturnType;

    private static RuntimeIdKind? KindOf(string type) => type switch
    {
        "CycleElemId" or "CycleElementId" => RuntimeIdKind.CycleElement,
        "NamedCutSceneId" => RuntimeIdKind.NamedCutScene,
        _ => null
    };

    private static void WalkDeclaration(DslDeclaration declaration, HashSet<string> cycles, HashSet<string> scenes, HashSet<string> refs)
    {
        switch (declaration)
        {
            case CycleElementDeclaration cycle:
                cycles.Add(cycle.Id); AddExpression(cycle.Condition, refs); WalkStatements(cycle.Body, cycles, scenes, refs); break;
            case FunctionDeclaration function: WalkStatements(function.Body, cycles, scenes, refs); break;
            case HandlerDeclaration handler:
                Add(refs, handler.First); Add(refs, handler.Second); Add(refs, handler.Target);
                AddExpression(handler.Phrase, refs); AddExpression(handler.Explanation, refs); AddExpression(handler.Condition, refs);
                WalkStatements(handler.Body, cycles, scenes, refs); break;
            case BeforeRoomChangeDeclaration before: WalkStatements(before.Body, cycles, scenes, refs); break;
            case AfterActionExecutedDeclaration after: WalkStatements(after.Body, cycles, scenes, refs); break;
            case NextCycleDeclaration next: AddExpression(next.Cycle, refs); break;
            case StateDeclaration state: Add(refs, state.Name); AddExpression(state.Initializer, refs); break;
        }
    }

    private static void WalkStatements(IEnumerable<DslStatement> statements, HashSet<string> cycles, HashSet<string> scenes, HashSet<string> refs)
    {
        foreach (var statement in statements)
        {
            switch (statement)
            {
                case AddCycleElementStatement add: cycles.Add(add.Id); Add(refs, add.Cycle); AddExpression(add.Condition, refs); WalkStatements(add.Body, cycles, scenes, refs); break;
                case NamedCutsceneStatement scene: scenes.Add(scene.Id); AddExpression(scene.Title, refs); foreach (var arg in scene.Arguments) AddExpression(arg, refs); WalkStatements(scene.Body, cycles, scenes, refs); break;
                case IfStatement conditional:
                    foreach (var branch in conditional.Branches) { AddExpression(branch.Condition, refs); WalkStatements(branch.Body, cycles, scenes, refs); }
                    if (conditional.ElseBody != null) WalkStatements(conditional.ElseBody, cycles, scenes, refs); break;
                case VariableDeclaration variable: Add(refs, variable.Name); AddExpression(variable.Initializer, refs); break;
                case AssignmentStatement assignment: Add(refs, assignment.Name); Add(refs, assignment.MemberName); AddExpression(assignment.Receiver, refs); AddExpression(assignment.Value, refs); break;
                case IncrementStatement increment: Add(refs, increment.Name); break;
                case CallStatement call: AddExpression(call.Expression, refs); break;
                case ReturnStatement ret: AddExpression(ret.Expression, refs); break;
                case DialogueStatement dialogue: Add(refs, dialogue.Character); AddExpression(dialogue.Text, refs); AddExpression(dialogue.Insta, refs); break;
                case NarStatement nar: AddExpression(nar.Text, refs); break;
                case NarRoomStatement narRoom: AddExpression(narRoom.Text, refs); break;
                case NarImgStatement image: AddExpression(image.ImagePath, refs); AddExpression(image.Text, refs); break;
                case NextCycleStatement next: AddExpression(next.Cycle, refs); break;
                case MarkHappenedOnceStatement once: AddExpression(once.Target, refs); break;
                case MarkHappenedStatement happened: AddExpression(happened.Target, refs); break;
                case TextInputStatement input: AddExpression(input.TextInput, refs); break;
            }
        }
    }

    private static void AddExpression(DslExpression? expression, HashSet<string> refs)
    {
        switch (expression)
        {
            case IdentifierExpression id: refs.Add(id.Name); break;
            case FunctionReferenceExpression function: refs.Add(function.Name); break;
            case MemberAccessExpression member: AddExpression(member.Receiver, refs); refs.Add(member.MemberName); break;
            case CallExpression call: refs.Add(call.Name); AddExpression(call.Receiver, refs); foreach (var arg in call.Arguments) AddExpression(arg.Expression, refs); break;
            case ParenthesizedExpression parenthesized: AddExpression(parenthesized.Expression, refs); break;
            case UnaryExpression unary: AddExpression(unary.Operand, refs); break;
            case BinaryExpression binary: AddExpression(binary.Left, refs); AddExpression(binary.Right, refs); break;
            case ConditionalExpression conditional: AddExpression(conditional.Condition, refs); AddExpression(conditional.WhenTrue, refs); AddExpression(conditional.WhenFalse, refs); break;
            case ListExpression list: foreach (var item in list.Elements) AddExpression(item, refs); break;
            case ExistsExpression exists: AddExpression(exists.Collection, refs); AddExpression(exists.Predicate, refs); refs.Add(exists.ItemName); break;
        }
    }

    private static void Add(HashSet<string> set, string? value) { if (!string.IsNullOrWhiteSpace(value)) set.Add(value); }
}
