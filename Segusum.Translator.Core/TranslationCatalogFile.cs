using System.Text.RegularExpressions;

namespace Segusum.Translator.Core;

public sealed record TranslationCatalogInfo(string Language, string Path);

public static class TranslationCatalogFile
{
    public static IReadOnlyList<TranslationCatalogInfo> Discover(string root)
    {
        return Directory.Exists(root) ? Directory.EnumerateFiles(root, "transl_*.xml", SearchOption.AllDirectories)
            .Where(path => !Path.GetRelativePath(root, path).Split(new[] { Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar }, StringSplitOptions.RemoveEmptyEntries)
                .Any(segment => segment.Equals("bin", StringComparison.OrdinalIgnoreCase) || segment.Equals("obj", StringComparison.OrdinalIgnoreCase)))
            .Select(path => (path, match: Regex.Match(Path.GetFileName(path), "^transl_(?<lang>[A-Za-z0-9-]+)\\.xml$")))
            .Where(x => x.match.Success).Select(x => new TranslationCatalogInfo(x.match.Groups["lang"].Value, x.path))
            .OrderBy(x => x.Language, StringComparer.OrdinalIgnoreCase).ToArray() : Array.Empty<TranslationCatalogInfo>();
    }
}
