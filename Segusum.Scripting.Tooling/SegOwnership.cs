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
    public static SegOwnershipReport Analyze(string segPath, IEnumerable<string> runtimeCSharpFiles, string? legacySymbolsPath = null)
    {
        var source = new DslSource(segPath, File.ReadAllText(segPath));
        var parsed = DslParser.Parse(source);
        var ownedCycles = new HashSet<string>(StringComparer.Ordinal);
        var ownedScenes = new HashSet<string>(StringComparer.Ordinal);
        var referenced = new HashSet<string>(StringComparer.Ordinal);
        foreach (var declaration in parsed.Document.Declarations)
            WalkDeclaration(declaration, ownedCycles, ownedScenes, referenced);

        var declarations = runtimeCSharpFiles
            .Where(File.Exists)
            .SelectMany(ReadRuntimeDeclarations)
            .ToArray();
        var runtimeMethods = runtimeCSharpFiles
            .Where(File.Exists)
            .SelectMany(ReadRuntimeMethods)
            .ToArray();
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

        var legacy = legacySymbolsPath != null && File.Exists(legacySymbolsPath)
            ? ReadRuntimeDeclarations(legacySymbolsPath).Select(x => x.Name).ToHashSet(StringComparer.Ordinal)
            : new HashSet<string>(StringComparer.Ordinal);
        var missingReferences = referenced
            .Where(x => !owned.Contains(x) && !declared.Contains(x) && legacy.Contains(x))
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        var missingLegacy = owned.Where(x => !legacy.Contains(x)).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (parsed.Diagnostics.Count != 0)
            ambiguities.AddRange(parsed.Diagnostics.Select(x => $"SEG parse diagnostic {x.Id} at {x.Span.Line}:{x.Span.Column}: {x.Message}"));

        var methodsToRemove = new List<RuntimeMethodDeclaration>();
        var ambiguousMethods = new List<RuntimeMethodDeclaration>();
        var methodsWithoutMatch = new List<string>();
        foreach (var segMethod in segMethods)
        {
            var matches = runtimeMethods.Where(x => MethodMatches(segMethod, x)).ToArray();
            if (matches.Length == 1) methodsToRemove.Add(matches[0]);
            else if (matches.Length == 0) methodsWithoutMatch.Add(MethodDisplay(segMethod));
            else
            {
                ambiguousMethods.AddRange(matches);
                ambiguities.Add($"SEG method '{MethodDisplay(segMethod)}' matches multiple runtime methods: {string.Join(", ", matches.Select(x => x.Path))}");
            }
        }
        var referencedMethods = FindActiveCSharpReferences(runtimeCSharpFiles, methodsToRemove);
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
        var changes = report.RuntimeDeclarations.Where(x => owned.Contains(x.Name)).GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase);
        var changed = new List<string>();
        foreach (var file in changes)
        {
            var text = File.ReadAllText(file.Key);
            foreach (var declaration in file.OrderByDescending(x => x.Span.Start))
                text = text.Remove(declaration.Span.Start, declaration.Span.Length);
            File.WriteAllText(file.Key, text, new UTF8Encoding(false));
            changed.Add(file.Key);
        }
        var methodChanges = report.MethodsToRemove.GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase);
        foreach (var file in methodChanges)
        {
            var text = File.ReadAllText(file.Key);
            foreach (var method in file.OrderByDescending(x => x.Span.Start))
                text = text.Remove(method.Span.Start, method.Span.Length);
            File.WriteAllText(file.Key, text, new UTF8Encoding(false));
            if (!changed.Contains(file.Key, StringComparer.OrdinalIgnoreCase)) changed.Add(file.Key);
        }
        return changed;
    }

    public static IReadOnlyList<string> EnsureLegacyMethods(SegOwnershipReport report, string legacyMethodsPath)
    {
        if (!report.IsUnambiguous) throw new InvalidOperationException(string.Join(Environment.NewLine, report.Ambiguities));
        var existing = File.Exists(legacyMethodsPath) ? File.ReadAllText(legacyMethodsPath) : "";
        var additions = report.MethodsToRemove.Where(x => !existing.Contains(x.Text, StringComparison.Ordinal)).ToArray();
        if (additions.Length == 0) return Array.Empty<string>();
        var sb = new StringBuilder();
        if (string.IsNullOrEmpty(existing))
        {
            sb.AppendLine("// Archived methods superseded by SEG OnRoomChanged; not compiled.");
            sb.AppendLine("namespace WebApiLitGir { public partial class World : WorldBase {");
            sb.AppendLine(existing);
        }
        else sb.Append(existing.TrimEnd()).AppendLine();
        foreach (var method in additions) sb.AppendLine(method.Text.Trim()).AppendLine();
        if (string.IsNullOrEmpty(existing)) sb.AppendLine("} }");
        File.WriteAllText(legacyMethodsPath, sb.ToString(), new UTF8Encoding(false));
        return additions.Select(x => MethodDisplay(x)).ToArray();
    }

    public static string? EnsureReferenceOnlyBridge(SegOwnershipReport report, string legacySymbolsPath, string bridgePath)
    {
        if (!report.IsUnambiguous) throw new InvalidOperationException(string.Join(Environment.NewLine, report.Ambiguities));
        var legacy = ReadRuntimeDeclarations(legacySymbolsPath).ToDictionary(x => x.Name, StringComparer.Ordinal);
        var missing = report.MissingReferenceOnlyRuntimeSymbols.Where(legacy.ContainsKey).OrderBy(x => x, StringComparer.Ordinal).ToArray();
        if (missing.Length == 0)
        {
            if (File.Exists(bridgePath)) File.Delete(bridgePath);
            return null;
        }
        var sb = new StringBuilder();
        sb.AppendLine("using Seg;");
        sb.AppendLine("namespace WebApiLitGir;");
        sb.AppendLine("public partial class World : WorldBase");
        sb.AppendLine("{");
        foreach (var name in missing) sb.Append("    ").AppendLine(legacy[name].Text.Trim());
        sb.AppendLine("}");
        File.WriteAllText(bridgePath, sb.ToString(), new UTF8Encoding(false));
        return bridgePath;
    }

    public static IReadOnlyList<string> EnsureLegacySymbols(SegOwnershipReport report, string legacySymbolsPath)
    {
        if (!report.IsUnambiguous) throw new InvalidOperationException(string.Join(Environment.NewLine, report.Ambiguities));
        var existing = ReadRuntimeDeclarations(legacySymbolsPath).Select(x => x.Name).ToHashSet(StringComparer.Ordinal);
        var additions = report.RuntimeDeclarations
            .Where(x => report.OwnedCycleElementIds.Contains(x.Name) || report.OwnedNamedCutSceneIds.Contains(x.Name))
            .Where(x => !existing.Contains(x.Name))
            .GroupBy(x => x.Name, StringComparer.Ordinal)
            .Select(x => x.First())
            .OrderBy(x => x.Name, StringComparer.Ordinal)
            .ToArray();
        if (additions.Length == 0) return Array.Empty<string>();
        var text = File.ReadAllText(legacySymbolsPath);
        var close = text.LastIndexOf('}');
        if (close < 0) throw new InvalidOperationException($"Legacy symbol archive has no closing brace: {legacySymbolsPath}");
        var block = new StringBuilder().AppendLine();
        foreach (var declaration in additions)
            block.AppendLine("        " + declaration.Text.Trim().Replace("\r\n", "\n").Replace("\n", "\n        "));
        File.WriteAllText(legacySymbolsPath, text.Insert(close, block.ToString()), new UTF8Encoding(false));
        return additions.Select(x => x.Name).ToArray();
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
