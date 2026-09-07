using Segusum.Scripting.Tooling;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("segusum migrate-csharp <file.cs> --world WORLD [--output FILE] [--audit] [--emit-partial] [--method NAME] [--dry-run]");
    Console.WriteLine("segusum audit-ownership <file.seg> --runtime-root DIR --legacy FILE [--legacy-methods FILE] [--apply] [--runtime-bridge FILE]");
    return 0;
}
if (string.Equals(args[0], "audit-ownership", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2) { Console.Error.WriteLine("Usage: audit-ownership <file.seg> --runtime-root DIR --legacy FILE [--legacy-methods FILE] [--apply] [--runtime-bridge FILE]"); return 2; }
    var segPath = Path.GetFullPath(args[1]); string? runtimeRoot = null; string? legacy = null; string? legacyMethods = null; string? bridge = null; var apply = false;
    for (var i = 2; i < args.Length; i++)
        switch (args[i])
        {
            case "--runtime-root": runtimeRoot = Path.GetFullPath(args[++i]); break;
            case "--legacy": legacy = Path.GetFullPath(args[++i]); break;
            case "--legacy-methods": legacyMethods = Path.GetFullPath(args[++i]); break;
            case "--runtime-bridge": bridge = Path.GetFullPath(args[++i]); break;
            case "--apply": apply = true; break;
            default: Console.Error.WriteLine($"Unknown option: {args[i]}"); return 2;
        }
    if (runtimeRoot == null || legacy == null) { Console.Error.WriteLine("Both --runtime-root and --legacy are required."); return 2; }
    var runtimeFiles = Directory.EnumerateFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories)
        .Where(x => !x.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !x.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    var report = SegOwnership.Analyze(segPath, runtimeFiles, legacy);
    Console.WriteLine("OWNED BY SEG -> REMOVE FROM C#:"); foreach (var x in report.RemoveFromCSharp) Console.WriteLine("  " + x);
    Console.WriteLine("REFERENCED BY SEG BUT OWNED BY C# -> KEEP:"); foreach (var x in report.KeepInCSharp) Console.WriteLine("  " + x);
    Console.WriteLine("REFERENCE-ONLY SYMBOLS MISSING FROM ACTIVE C#:"); foreach (var x in report.MissingReferenceOnlyRuntimeSymbols) Console.WriteLine("  " + x);
    Console.WriteLine("C# SYMBOLS NOT USED BY THIS SEG:"); foreach (var x in report.CSharpSymbolsNotUsedBySeg) Console.WriteLine("  " + x);
    Console.WriteLine("SEG OWNED SYMBOL WITHOUT MATCHING LEGACY C#:"); foreach (var x in report.SegOwnedWithoutMatchingLegacy) Console.WriteLine("  " + x);
    Console.WriteLine("SEG-OWNED SAFE TO REMOVE:"); foreach (var x in report.MethodsToRemove) Console.WriteLine("  " + x.Name + "(" + string.Join(", ", x.ParameterTypes) + "): " + x.ReturnType + " [" + x.Path + "]");
    Console.WriteLine("SEG-DEFINED BUT REFERENCED BY ACTIVE C#:"); foreach (var x in report.MethodsReferencedByActiveCSharp) Console.WriteLine("  " + x.Name + "(" + string.Join(", ", x.ParameterTypes) + "): " + x.ReturnType + " [" + x.Path + "]");
    Console.WriteLine("C#-OWNED / REFERENCE-ONLY:"); foreach (var x in report.MethodsKept.Where(x => report.SegMethods.All(s => !string.Equals(s.Name, x.Name, StringComparison.Ordinal)))) Console.WriteLine("  " + x.Name + "(" + string.Join(", ", x.ParameterTypes) + "): " + x.ReturnType);
    Console.WriteLine("AMBIGUOUS METHOD MATCHES:"); foreach (var x in report.AmbiguousMethodMatches) Console.WriteLine("  " + x.Name + "(" + string.Join(", ", x.ParameterTypes) + "): " + x.ReturnType + " [" + x.Path + "]");
    Console.WriteLine("SEG METHODS WITHOUT LEGACY MATCH:"); foreach (var x in report.SegMethodsWithoutMatchingLegacy) Console.WriteLine("  " + x);
    if (report.Ambiguities.Count != 0) { Console.WriteLine("AMBIGUITIES:"); foreach (var x in report.Ambiguities) Console.WriteLine("  " + x); return 3; }
    if (apply)
    {
        var archived = SegOwnership.EnsureLegacySymbols(report, legacy);
        Console.WriteLine("ARCHIVED OWNED SYMBOLS: " + (archived.Count == 0 ? "none" : string.Join(", ", archived)));
        if (legacyMethods == null) { Console.Error.WriteLine("--legacy-methods is required with --apply."); return 2; }
        var archivedMethods = SegOwnership.EnsureLegacyMethods(report, legacyMethods);
        Console.WriteLine("ARCHIVED OWNED METHODS: " + (archivedMethods.Count == 0 ? "none" : string.Join(", ", archivedMethods)));
        foreach (var file in SegOwnership.ApplyRemoval(report)) Console.WriteLine("UPDATED: " + file);
        if (bridge == null) { Console.Error.WriteLine("--runtime-bridge is required with --apply."); return 2; }
        var bridgeFile = SegOwnership.EnsureReferenceOnlyBridge(report, legacy, bridge);
        if (bridgeFile != null) Console.WriteLine("UPDATED: " + bridgeFile); else Console.WriteLine("REFERENCE BRIDGE: none required");
    }
    return 0;
}
if (!string.Equals(args[0], "migrate-csharp", StringComparison.OrdinalIgnoreCase) || args.Length < 2)
{
    Console.Error.WriteLine("Usage: migrate-csharp <file.cs> --world WORLD [--output FILE] [--audit] [--emit-partial] [--method NAME] [--dry-run]");
    return 2;
}
var input = Path.GetFullPath(args[1]); var output = (string?)null; var partial = false; var audit = false; var dryRun = false; string? method = null; string? world = null;
for (var i = 2; i < args.Length; i++)
    switch (args[i])
    {
        case "--output": output = Path.GetFullPath(args[++i]); break;
        case "--emit-partial": partial = true; break;
        case "--audit": audit = true; break;
        case "--dry-run": dryRun = true; break;
        case "--method": method = args[++i]; break;
        case "--world": world = args[++i]; break;
        default: Console.Error.WriteLine($"Unknown option: {args[i]}"); return 2;
    }
