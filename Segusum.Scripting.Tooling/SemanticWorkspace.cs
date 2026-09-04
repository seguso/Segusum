using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
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
            foreach (var tree in compilation.SyntaxTrees)
            {
                var semanticModel = compilation.GetSemanticModel(tree);
                foreach (var node in tree.GetRoot().DescendantNodes().OfType<IdentifierNameSyntax>())
                {
                    var symbol = semanticModel.GetSymbolInfo(node).Symbol;
                    if (SameSymbol(symbol, definition.CSharpSymbol) && !dsl.Any(x => x.Location.Path == tree.FilePath && x.Location.Span.Start == node.SpanStart))
                        dsl.Add(new SemanticReference(definition.DisplayName, new SemanticLocation(tree.FilePath, FromLocation(Location.Create(tree, node.Span)), "csharp-reference"), symbol, null));
                }
            }
        }
        return dsl;
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
        foreach (var reference in references.Where(x => x.Location.Kind is "name" or "csharp-reference")) edits.Add(new WorkspaceTextEdit(reference.Location.Path, reference.Location.Span, newName));
        if (definition.CSharpSymbol != null)
            foreach (var location in definition.CSharpSymbol.Locations.Where(x => x.IsInSource)) edits.Add(new WorkspaceTextEdit(location.SourceTree!.FilePath, FromLocation(location), newName));
        else if (definition.DslSymbol != null && model.DslDefinitions.TryGetValue(definition.DslSymbol, out var declaration)) edits.Add(new WorkspaceTextEdit(declaration.Path, DslNameLocation(definition.DslSymbol.Name, declaration), newName));
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
