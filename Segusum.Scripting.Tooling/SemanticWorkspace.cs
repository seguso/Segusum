using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Segusum.Scripting.Core;
using Segusum.Scripting.Semantics;

namespace Segusum.Scripting.Tooling;

public sealed record SemanticLocation(string Path, SourceSpan Span, string Kind)
{
    public string Language => Kind.StartsWith("csharp", StringComparison.Ordinal) ? "CSharp" : "Segusum";
}
public sealed record SemanticDefinition(string DisplayName, SemanticLocation Location, ISymbol? CSharpSymbol, DslSymbolIdentity? DslSymbol);
public sealed record SemanticReference(string DisplayName, SemanticLocation Location, ISymbol? CSharpSymbol, DslSymbolIdentity? DslSymbol);
public sealed record WorkspaceTextEdit(string Path, SourceSpan Span, string NewText);
public sealed record RenameResult(IReadOnlyList<WorkspaceTextEdit> Edits, IReadOnlyList<DslDiagnostic> Diagnostics)
{
    public bool Succeeded => Diagnostics.Count == 0;
}

/// <summary>Semantic services shared with the generator binder for a set of .seg files.</summary>
public sealed class DslSemanticWorkspace
{
    private readonly ICSharpWorkspaceContext workspaceContext;
    private Compilation compilation => workspaceContext.Compilation;
    private readonly INamedTypeSymbol world;
    private readonly IReadOnlyList<DslSource> sources;
    private readonly BoundModel model;
    private readonly DslBinder binder;
    private readonly Dictionary<string, DslSource> documents;
    private readonly List<DslDiagnostic> diagnostics = new();
    private Solution roslynSolution => workspaceContext.Solution;

    public DslSemanticWorkspace(Compilation compilation, INamedTypeSymbol world, IEnumerable<DslSource> sources)
        : this(new AdhocCSharpWorkspaceContext(compilation), world, sources)
    {
    }

    public DslSemanticWorkspace(ICSharpWorkspaceContext workspaceContext, INamedTypeSymbol world, IEnumerable<DslSource> sources)
    {
        this.workspaceContext = workspaceContext;
        this.world = world;
        this.sources = sources.ToArray();
        documents = this.sources.ToDictionary(x => x.Path, x => x, StringComparer.OrdinalIgnoreCase);
        foreach (var source in this.sources)
        {
            var parsed = DslParser.Parse(source);
            diagnostics.AddRange(parsed.Diagnostics);
        }
        var declarations = documents.Values.Select(x => DslParser.Parse(x).Document).SelectMany(x => x.Declarations).ToArray();
        binder = new DslBinder(compilation, world, diagnostics.Add);
        binder.Bind(declarations);
        model = binder.Model;
    }

    public IReadOnlyList<DslDiagnostic> Diagnostics => diagnostics;
    public IReadOnlyList<SemanticReference> FindReferences(string path, int line, int column)
        => FindReferencesAsync(path, line, column, CancellationToken.None).GetAwaiter().GetResult();

