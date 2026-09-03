using System.Xml.Linq;
using Segusum.Translator.Core;

namespace Segusum.Translator.Cli;

internal static class Program
{
    private static int Main(string[] args)
    {
        try
        {
            var options = Options.Parse(args);
            if (options.Help) { PrintHelp(); return 0; }
            if (options.Command != "sync") throw new ArgumentException("Use: sync [--root PATH] [--lang LANG|all] [--dry-run|--check]");

            var root = RepositoryLocator.Find(options.Root);
            var catalogs = TranslationCatalogFile.Discover(root);
            var languages = options.Language == "all" ? catalogs.Select(x => x.Language).ToArray() : new[] { options.Language };
            var anyChanged = false;
            foreach (var language in languages)
            {
                var existing = catalogs.FirstOrDefault(x => x.Language.Equals(language, StringComparison.OrdinalIgnoreCase));
                var operation = new TranslationCatalogOperations();
                var prepared = existing is null ? operation.Create(root, language, !options.DryRun && !options.Check) : operation.Synchronize(root, existing.Path, !options.DryRun && !options.Check);
                anyChanged |= prepared.Result.Changed;
                PrintStatistics(language, prepared.Path, prepared.Result);
                if (prepared.Result.Statistics.ChangedPairs.Count > 0)
                    foreach (var pair in prepared.Result.Statistics.ChangedPairs.Take(20))
                        Console.WriteLine($"  changed: {pair.OldValue} -> {pair.NewValue} ({pair.Similarity:0.00})");
                // Create is preview-only in dry-run/check mode: no file is written.
                if (existing is null && (options.DryRun || options.Check))
                    Console.WriteLine("  new catalogue preview only; no file written");
            }
            return options.Check && anyChanged ? 2 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"translator: {ex.Message}");
            return 1;
        }
    }

    private static void PrintStatistics(string language, string path, SyncResult result)
    {
        var s = result.Statistics;
        Console.WriteLine($"{language}: {path}");
        Console.WriteLine($"  unchanged={s.Unchanged}, new={s.New}, modified/replaced={s.ModifiedOrReplaced}, " +
                          $"translated obsolete preserved={s.PreservedTranslatedObsolete}, '+' obsolete removed={s.RemovedUntranslatedObsolete}, reactivated={s.Reactivated}, changed={result.Changed}");
    }


    private static void PrintHelp() => Console.WriteLine("dotnet run --project Segusum.Translator.Cli -- sync [--root PATH] [--lang LANG|all] [--dry-run|--check]");

    private sealed record Options(string Command, string? Root, string Language, bool DryRun, bool Check, bool Help)
    {
        public static Options Parse(string[] args)
        {
            var command = args.FirstOrDefault(x => !x.StartsWith("--", StringComparison.Ordinal)) ?? "sync";
            string? root = null; var language = "all"; var dry = false; var check = false; var help = false;
            for (var i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--root": root = args[++i]; break;
                    case "--lang": language = args[++i].ToLowerInvariant(); break;
                    case "--dry-run": dry = true; break;
                    case "--check": check = true; break;
                    case "--help": case "-h": help = true; break;
                }
            }
            if (language == "all" || !language.All(char.IsLetterOrDigit) && !language.Contains('-'))
                if (language != "all") throw new ArgumentException("--lang must be a catalog language code or all");
            if (dry && check) throw new ArgumentException("--dry-run and --check are mutually exclusive");
            return new Options(command, root, language, dry, check, help);
        }
    }
}

internal static class RepositoryLocator
{
    public static string Find(string? requested)
    {
        var start = Path.GetFullPath(requested ?? Directory.GetCurrentDirectory());
        if (File.Exists(start)) start = Path.GetDirectoryName(start)!;
            for (var current = new DirectoryInfo(start); current is not null; current = current.Parent)
                if (Directory.Exists(Path.Combine(current.FullName, ".git"))) return current.FullName;
        throw new DirectoryNotFoundException("Repository root not found. Pass --root PATH.");
    }
}
