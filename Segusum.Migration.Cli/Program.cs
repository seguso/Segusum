using Segusum.Scripting.Tooling;

if (args.Length == 0 || args[0] is "--help" or "-h")
{
    Console.WriteLine("segusum migrate-csharp <file.cs> [--output FILE] [--audit] [--emit-partial] [--method NAME] [--dry-run]");
    return 0;
}
if (!string.Equals(args[0], "migrate-csharp", StringComparison.OrdinalIgnoreCase) || args.Length < 2)
{
    Console.Error.WriteLine("Usage: migrate-csharp <file.cs> [--output FILE] [--audit] [--emit-partial] [--method NAME] [--dry-run]");
    return 2;
}
var input = Path.GetFullPath(args[1]); var output = (string?)null; var partial = false; var audit = false; var dryRun = false; string? method = null;
for (var i = 2; i < args.Length; i++)
    switch (args[i])
    {
        case "--output": output = Path.GetFullPath(args[++i]); break;
        case "--emit-partial": partial = true; break;
        case "--audit": audit = true; break;
        case "--dry-run": dryRun = true; break;
        case "--method": method = args[++i]; break;
        default: Console.Error.WriteLine($"Unknown option: {args[i]}"); return 2;
    }
var result = CSharpToSegTranspiler.Transpile(input, File.ReadAllText(input), partial, method);
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
