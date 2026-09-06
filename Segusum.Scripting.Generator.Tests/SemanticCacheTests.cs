using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Segusum.Scripting.Core;
using Segusum.Scripting.Semantics;
using Segusum.Scripting.Tooling;

namespace Segusum.Scripting.Generator.Tests;

public sealed class SemanticCacheTests
{
    [Fact]
    public void ParseCacheReusesUnchangedSourcesAndBoundsVersionsPerPath()
    {
        var cache = new DslParseCache();
        var a = new DslSource("A.seg", "world game\nstate a: int = 1\n");
        var b = new DslSource("B.seg", "world game\nstate b: int = 1\n");
        var c = new DslSource("C.seg", "world game\nstate c: int = 1\n");

        var aResult = cache.Get(a, overlay: false, out var aFirstReuse);
        var bResult = cache.Get(b, overlay: false, out var bFirstReuse);
        var cResult = cache.Get(c, overlay: false, out var cFirstReuse);
        Assert.False(aFirstReuse || bFirstReuse || cFirstReuse);

        cache.Get(a, overlay: false, out var aDiskReuse);
        var bOverlayResult = cache.Get(new DslSource("B.seg", "world game\nstate b: int = 2\n"), overlay: true, out var bOverlayReuse);
        cache.Get(c, overlay: false, out var cDiskReuse);
        Assert.True(aDiskReuse);
        Assert.False(bOverlayReuse);
        Assert.True(cDiskReuse);
        Assert.Same(aResult.Document, cache.Get(a, overlay: false, out _).Document);
        Assert.Empty(aResult.Diagnostics);
        Assert.Empty(bResult.Diagnostics);
        Assert.Empty(cResult.Diagnostics);
        Assert.Empty(bOverlayResult.Diagnostics);
        var invalidOverlay = cache.Get(new DslSource("B.seg", "world game\nstate : int = 2\n"), overlay: true, out var invalidReuse);
        Assert.False(invalidReuse);
        Assert.NotEmpty(invalidOverlay.Diagnostics);

        cache.Get(new DslSource("B.seg", "world game\nstate b: int = 3\n"), overlay: true, out var bSecondEditReuse);
        Assert.False(bSecondEditReuse);
        cache.Get(new DslSource("B.seg", "world game\nstate b: int = 3\n"), overlay: true, out var bSameEditReuse);
        Assert.True(bSameEditReuse);

        var stats = cache.Stats;
        Assert.Equal(6, stats.ParsedFiles);
        Assert.Equal(4, stats.ReusedFiles);
    }

    [Fact]
    public void CSharpIndexesAreBuiltOncePerCompilationAndWorld()
    {
        var compilation = CreateCompilation("using Seg; namespace Demo { public class Pinco : WorldBase { public Pinco() : base(\"game\") { } public Character olivia = null!; } }");
        var world = compilation.GetTypeByMetadataName("Demo.Pinco")!;
        var indexes = new CSharpSemanticIndexCache(compilation);

        var first = indexes.GetTypeIndex(world);
        var second = indexes.GetTypeIndex(world);
        var firstRoom = indexes.GetRoomChangedTargets();
        var secondRoom = indexes.GetRoomChangedTargets();

        Assert.Same(first, second);
        Assert.Same(firstRoom, secondRoom);
        Assert.Equal(1, indexes.TypeIndexBuildCount);
        Assert.Equal(1, indexes.RoomChangedIndexBuildCount);
    }

    private static CSharpCompilation CreateCompilation(string source)
    {
        var references = ((string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES"))!
            .Split(Path.PathSeparator)
            .Select(path => MetadataReference.CreateFromFile(path))
            .Cast<MetadataReference>()
            .ToList();
        references.Add(MetadataReference.CreateFromFile(typeof(Seg.WorldBase).Assembly.Location));
        return CSharpCompilation.Create("SemanticCacheTest", new[] { CSharpSyntaxTree.ParseText(source, path: "World.cs") }, references, new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }
}
