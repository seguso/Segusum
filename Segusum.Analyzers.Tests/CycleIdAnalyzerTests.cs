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
          public class LogicObj { }
          public class Objective { }
          public class Explanation { }
          public class HandlerInput { }
          public abstract class WorldBase {
            protected Cycle startCycle(string id, Action a) => new();
            protected Cycle startCycle(CycleElemId id, Action a) => new();
            protected void addHandlerCombine(LogicObj lo1, LogicObj lo2, string sentence, Action<HandlerInput> handler = null, Explanation explanation = null, Func<bool> isPossibleNow = null) { }
            protected void addHandlerCombine(LogicObj lo1, LogicObj lo2, Func<string> sentence, Action<HandlerInput> handler = null, Explanation explanation = null, Func<bool> isPossibleNow = null) { }
            protected void addHandlerUseFor(LogicObj lo, Objective ob, Explanation ex, Action<HandlerInput> handler) { }
            protected void addHandlerUseFor(LogicObj lo, Objective ob, Action<HandlerInput> handler) { }
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

    [Fact]
    public async Task DuplicateCombineRegistrationIsAnError() => await Test("""
        class Game : Seg.WorldBase { Seg.LogicObj a = new(), b = new(); void M() {
          addHandlerCombine(a, b, "one", null);
          addHandlerCombine({|SEG004:a|}, b, "two", null);
        }}
        """).RunAsync();

    [Fact]
    public async Task CombineIsOrientedAndWorldsAreSeparate() => await Test("""
        class Game : Seg.WorldBase { Seg.LogicObj a = new(), b = new(), c = new(); void M() {
          addHandlerCombine(a, b, "one", null);
          addHandlerCombine(b, a, "reverse", null);
          addHandlerCombine(a, c, "other target", null);
          addHandlerCombine(c, b, "other first", null);
        }}
        class OtherGame : Seg.WorldBase { Seg.LogicObj a = new(), b = new(); void M() {
          addHandlerCombine(a, b, "independent", null);
        }}
        """).RunAsync();

    [Fact]
    public async Task CombineOverloadsAndNamedArgumentsStillCollide() => await Test("""
        class Game : Seg.WorldBase { Seg.LogicObj a = new(), b = new(); void M() {
          addHandlerCombine(a, b, "one", null);
          addHandlerCombine(
            lo2: b, lo1: {|SEG004:this.a|}, sentence: () => "two", handler: null);
        }}
        """).RunAsync();

    [Fact]
    public async Task DuplicateUseForRegistrationIsAnError() => await Test("""
        class Game : Seg.WorldBase { Seg.LogicObj a = new(); Seg.Objective ob = new(); void M() {
          addHandlerUseFor(a, ob, null);
          addHandlerUseFor({|SEG005:this.a|}, ob, null, null);
        }}
        """).RunAsync();

    [Fact]
    public async Task UseForDifferentElementsAndWorldsAreSeparate() => await Test("""
        class Game : Seg.WorldBase { Seg.LogicObj a = new(), b = new(); Seg.Objective x = new(), y = new(); void M() {
          addHandlerUseFor(a, x, null);
          addHandlerUseFor(a, y, null);
          addHandlerUseFor(b, x, null);
        }}
        class OtherGame : Seg.WorldBase { Seg.LogicObj a = new(); Seg.Objective x = new(); void M() {
          addHandlerUseFor(a, x, null);
        }}
        """).RunAsync();

    [Fact]
    public async Task NonSegusumHandlerNameIsIgnored() => await Test("""
        class Other { void addHandlerCombine(Seg.LogicObj a, Seg.LogicObj b, string s) { } void M() {
          addHandlerCombine(null, null, "not Segusum");
        }}
        """).RunAsync();
}