    public async Task<IReadOnlyList<SemanticReference>> FindReferencesAsync(string path, int line, int column, CancellationToken cancellationToken = default)
    {
        var definition = GetDefinition(path, line, column);
        if (definition == null) return Array.Empty<SemanticReference>();
        var dsl = model.ReferencesByNode
            .Where(x => SameSymbol(x.CSharpSymbol, definition.CSharpSymbol) || SameDsl(x.DslSymbol, definition.DslSymbol))
            .Select(x => new SemanticReference(definition.DisplayName, new SemanticLocation(x.Path, NormalizeDslSpan(x), x.ReferenceKind), x.CSharpSymbol, x.DslSymbol))
            .ToList();
        if (definition.CSharpSymbol != null)
        {
            var workspaceSymbol = ResolveWorkspaceSymbol(definition.CSharpSymbol);
            if (workspaceSymbol != null)
            {
                var references = await SymbolFinder.FindReferencesAsync(workspaceSymbol, roslynSolution, cancellationToken: cancellationToken).ConfigureAwait(false);
                foreach (var reference in references.SelectMany(x => x.Locations))
                {
                    var location = reference.Location;
                    if (location.SourceTree != null && SegusumGeneratedSource.IsGenerated(location.SourceTree)) continue;
                    var sourcePath = location.SourceTree?.FilePath ?? "";
                    var semanticReference = new SemanticReference(definition.DisplayName, new SemanticLocation(sourcePath, FromLocation(location), "csharp-reference"), definition.CSharpSymbol, null);
                    if (!dsl.Any(x => x.Location.Path == sourcePath && x.Location.Span.Start == semanticReference.Location.Span.Start)) dsl.Add(semanticReference);
                }
            }
            foreach (var location in definition.CSharpSymbol.Locations.Where(x => x.IsInSource))
            {
                var reference = new SemanticReference(definition.DisplayName, new SemanticLocation(location.SourceTree!.FilePath, FromLocation(location), "csharp-definition"), definition.CSharpSymbol, null);
                if (!dsl.Any(x => x.Location.Path == reference.Location.Path && x.Location.Span.Start == reference.Location.Span.Start)) dsl.Add(reference);
            }
        }
        return dsl.OrderBy(x => x.Location.Path, StringComparer.Ordinal).ThenBy(x => x.Location.Span.Start).ToArray();
    }

