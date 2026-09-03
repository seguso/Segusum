using System.Text.RegularExpressions;
using System.Text.Json;

namespace Segusum.Translator.Core;

public sealed record SourceString(string Value, string RelativePath, int LineNumber);

public sealed class SourceDiscoveryOptions
{
    public IReadOnlyList<string> Include { get; init; } = Array.Empty<string>();
    public IReadOnlyList<string> Exclude { get; init; } = new[] { "bin", "obj", ".git", "node_modules", "generated" };

    public static SourceDiscoveryOptions Load(string root)
    {
        var path = Path.Combine(root, "translator.json");
        if (!File.Exists(path)) return new();
        var configured = JsonSerializer.Deserialize<SourceDiscoveryOptions>(File.ReadAllText(path)) ?? new();
        return new SourceDiscoveryOptions
        {
            Include = configured.Include,
            Exclude = new[] { "bin", "obj", ".git", "node_modules", "generated" }
                .Concat(configured.Exclude).Distinct(StringComparer.OrdinalIgnoreCase).ToArray()
        };
    }
}

public sealed class SourceStringExtractor
{
    private static readonly string[] Markers =
    {
        "dial(", "dial (", "nar(", "nar (", "narImg(", "narImg (",
        "narText(", "narText (", "narRoom(", "narRoom (",
        "using (namedCutScene", "using(namedCutScene", ".translatable()",
        "fatinaDiceQui(", "addHandlerCombine(", "addHandlerLook("
    };

    public IReadOnlyList<SourceString> Extract(string repositoryRoot,
        IEnumerable<string>? relativeFiles = null, SourceDiscoveryOptions? options = null)
    {
        var result = new List<SourceString>();
        options ??= new SourceDiscoveryOptions();
        var files = relativeFiles?.ToArray() ?? DiscoverFiles(repositoryRoot, options);
        foreach (var relativePath in files)
        {
            var fullPath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath))
                continue;

            var lines = File.ReadAllLines(fullPath);
            for (var index = 0; index < lines.Length; index++)
            {
                var line = lines[index].Trim();
                if (line.StartsWith("//", StringComparison.Ordinal) ||
                    !Markers.Any(marker => line.StartsWith(marker, StringComparison.Ordinal) ||
                                           line.Contains(marker, StringComparison.Ordinal)))
                    continue;

                var literal = ExtractLongestLiteral(line);
                if (literal is not null)
                    result.Add(new SourceString(ReplaceQuotes(literal), relativePath, index + 1));
            }
        }

        // The source sequence is canonical, but repeated use of one phrase is not
        // a second translation entry. Keep the first occurrence deterministically.
        return result.GroupBy(x => x.Value, StringComparer.Ordinal).Select(g => g.First()).ToList();
    }

    private static IReadOnlyList<string> DiscoverFiles(string root, SourceDiscoveryOptions options)
    {
        var all = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories);
        var candidates = options.Include.Count == 0 ? all : all.Where(path => options.Include.Any(pattern => Matches(root, path, pattern)));
        return candidates
            .Where(path => !options.Exclude.Any(exclude => IsExcluded(root, path, exclude)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(path => Path.GetRelativePath(root, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static bool IsExcluded(string root, string path, string exclude)
    {
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        var normalized = exclude.Replace('\\', '/').Trim('/');
        return relative.Split('/').Any(part => part.Equals(normalized, StringComparison.OrdinalIgnoreCase)) || Matches(root, path, normalized);
    }

    private static bool Matches(string root, string path, string pattern)
    {
        var relative = Path.GetRelativePath(root, path).Replace('\\', '/');
        var normalized = pattern.Replace('\\', '/').Trim('/');
        var regex = "^" + Regex.Escape(normalized).Replace("\\*", ".*").Replace("\\?", ".") + "$";
        return Regex.IsMatch(relative, regex, RegexOptions.IgnoreCase) || Regex.IsMatch(relative, regex.TrimEnd('$') + "/.*$", RegexOptions.IgnoreCase);
    }

    internal static string ReplaceQuotes(string value) => value.Replace("\"", "''", StringComparison.Ordinal);

    private static string? ExtractLongestLiteral(string line)
    {
        var matches = Regex.Matches(line, "\\\"(?:\\\\.|[^\\\"\\\\])*\\\"");
        var match = matches.Cast<Match>().OrderByDescending(x => x.Length).FirstOrDefault();
        if (match is null) return null;
        var value = match.Value[1..^1];
        return value.Replace("\\\"", "\"", StringComparison.Ordinal);
    }
}
