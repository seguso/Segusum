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
            var extractor = new SourceStringExtractor();
            var sources = extractor.Extract(root, options: SourceDiscoveryOptions.Load(root));
            var catalogs = TranslationCatalogFile.Discover(root);
            var languages = options.Language == "all" ? catalogs.Select(x => x.Language).ToArray() : new[] { options.Language };
            var anyChanged = false;
            foreach (var language in languages)
            {
                var path = catalogs.FirstOrDefault(x => x.Language.Equals(language, StringComparison.OrdinalIgnoreCase))?.Path
                    ?? throw new FileNotFoundException($"Translation catalogue not found for language: {language}");
                if (!File.Exists(path)) throw new FileNotFoundException($"Translation catalogue not found: {path}");
                var current = XDocument.Load(path, LoadOptions.PreserveWhitespace);
                var result = new TranslationCatalogSynchronizer().Synchronize(sources.Select(x => x.Value).ToList(), current);
                anyChanged |= result.Changed;
                PrintStatistics(language, path, sources.Count, result);
                if (result.Statistics.ChangedPairs.Count > 0)
                    foreach (var pair in result.Statistics.ChangedPairs.Take(20))
                        Console.WriteLine($"  changed: {pair.OldValue} -> {pair.NewValue} ({pair.Similarity:0.00})");
                if (!options.DryRun && !options.Check && result.Changed)
                    CatalogFileStore.SaveAtomic(path, result.Document, CatalogFileStore.Fingerprint(path));
            }
            return options.Check && anyChanged ? 2 : 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"translator: {ex.Message}");
            return 1;
        }
    }

    private static void PrintStatistics(string language, string path, int sourceCount, SyncResult result)
    {
        var s = result.Statistics;
        Console.WriteLine($"{language}: {path}");
        Console.WriteLine($"  source={sourceCount}, unchanged={s.Unchanged}, new={s.New}, modified/replaced={s.ModifiedOrReplaced}, " +
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