if (string.IsNullOrWhiteSpace(world)) { Console.Error.WriteLine("Missing required option: --world WORLD"); return 2; }
var result = CSharpToSegTranspiler.Transpile(input, File.ReadAllText(input), partial, method, world);
Console.WriteLine($"file: {input}");
Console.WriteLine($"status: {(result.IsFullyTranslated ? "TRANSLATED" : partial ? "PARTIAL" : "UNSUPPORTED")}");
foreach (var group in result.Units.GroupBy(x => x.Status).OrderBy(x => x.Key)) Console.WriteLine($"units {group.Key}: {group.Count()}");
if (audit)
    foreach (var unit in result.Units)
    {
        Console.WriteLine($"unit {unit.Id} {unit.Status} {unit.Path}:{unit.Line}-{unit.EndLine}");
        foreach (var diagnostic in unit.Diagnostics)
            Console.WriteLine($"  unit-diagnostic {diagnostic.Status}: {diagnostic.Path}:{diagnostic.Line}: {diagnostic.Reason}");
    }
foreach (var d in result.Diagnostics) Console.WriteLine($"{d.Status}: {d.Path}:{d.Line}: {d.Reason}");
if (!audit && output != null && !dryRun)
{
    if (Directory.Exists(output)) output = Path.Combine(output, Path.GetFileNameWithoutExtension(input) + ".seg");
    File.WriteAllText(output, result.Text);
}
else if (!audit && output == null && !dryRun) Console.Write(result.Text);
return result.IsFullyTranslated ? 0 : 1;