    public SemanticDefinition? GetDefinition(string path, int line, int column)
    {
        var sourceReference = model.SemanticReferenceList.FirstOrDefault(x => string.Equals(x.Path, path, StringComparison.OrdinalIgnoreCase) && x.Span.Line == line && column >= x.Span.Column && column <= x.Span.Column + Math.Max(1, x.Span.Length));
        if (sourceReference?.CSharpSymbol != null)
        {
            var location = sourceReference.CSharpSymbol.Locations.FirstOrDefault() ?? Location.None;
            return new SemanticDefinition(sourceReference.CSharpSymbol.Name, ToLocation(location), sourceReference.CSharpSymbol, null);
        }
        if (sourceReference?.DslSymbol != null && model.DslDefinitions.TryGetValue(sourceReference.DslSymbol, out var dslSpan))
            return new SemanticDefinition(sourceReference.DslSymbol.Name, new SemanticLocation(dslSpan.Path, dslSpan, "dsl-definition"), null, sourceReference.DslSymbol);

        var tree = compilation.SyntaxTrees.FirstOrDefault(x => string.Equals(x.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (tree == null) return null;
        var position = GetPosition(tree, line, column);
        var semanticModel = compilation.GetSemanticModel(tree);
        var node = tree.GetRoot().FindToken(position).Parent!;
        var symbol = semanticModel.GetSymbolInfo(node).Symbol ?? GetDeclaredSymbol(semanticModel, node);
        return symbol == null ? null : new SemanticDefinition(symbol.Name, ToLocation(symbol.Locations.FirstOrDefault() ?? Location.None), symbol, null);
    }

    public RenameResult RenameSymbol(string path, int line, int column, string newName)
    {
        if (string.IsNullOrWhiteSpace(newName) || !Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(newName))
            return new RenameResult(Array.Empty<WorkspaceTextEdit>(), new[] { new DslDiagnostic("SEGTOOL001", "The new name is not a valid identifier.", new SourceSpan(path, 0, 0, line, column)) });
        var definition = GetDefinition(path, line, column);
        if (definition == null)
            return new RenameResult(Array.Empty<WorkspaceTextEdit>(), new[] { new DslDiagnostic("SEGTOOL002", "No symbol found at the requested location.", new SourceSpan(path, 0, 0, line, column)) });
        if (definition.CSharpSymbol?.ContainingType is INamedTypeSymbol containingType &&
            containingType.GetMembers(newName).Any(x => !SymbolEqualityComparer.Default.Equals(x, definition.CSharpSymbol)))
            return new RenameResult(Array.Empty<WorkspaceTextEdit>(), new[] { new DslDiagnostic("SEGTOOL005", $"The rename collides with an existing C# member '{newName}'.", definition.Location.Span) });
        if (definition.DslSymbol != null && model.DslDefinitions.Keys.Any(x => !Equals(x, definition.DslSymbol) && string.Equals(x.Name, newName, StringComparison.Ordinal)))
            return new RenameResult(Array.Empty<WorkspaceTextEdit>(), new[] { new DslDiagnostic("SEGTOOL006", $"The rename collides with an existing DSL symbol '{newName}'.", definition.Location.Span) });
        var references = FindReferences(path, line, column);
        var edits = new List<WorkspaceTextEdit>();
        Solution? renamedSolution = null;
        if (definition.CSharpSymbol != null)
        {
            var workspaceSymbol = ResolveWorkspaceSymbol(definition.CSharpSymbol);
            if (workspaceSymbol == null)
                return new RenameResult(Array.Empty<WorkspaceTextEdit>(), new[] { new DslDiagnostic("SEGTOOL003", "The C# symbol is not available in the Roslyn workspace.", new SourceSpan(path, 0, 0, line, column)) });
            renamedSolution = Renamer.RenameSymbolAsync(roslynSolution, workspaceSymbol, newName, null, CancellationToken.None).GetAwaiter().GetResult();
            foreach (var project in roslynSolution.Projects)
                foreach (var originalDocument in project.Documents)
                {
                    var originalRoot = originalDocument.GetSyntaxRootAsync().GetAwaiter().GetResult();
                    if (originalRoot != null && SegusumGeneratedSource.IsGenerated(originalRoot.SyntaxTree)) continue;
                    var renamedDocument = renamedSolution.GetDocument(originalDocument.Id);
                    if (renamedDocument == null) continue;
                    foreach (var change in renamedDocument.GetTextChangesAsync(originalDocument).GetAwaiter().GetResult())
                    {
                        var originalText = originalDocument.GetTextAsync().GetAwaiter().GetResult().ToString();
                        edits.Add(new WorkspaceTextEdit(originalDocument.FilePath ?? "", SourceSpan.From(originalDocument.FilePath ?? "", originalText, change.Span.Start, change.Span.Length), change.NewText ?? ""));
                    }
                }
        }
        else if (definition.DslSymbol != null && model.DslDefinitions.TryGetValue(definition.DslSymbol, out var declaration))
            edits.Add(new WorkspaceTextEdit(declaration.Path, DslNameLocation(definition.DslSymbol.Name, declaration), newName));
        foreach (var reference in references.Where(x => x.Location.Kind == "name"))
            edits.Add(new WorkspaceTextEdit(reference.Location.Path, reference.Location.Span, newName));
        var finalEdits = edits.DistinctBy(x => (x.Path, x.Span.Start)).ToArray();
        var validation = ValidateRename(definition, finalEdits, renamedSolution);
        return validation.Count == 0
            ? new RenameResult(finalEdits, Array.Empty<DslDiagnostic>())
            : new RenameResult(Array.Empty<WorkspaceTextEdit>(), validation);
    }

    public IReadOnlyList<string> GetCompletions(string path, int line, int column)
    {
        if (!documents.TryGetValue(path, out var source)) return Array.Empty<string>();
        var lineText = source.Text.Split('\n').ElementAtOrDefault(Math.Max(0, line - 1))?.TrimEnd('\r') ?? "";
        var cursor = Math.Clamp(column - 1, 0, lineText.Length);
        var before = lineText[..cursor];
        var prefix = ReadIdentifierBackward(before, before.Length);
        var dot = before.LastIndexOf('.');
        if (dot >= 0 && dot == before.Length - prefix.Length - 1)
        {
            var receiverName = ReadIdentifierBackward(before, dot);
            var receiverType = binder.ResolveCompletionType(receiverName);
            if (receiverType != null)
                return binder.GetAccessibleMembers(receiverType).Select(x => x.Name).Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
            return Array.Empty<string>();
        }
        return model.DslSymbolsByName.Keys.Concat(binder.GetAccessibleWorldMembers().Select(x => x.Name))
            .Where(x => x.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)).Distinct(StringComparer.Ordinal).OrderBy(x => x, StringComparer.Ordinal).ToArray();
    }

    private static string ReadIdentifierBackward(string text, int end)
    {
        var i = Math.Min(end, text.Length);
        while (i > 0 && (char.IsLetterOrDigit(text[i - 1]) || text[i - 1] is '_' or '-')) i--;
        return text[i..Math.Min(end, text.Length)];
    }

    private static bool SameSymbol(ISymbol? left, ISymbol? right) => left != null && right != null && SymbolEqualityComparer.Default.Equals(left, right);
    private static bool SameDsl(DslSymbolIdentity? left, DslSymbolIdentity? right) => left != null && right != null && left.Equals(right);
    private SourceSpan NormalizeDslSpan(DslSemanticReference reference)
    {
        if (!documents.TryGetValue(reference.Path, out var document)) return reference.Span;
        var name = reference.CSharpSymbol?.Name ?? reference.DslSymbol?.Name;
        if (string.IsNullOrEmpty(name)) return reference.Span;
        if (reference.Span.Start >= 0 && reference.Span.Start + reference.Span.Length <= document.Text.Length &&
            string.Equals(document.Text.Substring(reference.Span.Start, reference.Span.Length), name, StringComparison.Ordinal)) return reference.Span;
        var lineStart = reference.Span.Start;
        while (lineStart > 0 && document.Text[lineStart - 1] != '\n') lineStart--;
        var lineEnd = document.Text.IndexOf('\n', lineStart);
        if (lineEnd < 0) lineEnd = document.Text.Length;
        var start = document.Text.IndexOf(name, Math.Max(lineStart, Math.Min(reference.Span.Start, lineEnd)), lineEnd - Math.Max(lineStart, Math.Min(reference.Span.Start, lineEnd)), StringComparison.Ordinal);
        return start < 0 ? reference.Span : SourceSpan.From(reference.Path, document.Text, start, name.Length);
    }
    private static int GetPosition(SyntaxTree tree, int line, int column) => tree.GetText().Lines[Math.Max(0, line - 1)].Start + Math.Max(0, column - 1);
    private static SemanticLocation ToLocation(Location location)
    {
        if (location == Location.None || location.SourceTree == null)
            return new SemanticLocation("", new SourceSpan("", 0, 0, 1, 1), "csharp-definition");
        var path = location.SourceTree.FilePath ?? "";
        return new SemanticLocation(path, SourceSpan.From(path, location.SourceTree.GetText().ToString(), location.SourceSpan.Start, location.SourceSpan.Length), "csharp-definition");
    }

    private ISymbol? ResolveWorkspaceSymbol(ISymbol symbol)
    {
        var location = symbol.Locations.FirstOrDefault(x => x.IsInSource);
        if (location?.SourceTree == null) return null;
        var document = roslynSolution.GetDocumentIdsWithFilePath(location.SourceTree.FilePath).Select(id => roslynSolution.GetDocument(id)).FirstOrDefault(x => x != null);
        if (document == null) return null;
        var syntax = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
        var node = syntax?.FindNode(location.SourceSpan, getInnermostNodeForTie: true);
        var semanticModel = document.GetSemanticModelAsync().GetAwaiter().GetResult();
        return node == null || semanticModel == null ? null : semanticModel.GetSymbolInfo(node).Symbol ?? GetDeclaredSymbol(semanticModel, node);
    }

    private IReadOnlyList<DslDiagnostic> ValidateRename(SemanticDefinition definition, IReadOnlyList<WorkspaceTextEdit> edits, Solution? renamedSolution)
    {
        if (renamedSolution != null)
        {
            var project = renamedSolution.Projects.FirstOrDefault();
            var renamedCompilation = project?.GetCompilationAsync().GetAwaiter().GetResult();
            if (renamedCompilation == null)
                return new[] { new DslDiagnostic("SEGTOOL004", "The renamed C# solution could not be compiled.", definition.Location.Span) };
            var newErrors = renamedCompilation.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error).ToArray();
            var oldErrorCounts = compilation.GetDiagnostics().Where(x => x.Severity == DiagnosticSeverity.Error).GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
            var introducedError = newErrors.GroupBy(x => x.Id).FirstOrDefault(group => !oldErrorCounts.TryGetValue(group.Key, out var count) || group.Count() > count);
            if (introducedError != null)
                return new[] { new DslDiagnostic("SEGTOOL004", $"The rename introduces C# compilation errors: {introducedError.First().Id} {introducedError.First().GetMessage()}", definition.Location.Span) };
            var worldName = world.ToDisplayString();
            var renamedWorld = renamedCompilation.GetTypeByMetadataName(worldName);
            if (renamedWorld == null)
                return new[] { new DslDiagnostic("SEGTOOL004", "The renamed C# World could not be resolved.", definition.Location.Span) };
            return ValidateDslSources(renamedCompilation, renamedWorld, edits);
        }

        return ValidateDslSources(compilation, world, edits);
    }

