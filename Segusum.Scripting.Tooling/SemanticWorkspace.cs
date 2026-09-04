using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FindSymbols;
using Microsoft.CodeAnalysis.Rename;
using Microsoft.CodeAnalysis.Text;
using Segusum.Scripting.Core;
using Segusum.Scripting.Generator;

namespace Segusum.Scripting.Tooling;

public sealed record SemanticLocation(string Path, SourceSpan Span, string Kind);
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
    private readonly Compilation compilation;
    private readonly INamedTypeSymbol world;
    private readonly IReadOnlyList<DslSource> sources;
    private readonly BoundModel model;
    private readonly Dictionary<string, DslSource> documents;
    private readonly List<DslDiagnostic> diagnostics = new();
    private readonly AdhocWorkspace roslynWorkspace;
    private readonly Solution roslynSolution;

    public DslSemanticWorkspace(Compilation compilation, INamedTypeSymbol world, IEnumerable<DslSource> sources)
    {
        this.compilation = compilation;
        this.world = world;
        this.sources = sources.ToArray();
        documents = this.sources.ToDictionary(x => x.Path, x => x, StringComparer.OrdinalIgnoreCase);
        foreach (var source in this.sources)
        {
            var parsed = DslParser.Parse(source);
            diagnostics.AddRange(parsed.Diagnostics);
        }
        var declarations = documents.Values.Select(x => DslParser.Parse(x).Document).SelectMany(x => x.Declarations).ToArray();
        var binder = new DslBinder(compilation, world, diagnostics.Add);
        binder.Bind(declarations);
        model = binder.Model;
        (roslynWorkspace, roslynSolution) = CreateRoslynSolution();
    }

    public IReadOnlyList<DslDiagnostic> Diagnostics => diagnostics;
    public IReadOnlyList<SemanticReference> FindReferences(string path, int line, int column)
    {
        var definition = GetDefinition(path, line, column);
        if (definition == null) return Array.Empty<SemanticReference>();
        var dsl = model.ReferencesByNode
            .Where(x => SameSymbol(x.CSharpSymbol, definition.CSharpSymbol) || SameDsl(x.DslSymbol, definition.DslSymbol))
            .Select(x => new SemanticReference(definition.DisplayName, new SemanticLocation(x.Path, x.Span, x.ReferenceKind), x.CSharpSymbol, x.DslSymbol))
            .ToList();
        if (definition.CSharpSymbol != null)
        {
            var workspaceSymbol = ResolveWorkspaceSymbol(definition.CSharpSymbol);
            if (workspaceSymbol != null)
            {
                var references = SymbolFinder.FindReferencesAsync(workspaceSymbol, roslynSolution, cancellationToken: CancellationToken.None).GetAwaiter().GetResult();
                foreach (var reference in references.SelectMany(x => x.Locations))
                {
                    var location = reference.Location;
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
        var references = FindReferences(path, line, column);
        var edits = new List<WorkspaceTextEdit>();
        if (definition.CSharpSymbol != null)
        {
            var workspaceSymbol = ResolveWorkspaceSymbol(definition.CSharpSymbol);
            if (workspaceSymbol == null)
                return new RenameResult(Array.Empty<WorkspaceTextEdit>(), new[] { new DslDiagnostic("SEGTOOL003", "The C# symbol is not available in the Roslyn workspace.", new SourceSpan(path, 0, 0, line, column)) });
            var renamed = Renamer.RenameSymbolAsync(roslynSolution, workspaceSymbol, newName, null, CancellationToken.None).GetAwaiter().GetResult();
            foreach (var project in roslynSolution.Projects)
                foreach (var document in project.Documents)
                    foreach (var change in document.GetTextChangesAsync(renamed.GetDocument(document.Id)!).GetAwaiter().GetResult())
                        edits.Add(new WorkspaceTextEdit(document.FilePath ?? "", SourceSpan.From(document.FilePath ?? "", document.GetTextAsync().GetAwaiter().GetResult().ToString(), change.Span.Start, change.Span.Length), newName));
        }
        else if (definition.DslSymbol != null && model.DslDefinitions.TryGetValue(definition.DslSymbol, out var declaration))
            edits.Add(new WorkspaceTextEdit(declaration.Path, DslNameLocation(definition.DslSymbol.Name, declaration), newName));
        foreach (var reference in references.Where(x => x.Location.Kind == "name"))
            edits.Add(new WorkspaceTextEdit(reference.Location.Path, reference.Location.Span, newName));
        return new RenameResult(edits.DistinctBy(x => (x.Path, x.Span.Start)).ToArray(), Array.Empty<DslDiagnostic>());
    }

    public IReadOnlyList<string> GetCompletions(string path, int line, int column)
    {
        var tree = compilation.SyntaxTrees.FirstOrDefault(x => string.Equals(x.FilePath, path, StringComparison.OrdinalIgnoreCase));
        if (tree != null)
        {
            var token = tree.GetRoot().FindToken(GetPosition(tree, line, column));
            if (token.Text == ".") return compilation.GetSemanticModel(tree).LookupSymbols(token.SpanStart + 1).Where(x => x is IFieldSymbol or IPropertySymbol or IMethodSymbol).Select(x => x.Name).Distinct(StringComparer.Ordinal).OrderBy(x => x).ToArray();
        }
        return model.DslSymbolsByName.Keys.Concat(world.GetMembers().Select(x => x.Name)).Distinct(StringComparer.Ordinal).OrderBy(x => x).ToArray();
    }

    private static bool SameSymbol(ISymbol? left, ISymbol? right) => left != null && right != null && SymbolEqualityComparer.Default.Equals(left, right);
    private static bool SameDsl(DslSymbolIdentity? left, DslSymbolIdentity? right) => left != null && right != null && left.Equals(right);
    private static int GetPosition(SyntaxTree tree, int line, int column) => tree.GetText().Lines[Math.Max(0, line - 1)].Start + Math.Max(0, column - 1);
    private static SemanticLocation ToLocation(Location location)
    {
        if (location == Location.None || location.SourceTree == null)
            return new SemanticLocation("", new SourceSpan("", 0, 0, 1, 1), "csharp-definition");
        var path = location.SourceTree.FilePath ?? "";
        return new SemanticLocation(path, SourceSpan.From(path, location.SourceTree.GetText().ToString(), location.SourceSpan.Start, location.SourceSpan.Length), "csharp-definition");
    }

    private (AdhocWorkspace Workspace, Solution Solution) CreateRoslynSolution()
    {
        var workspace = new AdhocWorkspace();
        var projectId = ProjectId.CreateNewId("Segusum.Tooling");
        var projectInfo = ProjectInfo.Create(projectId, VersionStamp.Create(), "Segusum.Tooling", "Segusum.Tooling", LanguageNames.CSharp,
            metadataReferences: compilation.References.OfType<PortableExecutableReference>());
        workspace.AddProject(projectInfo);
        foreach (var tree in compilation.SyntaxTrees)
        {
            var documentId = DocumentId.CreateNewId(projectId, tree.FilePath);
            var text = TextAndVersion.Create(tree.GetText(), VersionStamp.Create(), tree.FilePath);
            workspace.AddDocument(DocumentInfo.Create(documentId, System.IO.Path.GetFileName(tree.FilePath), filePath: tree.FilePath, loader: TextLoader.From(text)));
        }
        return (workspace, workspace.CurrentSolution);
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
