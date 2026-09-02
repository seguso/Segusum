using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Testing;
using Microsoft.CodeAnalysis.Testing;
using Microsoft.CodeAnalysis.Testing.Verifiers;
using Segusum.Analyzers;

namespace Segusum.Analyzers.Tests;

public sealed class CycleIdAnalyzerTests
{
    private const string Api = """
        namespace Seg {
          public class Cycle { }
          public abstract class WorldBase {
            protected Cycle startCycle(string id, Action a) => new();
            protected Cycle startCycle(CycleElemId id, Action a) => new();
          }
          public sealed class CycleElemId { }
          public static class Utils {
            public static Cycle addToCycle(this Cycle cycle, string id, Action a) => cycle;
          }
        }
        """;

    private static CSharpAnalyzerTest<CycleIdAnalyzer, XUnitVerifier> Test(string code)
    {
        var test = new CSharpAnalyzerTest<CycleIdAnalyzer, XUnitVerifier>
        {
            TestCode = "using System; using Seg;" + code + Api
        };
        return test;
    }

    [Fact]
    public async Task NonLiteralStartCycleIsAnError() => await Test("""
        class Game : Seg.WorldBase { void M() {
          var id = "pippo";
          startCycle({|SEG001:id|}, () => { });
        }}
        """).RunAsync();

    [Fact]
    public async Task EmptyAndWhitespaceIdsAreErrors() => await Test("""
        class Game : Seg.WorldBase { void M() {
          startCycle({|SEG002:""|}, () => { });
          startCycle({|SEG002:"   "|}, () => { });
        }}
        """).RunAsync();

    [Fact]
    public async Task DuplicateIdsAcrossFilesAreErrors()
    {
        var test = Test("""
            partial class Game : Seg.WorldBase { void M() {
              startCycle("pippo", () => { });
            }}
            """);
        test.TestState.Sources.Add(("FileB.cs", """
            partial class Game { void N() {
              startCycle({|SEG003:"pippo"|}, () => { });
            }}
            """));
        await test.RunAsync();
    }

    [Fact]
    public async Task DuplicateIdsInOneFileAreErrors() => await Test("""
            partial class Game : Seg.WorldBase { void M() {
          startCycle("pippo", () => { });
          startCycle({|SEG003:"pippo"|}, () => { });
        }}
        """).RunAsync();

    [Fact]
    public async Task ValidAndHistoricalIdsAreAccepted() => await Test("""
        class Game : Seg.WorldBase { void M() {
          startCycle("pippo", () => { });
          startCycle("pluto", () => { });
          Seg.CycleElemId id = new();
          startCycle(id, () => { });
        }}
        """).RunAsync();

    [Fact]
    public async Task NonSegusumSameNamedMethodIsIgnored() => await Test("""
        class Other { void startCycle(string id) { } void M() {
          startCycle("pippo");
        }}
        """).RunAsync();

    [Fact]
    public async Task AddToCycleUsesTheSameGlobalIdSpace() => await Test("""
        class Game : Seg.WorldBase { void M() {
          var c = new Seg.Cycle();
          c.addToCycle("pippo", () => { });
          startCycle({|SEG003:"pippo"|}, () => { });
        }}
        """).RunAsync();
}