    private IReadOnlyList<DslDiagnostic> ValidateDslSources(Compilation targetCompilation, INamedTypeSymbol targetWorld, IReadOnlyList<WorkspaceTextEdit> edits)
    {
        var updatedSources = sources.Select(source => new DslSource(source.Path, ApplyEdits(source.Text, edits.Where(x => string.Equals(x.Path, source.Path, StringComparison.OrdinalIgnoreCase))))).ToArray();
        var parseResults = updatedSources.Select(DslParser.Parse).ToArray();
        var result = parseResults.SelectMany(x => x.Diagnostics).ToList();
        if (result.Count != 0) return result;
        var declarations = parseResults.Select(x => x.Document).SelectMany(x => x.Declarations).ToArray();
        var binder = new DslBinder(targetCompilation, targetWorld, result.Add);
        binder.Bind(declarations);
        return result;
    }

    private static string ApplyEdits(string text, IEnumerable<WorkspaceTextEdit> edits)
    {
        var updated = text;
        foreach (var edit in edits.OrderByDescending(x => x.Span.Start))
            updated = updated.Remove(edit.Span.Start, edit.Span.Length).Insert(edit.Span.Start, edit.NewText);
        return updated;
    }
    private static SourceSpan FromLocation(Location location) => SourceSpan.From(location.SourceTree!.FilePath, location.SourceTree.GetText().ToString(), location.SourceSpan.Start, location.SourceSpan.Length);
    private static ISymbol? GetDeclaredSymbol(SemanticModel model, SyntaxNode node)
    {
        for (var current = node; current != null; current = current.Parent)
        {
            var symbol = current switch
            {
                VariableDeclaratorSyntax variable => model.GetDeclaredSymbol(variable),
                MethodDeclarationSyntax method => model.GetDeclaredSymbol(method),
                PropertyDeclarationSyntax property => model.GetDeclaredSymbol(property),
                ClassDeclarationSyntax type => model.GetDeclaredSymbol(type),
                _ => null
            };
            if (symbol != null) return symbol;
        }
        return null;
    }
    private SourceSpan DslNameLocation(string name, SourceSpan declaration)
    {
        if (!documents.TryGetValue(declaration.Path, out var document)) return declaration;
        var start = document.Text.IndexOf(name, Math.Max(0, declaration.Start), StringComparison.Ordinal);
        return start < 0 ? declaration : SourceSpan.From(declaration.Path, document.Text, start, name.Length);
    }
}
