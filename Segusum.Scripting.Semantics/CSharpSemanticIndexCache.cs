using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Segusum.Scripting.Core;

namespace Segusum.Scripting.Semantics;

public sealed class CSharpSemanticIndexCache
{
    private readonly Compilation compilation;
    private readonly object gate = new();
    private readonly Dictionary<INamedTypeSymbol, IReadOnlyDictionary<string, INamedTypeSymbol?>> typeIndexes = new(SymbolEqualityComparer.Default);
    private HashSet<ISymbol>? roomChangedTargets;
    public int TypeIndexBuildCount { get; private set; }
    public int RoomChangedIndexBuildCount { get; private set; }

    public CSharpSemanticIndexCache(Compilation compilation) => this.compilation = compilation;

    public IReadOnlyDictionary<string, INamedTypeSymbol?> GetTypeIndex(INamedTypeSymbol world)
    {
        var started = Stopwatch.StartNew();
        lock (gate)
        {
            if (typeIndexes.TryGetValue(world, out var cached))
            {
                Console.Error.WriteLine($"csharpSemanticCache typeIndex=hit world={world.ToDisplayString()} entries={cached.Count} elapsed={started.Elapsed.TotalMilliseconds:0.0}ms");
                return cached;
            }

            var index = new Dictionary<string, INamedTypeSymbol?>(StringComparer.Ordinal);
            VisitNamespace(compilation.GlobalNamespace, compilation, world, index);
            typeIndexes[world] = index;
            TypeIndexBuildCount++;
            Console.Error.WriteLine($"csharpSemanticCache typeIndex=miss world={world.ToDisplayString()} entries={index.Count} elapsed={started.Elapsed.TotalMilliseconds:0.0}ms");
            return index;
        }
    }

    public IReadOnlyCollection<ISymbol> GetRoomChangedTargets()
    {
        var started = Stopwatch.StartNew();
        lock (gate)
        {
            if (roomChangedTargets != null)
            {
                Console.Error.WriteLine($"csharpSemanticCache roomChangedIndex=hit targets={roomChangedTargets.Count} elapsed={started.Elapsed.TotalMilliseconds:0.0}ms");
                return roomChangedTargets;
            }

            var targets = new HashSet<ISymbol>(SymbolEqualityComparer.Default);
            var trees = 0;
            var invocations = 0;
            foreach (var tree in compilation.SyntaxTrees)
            {
                if (SegusumGeneratedSource.IsGenerated(tree)) continue;
                trees++;
                var semanticModel = compilation.GetSemanticModel(tree);
                foreach (var invocation in tree.GetRoot().DescendantNodes().OfType<InvocationExpressionSyntax>())
                {
                    invocations++;
                    if (invocation.Expression is not IdentifierNameSyntax { Identifier.ValueText: "addRoomChangedHandler" } || invocation.ArgumentList.Arguments.Count == 0) continue;
                    var symbol = semanticModel.GetSymbolInfo(invocation.ArgumentList.Arguments[0].Expression).Symbol;
                    if (symbol != null) targets.Add(symbol);
                }
            }

            roomChangedTargets = targets;
            RoomChangedIndexBuildCount++;
            Console.Error.WriteLine($"csharpSemanticCache roomChangedIndex=miss trees={trees} invocations={invocations} targets={targets.Count} elapsed={started.Elapsed.TotalMilliseconds:0.0}ms");
            return targets;
        }
    }

    private static void VisitNamespace(INamespaceSymbol current, Compilation compilation, INamedTypeSymbol world, Dictionary<string, INamedTypeSymbol?> index)
    {
        foreach (var member in current.GetMembers())
        {
            if (member is INamespaceSymbol childNamespace) VisitNamespace(childNamespace, compilation, world, index);
            else if (member is INamedTypeSymbol type) VisitType(type, compilation, world, index);
        }
    }

    private static void VisitType(INamedTypeSymbol type, Compilation compilation, INamedTypeSymbol world, Dictionary<string, INamedTypeSymbol?> index)
    {
        if (!SegusumGeneratedSource.IsGenerated(type) && compilation.IsSymbolAccessibleWithin(type, world, world))
        {
            if (!index.TryGetValue(type.Name, out var existing)) index[type.Name] = type;
            else if (!SymbolEqualityComparer.Default.Equals(existing, type)) index[type.Name] = null;
        }

        foreach (var nested in type.GetTypeMembers()) VisitType(nested, compilation, world, index);
    }
}
