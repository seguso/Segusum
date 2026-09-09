using Segusum.Scripting.Tooling;
using Segusum.Scripting.Core;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("segusum migrate-csharp <file.cs> --world WORLD [--output FILE] [--audit] [--emit-partial] [--method NAME] [--context-root DIR] [--dry-run]");
    Console.WriteLine("segusum audit-ownership <file.seg> --runtime-root DIR --history FILE [--apply]");
    Console.WriteLine("segusum parse-seg <file.seg>");
    Console.WriteLine("segusum audit-seg-declarations <generated.seg> --runtime-root DIR");
    Console.WriteLine("segusum extract-new-seg-declarations <generated.seg> --runtime-root DIR --output FILE");
    Console.WriteLine("segusum merge-seg <base.seg> <addition.seg>... --output FILE");
    return 0;
}
if (string.Equals(args[0], "parse-seg", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length != 2) { Console.Error.WriteLine("Usage: parse-seg <file.seg>"); return 2; }
    var segPath = Path.GetFullPath(args[1]);
    var stopwatch = System.Diagnostics.Stopwatch.StartNew();
    var parsed = DslParser.Parse(new Segusum.Scripting.Core.DslSource(segPath, File.ReadAllText(segPath)));
    stopwatch.Stop();
    Console.WriteLine($"file: {segPath}");
    Console.WriteLine($"declarations: {parsed.Document.Declarations.Count}");
    Console.WriteLine($"diagnostics: {parsed.Diagnostics.Count}");
    Console.WriteLine($"elapsed-ms: {stopwatch.Elapsed.TotalMilliseconds:0}");
    Console.WriteLine($"managed-memory: {GC.GetTotalMemory(false)}");
    foreach (var diagnostic in parsed.Diagnostics) Console.WriteLine($"{diagnostic.Id}: {diagnostic.Span.Line}:{diagnostic.Span.Column}: {diagnostic.Message}");
    return parsed.Diagnostics.Count == 0 ? 0 : 1;
}
if (string.Equals(args[0], "audit-seg-declarations", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2) { Console.Error.WriteLine("Usage: audit-seg-declarations <generated.seg> --runtime-root DIR"); return 2; }
    var generatedPath = Path.GetFullPath(args[1]); string? runtimeRoot = null;
    for (var i = 2; i < args.Length; i++)
    {
        if (args[i] == "--runtime-root") runtimeRoot = Path.GetFullPath(args[++i]);
        else { Console.Error.WriteLine($"Unknown option: {args[i]}"); return 2; }
    }
    if (runtimeRoot == null) { Console.Error.WriteLine("--runtime-root is required."); return 2; }
    var runtimeFiles = Directory.EnumerateFiles(runtimeRoot, "*.seg", SearchOption.AllDirectories)
        .Where(x => !x.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !x.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
        .Select(x => (x, File.ReadAllText(x)));
    var auditResult = SegDocumentMerger.Audit(generatedPath, File.ReadAllText(generatedPath), runtimeFiles);
    foreach (var entry in auditResult.Entries) Console.WriteLine($"{entry.Status}: {entry.Identity} body-sha256={entry.GeneratedBodyHash} existing={entry.ExistingPath ?? "-"}");
    Console.WriteLine($"Generated declarations: {auditResult.Entries.Count}");
    Console.WriteLine($"Already present identical: {auditResult.Entries.Count(x => x.Status == "AlreadyPresent")}");
    Console.WriteLine($"New declarations to integrate: {auditResult.Entries.Count(x => x.Status == "New")}");
    Console.WriteLine($"Conflicts: {auditResult.Entries.Count(x => x.Status == "Conflict")}");
    foreach (var diagnostic in auditResult.Diagnostics) Console.Error.WriteLine(diagnostic);
    return auditResult.Succeeded ? 0 : 1;
}
if (string.Equals(args[0], "extract-new-seg-declarations", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2) { Console.Error.WriteLine("Usage: extract-new-seg-declarations <generated.seg> --runtime-root DIR --output FILE"); return 2; }
    var generatedPath = Path.GetFullPath(args[1]); string? runtimeRoot = null; string? extractOutput = null;
    for (var i = 2; i < args.Length; i++)
    {
        if (args[i] == "--runtime-root") runtimeRoot = Path.GetFullPath(args[++i]);
        else if (args[i] == "--output") extractOutput = Path.GetFullPath(args[++i]);
        else { Console.Error.WriteLine($"Unknown option: {args[i]}"); return 2; }
    }
    if (runtimeRoot == null || extractOutput == null) { Console.Error.WriteLine("--runtime-root and --output are required."); return 2; }
    var runtimeFiles = Directory.EnumerateFiles(runtimeRoot, "*.seg", SearchOption.AllDirectories)
        .Where(x => !x.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !x.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
        .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
        .Select(x => (x, File.ReadAllText(x)));
    var extracted = SegDocumentMerger.ExtractNewDeclarations(generatedPath, File.ReadAllText(generatedPath), runtimeFiles);
    foreach (var diagnostic in extracted.Audit.Diagnostics) Console.Error.WriteLine(diagnostic);
    if (!extracted.Audit.Succeeded) return 1;
    Directory.CreateDirectory(Path.GetDirectoryName(extractOutput)!);
    File.WriteAllText(extractOutput, extracted.NewOnlyText);
    Console.WriteLine($"new-only: {extractOutput}");
    Console.WriteLine($"new-declarations: {extracted.Audit.Entries.Count(x => x.Status == "New")}");
    return 0;
}
if (string.Equals(args[0], "merge-seg", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 4) { Console.Error.WriteLine("Usage: merge-seg <base.seg> <addition.seg>... --output FILE"); return 2; }
    var basePath = Path.GetFullPath(args[1]); string? mergeOutput = null; var additions = new List<(string Path, string Text)>();
    for (var i = 2; i < args.Length; i++)
    {
        if (args[i] == "--output") { mergeOutput = Path.GetFullPath(args[++i]); continue; }
        var path = Path.GetFullPath(args[i]); additions.Add((path, File.ReadAllText(path)));
    }
    if (mergeOutput == null || additions.Count == 0) { Console.Error.WriteLine("Usage: merge-seg <base.seg> <addition.seg>... --output FILE"); return 2; }
    var merged = SegDocumentMerger.Merge(basePath, File.ReadAllText(basePath), additions);
    foreach (var diagnostic in merged.Diagnostics) Console.Error.WriteLine(diagnostic);
    if (!merged.Succeeded) return 1;
    Directory.CreateDirectory(Path.GetDirectoryName(mergeOutput)!);
    File.WriteAllText(mergeOutput, merged.Text);
    Console.WriteLine($"merged: {mergeOutput}");
    return 0;
}
if (string.Equals(args[0], "audit-ownership", StringComparison.OrdinalIgnoreCase))
{
    if (args.Length < 2) { Console.Error.WriteLine("Usage: audit-ownership <file.seg> --runtime-root DIR --history FILE [--apply] [--methods-only]"); return 2; }
    var segPath = Path.GetFullPath(args[1]); string? runtimeRoot = null; string? history = null; var apply = false; var methodsOnly = false;
    for (var i = 2; i < args.Length; i++)
        switch (args[i])
        {
            case "--runtime-root": runtimeRoot = Path.GetFullPath(args[++i]); break;
            case "--history": history = Path.GetFullPath(args[++i]); break;
            case "--apply": apply = true; break;
            case "--methods-only": methodsOnly = true; break;
            default: Console.Error.WriteLine($"Unknown option: {args[i]}"); return 2;
        }
    if (runtimeRoot == null || history == null) { Console.Error.WriteLine("Both --runtime-root and --history are required."); return 2; }
    var runtimeFiles = Directory.EnumerateFiles(runtimeRoot, "*.cs", SearchOption.AllDirectories)
        .Where(x => !x.Contains(Path.DirectorySeparatorChar + "bin" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !x.Contains(Path.DirectorySeparatorChar + "obj" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            && !x.Contains(Path.DirectorySeparatorChar + "docs" + Path.DirectorySeparatorChar + "migration-inputs" + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase));
    var report = SegOwnership.Analyze(segPath, runtimeFiles, history);
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
        foreach (var file in SegOwnership.ApplyRemoval(report, includeRuntimeIds: !methodsOnly)) Console.WriteLine("UPDATED: " + file);
        Console.WriteLine("HISTORICAL SOURCE: " + history);
    }
    return 0;
}
if (!string.Equals(args[0], "migrate-csharp", StringComparison.OrdinalIgnoreCase) || args.Length < 2)
{
    Console.Error.WriteLine("Usage: migrate-csharp <file.cs> --world WORLD [--output FILE] [--audit] [--emit-partial] [--method NAME] [--context-root DIR] [--dry-run]");
    return 2;
}
var input = Path.GetFullPath(args[1]); var output = (string?)null; var partial = false; var audit = false; var dryRun = false; string? method = null; string? world = null; string? contextRoot = null;
for (var i = 2; i < args.Length; i++)
    switch (args[i])
    {
        case "--output": output = Path.GetFullPath(args[++i]); break;
        case "--emit-partial": partial = true; break;
        case "--audit": audit = true; break;
        case "--dry-run": dryRun = true; break;
        case "--method": method = args[++i]; break;
        case "--world": world = args[++i]; break;
        case "--context-root": contextRoot = Path.GetFullPath(args[++i]); break;
        default: Console.Error.WriteLine($"Unknown option: {args[i]}"); return 2;
    }
if (string.IsNullOrWhiteSpace(world)) { Console.Error.WriteLine("Missing required option: --world WORLD"); return 2; }
var result = CSharpToSegTranspiler.Transpile(input, File.ReadAllText(input), partial, method, world, contextRoot);
Console.WriteLine($"file: {input}");
Console.WriteLine($"status: {(result.IsFullyTranslated ? "TRANSLATED" : partial ? "PARTIAL" : "UNSUPPORTED")}");
Console.WriteLine($"context-files: {result.ContextFileCount}");
Console.WriteLine($"context-parse-ms: {result.ContextParseMilliseconds}");
Console.WriteLine($"context-compilation-ms: {result.ContextCompilationMilliseconds}");
Console.WriteLine($"context-managed-memory: {result.ContextManagedMemoryBytes}");
Console.WriteLine($"project-discovery-ms: {result.ProjectDiscoveryMilliseconds}");
Console.WriteLine($"project-load-ms: {result.ProjectLoadMilliseconds}");
Console.WriteLine($"project-reference-load-ms: {result.ProjectReferenceLoadMilliseconds}");
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
