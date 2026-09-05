using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
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
    private readonly string[] completionNames;
    private readonly Dictionary<string, DslSemanticReference[]> referencesByPath;
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
        completionNames = model.DslSymbolsByName.Keys
            .Concat(binder.GetAccessibleWorldMembers().Select(x => x.Name))
            .Distinct(StringComparer.Ordinal)
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToArray();
        referencesByPath = model.SemanticReferenceList
            .GroupBy(x => x.Path, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.OrderBy(r => r.Span.Start).ToArray(), StringComparer.OrdinalIgnoreCase);
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
        var sourceReference = FindReference(path, line, column);
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
        var renameTimer = Stopwatch.StartNew();
        var definitionTimer = Stopwatch.StartNew();
        if (string.IsNullOrWhiteSpace(newName) || !Microsoft.CodeAnalysis.CSharp.SyntaxFacts.IsValidIdentifier(newName))
            return new RenameResult(Array.Empty<WorkspaceTextEdit>(), new[] { new DslDiagnostic("SEGTOOL001", "The new name is not a valid identifier.", new SourceSpan(path, 0, 0, line, column)) });
        var definition = GetDefinition(path, line, column);
        definitionTimer.Stop();
        if (definition == null)
            return new RenameResult(Array.Empty<WorkspaceTextEdit>(), new[] { new DslDiagnostic("SEGTOOL002", "No symbol found at the requested location.", new SourceSpan(path, 0, 0, line, column)) });
        if (definition.CSharpSymbol?.ContainingType is INamedTypeSymbol containingType &&
            containingType.GetMembers(newName).Any(x => !SymbolEqualityComparer.Default.Equals(x, definition.CSharpSymbol)))
            return new RenameResult(Array.Empty<WorkspaceTextEdit>(), new[] { new DslDiagnostic("SEGTOOL005", $"The rename collides with an existing C# member '{newName}'.", definition.Location.Span) });
        if (definition.DslSymbol != null && model.DslDefinitions.Keys.Any(x => !Equals(x, definition.DslSymbol) && string.Equals(x.Name, newName, StringComparison.Ordinal)))
            return new RenameResult(Array.Empty<WorkspaceTextEdit>(), new[] { new DslDiagnostic("SEGTOOL006", $"The rename collides with an existing DSL symbol '{newName}'.", definition.Location.Span) });
        var dslReferenceTimer = Stopwatch.StartNew();
        var references = GetDslReferences(definition);
        dslReferenceTimer.Stop();
        var edits = new List<WorkspaceTextEdit>();
        Solution? renamedSolution = null;
        if (definition.CSharpSymbol != null)
        {
            var workspaceSymbol = ResolveWorkspaceSymbol(definition.CSharpSymbol);
            if (workspaceSymbol == null)
                return new RenameResult(Array.Empty<WorkspaceTextEdit>(), new[] { new DslDiagnostic("SEGTOOL003", "The C# symbol is not available in the Roslyn workspace.", new SourceSpan(path, 0, 0, line, column)) });
            var renamerTimer = Stopwatch.StartNew();
            renamedSolution = Renamer.RenameSymbolAsync(roslynSolution, workspaceSymbol, newName, null, CancellationToken.None).GetAwaiter().GetResult();
            renamerTimer.Stop();
            var extractionTimer = Stopwatch.StartNew();
            foreach (var project in roslynSolution.Projects)
                foreach (var originalDocument in project.Documents)
                {
                    var originalRoot = originalDocument.GetSyntaxRootAsync().GetAwaiter().GetResult();
                    if (originalRoot != null && SegusumGeneratedSource.IsGenerated(originalRoot.SyntaxTree)) continue;
                    var renamedDocument = renamedSolution.GetDocument(originalDocument.Id);
                    if (renamedDocument == null) continue;
                    var originalSourceText = originalDocument.GetTextAsync().GetAwaiter().GetResult();
                    var renamedSourceText = renamedDocument.GetTextAsync().GetAwaiter().GetResult();
                    var changes = renamedDocument.GetTextChangesAsync(originalDocument).GetAwaiter().GetResult().ToArray();
                    var projected = originalSourceText.WithChanges(changes);
                    if (projected.ContentEquals(renamedSourceText) &&
                        HasRenamedTokenAtAuthorSpan(originalDocument, renamedSourceText, workspaceSymbol, newName) &&
                        changes.All(change => IsTokenBoundary(originalSourceText.ToString(), change.Span)))
                    {
                        foreach (var change in changes)
                        {
                            var originalText = originalSourceText.ToString();
                            edits.Add(new WorkspaceTextEdit(originalDocument.FilePath ?? "", SourceSpan.From(originalDocument.FilePath ?? "", originalText, change.Span.Start, change.Span.Length), change.NewText ?? ""));
                        }
                    }
                    else
                    {
                        // Some large documents/snapshot combinations can produce
                        // a TextChange set whose spans do not project to the
                        // renamed document. Keep Roslyn as the authority for the
                        // symbol, then obtain exact token spans from the original
                        // semantic model rather than falling back to text search.
                        AddRoslynChangeTokenEdits(edits, originalDocument, changes, newName);
                    }
                }
            extractionTimer.Stop();
            Console.Error.WriteLine($"rename symbol={definition.DisplayName} definition={definitionTimer.Elapsed.TotalMilliseconds:0}ms dslLookup={dslReferenceTimer.Elapsed.TotalMilliseconds:0}ms renamer={renamerTimer.Elapsed.TotalMilliseconds:0}ms textChanges={extractionTimer.Elapsed.TotalMilliseconds:0}ms total={renameTimer.Elapsed.TotalMilliseconds:0}ms");
        }
        else if (definition.DslSymbol != null && model.DslDefinitions.TryGetValue(definition.DslSymbol, out var declaration))
            edits.Add(new WorkspaceTextEdit(declaration.Path, DslNameLocation(definition.DslSymbol.Name, declaration), newName));
        // DSL references are token-level and can be speakers, operands or
        // invocation names. For a C# symbol, do not restrict this to the
        // historical "name" kind: every Segusum location bound to the same
        // Roslyn symbol must be updated.
        foreach (var reference in references.Where(x => x.Location.Language == "Segusum" &&
            ((definition.CSharpSymbol != null && SameSymbol(x.CSharpSymbol, definition.CSharpSymbol)) ||
             (definition.DslSymbol != null && x.DslSymbol?.Equals(definition.DslSymbol) == true))))
            edits.Add(new WorkspaceTextEdit(reference.Location.Path, reference.Location.Span, newName));
        var finalEdits = edits.DistinctBy(x => (x.Path, x.Span.Start)).ToArray();
        var validationTimer = Stopwatch.StartNew();
        var validation = ValidateRename(definition, finalEdits, renamedSolution, newName);
        validationTimer.Stop();
        Console.Error.WriteLine($"rename validation symbol={definition.DisplayName} finalValidation={validationTimer.Elapsed.TotalMilliseconds:0}ms total={renameTimer.Elapsed.TotalMilliseconds:0}ms succeeded={validation.Count == 0}");
        return validation.Count == 0
            ? new RenameResult(finalEdits, Array.Empty<DslDiagnostic>())
            : new RenameResult(Array.Empty<WorkspaceTextEdit>(), validation);
    }

    private IReadOnlyList<SemanticReference> GetDslReferences(SemanticDefinition definition)
        => model.ReferencesByNode
            .Where(x => SameSymbol(x.CSharpSymbol, definition.CSharpSymbol) || SameDsl(x.DslSymbol, definition.DslSymbol))
            .Select(x => new SemanticReference(definition.DisplayName, new SemanticLocation(x.Path, NormalizeDslSpan(x), x.ReferenceKind), x.CSharpSymbol, x.DslSymbol))
            .ToArray();

    public IReadOnlyList<string> GetCompletions(string path, int line, int column, string? currentText = null)
    {
        if (!documents.TryGetValue(path, out var source)) return Array.Empty<string>();
        var text = currentText ?? source.Text;
        var lineText = text.Split('\n').ElementAtOrDefault(Math.Max(0, line - 1))?.TrimEnd('\r') ?? "";
        var cursor = Math.Clamp(column - 1, 0, lineText.Length);
        var before = lineText[..cursor];
        var prefix = ReadIdentifierBackward(before, before.Length);
        var dot = before.LastIndexOf('.');
        if (dot >= 0 && dot == before.Length - prefix.Length - 1)
        {
            var receiverName = ReadIdentifierBackward(before, dot);
            var receiverType = binder.ResolveCompletionType(receiverName);
            if (receiverType != null)
                return RankCompletions(binder.GetAccessibleMembers(receiverType).Select(x => x.Name), prefix);
            return Array.Empty<string>();
        }
        return RankCompletions(completionNames, prefix);
    }

    private DslSemanticReference? FindReference(string path, int line, int column)
    {
        if (!referencesByPath.TryGetValue(path, out var references)) return null;
        var low = 0; var high = references.Length - 1;
        while (low <= high)
        {
            var middle = low + ((high - low) / 2);
            var candidate = references[middle];
            if (candidate.Span.Line < line || (candidate.Span.Line == line && candidate.Span.Column + Math.Max(1, candidate.Span.Length) < column)) low = middle + 1;
            else high = middle - 1;
        }
        for (var i = Math.Max(0, low - 2); i < Math.Min(references.Length, low + 3); i++)
        {
            var span = references[i].Span;
            if (span.Line == line && column >= span.Column && column <= span.Column + Math.Max(1, span.Length)) return references[i];
        }
        return null;
    }

    private static IReadOnlyList<string> RankCompletions(IEnumerable<string> names, string query)
        => names.Select(name => (name, score: CompletionScore(name, query)))
            .Where(x => x.score < int.MaxValue)
            .OrderBy(x => x.score).ThenBy(x => x.name, StringComparer.Ordinal)
            .Select(x => x.name).ToArray();

    private static int CompletionScore(string name, string query)
    {
        if (query.Length == 0) return 400;
        if (string.Equals(name, query, StringComparison.OrdinalIgnoreCase)) return 0;
        if (name.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 100 + name.Length - query.Length;
        var compact = new string(name.Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
        var q = query.ToLowerInvariant();
        var subsequence = 0; var cursor = 0;
        foreach (var c in q) { var found = compact.IndexOf(c, cursor); if (found < 0) { subsequence = int.MaxValue; break; } subsequence += found - cursor; cursor = found + 1; }
        if (subsequence != int.MaxValue) return 200 + subsequence + name.Length;
        var contains = name.IndexOf(query, StringComparison.OrdinalIgnoreCase);
        return contains >= 0 ? 300 + contains + name.Length : int.MaxValue;
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

    private IReadOnlyList<DslDiagnostic> ValidateRename(SemanticDefinition definition, IReadOnlyList<WorkspaceTextEdit> edits, Solution? renamedSolution, string newName)
    {
        if (renamedSolution != null)
        {
            // A workspace can contain several projects. Validate the project that
            // owns the World used by this semantic model, not an arbitrary first
            // project (which may not even contain the generated source).
            var worldLocation = world.Locations.FirstOrDefault(x => x.IsInSource)?.SourceTree?.FilePath;
            var project = renamedSolution.Projects.FirstOrDefault(p =>
                worldLocation != null && p.Documents.Any(d => string.Equals(d.FilePath, worldLocation, StringComparison.OrdinalIgnoreCase)))
                ?? renamedSolution.Projects.FirstOrDefault();
            var renamedCompilation = project?.GetCompilationAsync().GetAwaiter().GetResult();
            if (renamedCompilation == null)
                return new[] { new DslDiagnostic("SEGTOOL004", "The renamed C# solution could not be compiled.", definition.Location.Span) };
            // Roslyn workspaces do not guarantee that an analyzer-backed source
            // generator is rerun after a rename. Generated Segusum trees are
            // therefore not a valid validation snapshot here: their input .seg
            // files have not yet been replaced and they can report stale errors.
            // The author compilation remains authoritative for C# errors; the
            // updated DSL is rebound below against its renamed symbols. This is
            // deliberately a source-origin filter, not an error-id suppression.
            renamedCompilation = RebuildGeneratedValidationCompilation(renamedCompilation, definition.CSharpSymbol!, newName);
            var newErrors = renamedCompilation.GetDiagnostics()
                .Where(x => x.Severity == DiagnosticSeverity.Error).ToArray();
            var oldErrorCounts = compilation.GetDiagnostics()
                .Where(x => x.Severity == DiagnosticSeverity.Error)
                .GroupBy(x => x.Id).ToDictionary(x => x.Key, x => x.Count(), StringComparer.Ordinal);
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

    private Compilation RebuildGeneratedValidationCompilation(Compilation renamedCompilation, ISymbol renamedSymbol, string replacement)
    {
        // MSBuildWorkspace exposes the generated trees in the original project,
        // but Roslyn's renamed Solution snapshot does not reliably rerun the
        // AdditionalFiles-driven generator. Reconstruct the final snapshot by
        // removing stale Segusum trees and semantically rewriting only generated
        // identifier nodes bound to the renamed author symbol. This is equivalent
        // to the generator result for a rename and keeps generated C# out of the
        // returned edits.
        var stale = renamedCompilation.SyntaxTrees.Where(SegusumGeneratedSource.IsGenerated).ToArray();
        var result = stale.Length == 0 ? renamedCompilation : renamedCompilation.RemoveSyntaxTrees(stale);
        var originalGenerated = compilation.SyntaxTrees.Where(SegusumGeneratedSource.IsGenerated).ToArray();
        foreach (var tree in originalGenerated)
        {
            var root = tree.GetRoot();
            var model = compilation.GetSemanticModel(tree);
            var rewritten = new GeneratedRenameRewriter(model, renamedSymbol.Name, renamedSymbol, replacement).Visit(root);
            var generatedTree = rewritten == null
                ? tree
                : tree.WithRootAndOptions(rewritten, tree.Options);
            result = result.AddSyntaxTrees(generatedTree);
        }
        return result;
    }

    private void AddSemanticCSharpRenameEdits(List<WorkspaceTextEdit> edits, Document document, ISymbol symbol, string replacement)
    {
        var root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
        var model = document.GetSemanticModelAsync().GetAwaiter().GetResult();
        if (root == null || model == null) return;
        var path = document.FilePath ?? "";
        var text = document.GetTextAsync().GetAwaiter().GetResult().ToString();
        foreach (var node in root.DescendantNodes().Where(x => x is IdentifierNameSyntax || x is MethodDeclarationSyntax))
        {
            var resolved = node switch
            {
                MethodDeclarationSyntax method => model.GetDeclaredSymbol(method),
                _ => model.GetSymbolInfo(node).Symbol
            };
            if (resolved != null && CorrespondsTo(resolved, symbol))
                edits.Add(new WorkspaceTextEdit(path, SourceSpan.From(path, text, node.Span.Start, node.Span.Length), replacement));
        }
    }

    private static void AddRoslynChangeTokenEdits(List<WorkspaceTextEdit> edits, Document document, IEnumerable<TextChange> changes, string replacement)
    {
        var path = document.FilePath ?? "";
        var text = document.GetTextAsync().GetAwaiter().GetResult().ToString();
        foreach (var change in changes)
        {
            var start = Math.Clamp(change.Span.Start, 0, text.Length);
            var end = Math.Clamp(change.Span.End, start, text.Length);
            while (start > 0 && (char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_')) start--;
            while (end < text.Length && (char.IsLetterOrDigit(text[end]) || text[end] == '_')) end++;
            edits.Add(new WorkspaceTextEdit(path, SourceSpan.From(path, text, start, end - start), replacement));
        }
    }

    private static bool IsTokenBoundary(string text, TextSpan span)
    {
        var start = Math.Clamp(span.Start, 0, text.Length);
        var end = Math.Clamp(span.End, start, text.Length);
        return (start == 0 || !(char.IsLetterOrDigit(text[start - 1]) || text[start - 1] == '_')) &&
            (end == text.Length || !(char.IsLetterOrDigit(text[end]) || text[end] == '_'));
    }

    private bool HasRenamedTokenAtAuthorSpan(Document document, SourceText renamedText, ISymbol symbol, string replacement)
    {
        var root = document.GetSyntaxRootAsync().GetAwaiter().GetResult();
        var model = document.GetSemanticModelAsync().GetAwaiter().GetResult();
        if (root == null || model == null) return false;
        var node = root.DescendantNodes().Where(x => x is IdentifierNameSyntax || x is MethodDeclarationSyntax)
            .FirstOrDefault(x =>
            {
                var resolved = x switch
                {
                    MethodDeclarationSyntax method => model.GetDeclaredSymbol(method),
                    _ => model.GetSymbolInfo(x).Symbol
                };
                return resolved != null && CorrespondsTo(resolved, symbol);
            });
        return node != null && node.SpanStart + replacement.Length <= renamedText.Length &&
            string.Equals(renamedText.ToString().Substring(node.SpanStart, replacement.Length), replacement, StringComparison.Ordinal);
    }

    private static bool CorrespondsTo(ISymbol left, ISymbol right)
    {
        if (SymbolEqualityComparer.Default.Equals(left, right)) return true;
        var leftLocation = left.Locations.FirstOrDefault(x => x.IsInSource);
        var rightLocation = right.Locations.FirstOrDefault(x => x.IsInSource);
        return leftLocation?.SourceTree?.FilePath != null && rightLocation?.SourceTree?.FilePath != null &&
            string.Equals(leftLocation.SourceTree.FilePath, rightLocation.SourceTree.FilePath, StringComparison.OrdinalIgnoreCase) &&
            leftLocation.SourceSpan == rightLocation.SourceSpan;
    }

    private sealed class GeneratedRenameRewriter : CSharpSyntaxRewriter
    {
        private readonly SemanticModel model;
        private readonly string oldName;
        private readonly ISymbol oldSymbol;
        private readonly string? replacement;
        public GeneratedRenameRewriter(SemanticModel model, string oldName, ISymbol oldSymbol, string? newName)
        { this.model = model; this.oldName = oldName; this.oldSymbol = oldSymbol; replacement = newName; }
        public override SyntaxNode? VisitIdentifierName(IdentifierNameSyntax node)
        {
            if (replacement != null && node.Identifier.ValueText == oldName &&
                SymbolEqualityComparer.Default.Equals(model.GetSymbolInfo(node).Symbol, oldSymbol))
                return node.WithIdentifier(SyntaxFactory.Identifier(node.Identifier.LeadingTrivia, replacement, node.Identifier.TrailingTrivia));
            return base.VisitIdentifierName(node);
        }
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
