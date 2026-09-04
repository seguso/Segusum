using Segusum.Scripting.Core;

namespace Segusum.Translator.Core;

/// <summary>Extracts translatable literals from the DSL AST, without inspecting generated C#.</summary>
public sealed class DslSourceStringExtractor
{
    public IReadOnlyList<SourceString> Extract(string repositoryRoot, IEnumerable<string>? relativeFiles = null)
    {
        var files = relativeFiles?.ToArray() ?? Directory.EnumerateFiles(repositoryRoot, "*.seg", SearchOption.AllDirectories)
            .Where(x => !x.Split(Path.DirectorySeparatorChar).Any(p => p is "bin" or "obj" or ".git"))
            .Select(x => Path.GetRelativePath(repositoryRoot, x)).ToArray();
        var result = new List<SourceString>();
        foreach (var relative in files)
        {
            var path = Path.Combine(repositoryRoot, relative);
            if (!File.Exists(path)) continue;
            var lines = File.ReadAllLines(path);
            var parsed = DslParser.Parse(new DslSource(relative, File.ReadAllText(path))).Document;
            foreach (var handler in parsed.Declarations.OfType<HandlerDeclaration>())
            {
                if (handler.Phrase is not null) result.Add(new SourceString(Unquote(handler.Phrase), relative, handler.Line));
                foreach (var line in handler.Body.Where(x => x.StartsWith("nar ") || x.Contains(":")))
                    AddLine(result, line, relative, handler.Line);
            }
            foreach (var element in parsed.Declarations.OfType<CycleElementDeclaration>())
                foreach (var line in element.Body.Where(x => x.StartsWith("nar ") || x.Contains(":"))) AddLine(result, line, relative, element.Line);
            foreach (var function in parsed.Declarations.OfType<FunctionDeclaration>())
                foreach (var line in function.Body.Where(x => x.StartsWith("nar ") || x.Contains(":"))) AddLine(result, line, relative, function.Line);
        }
        return result.GroupBy(x => x.Value, StringComparer.Ordinal).Select(x => x.First()).ToArray();
    }
    private static void AddLine(List<SourceString> result, string line, string path, int number)
    {
        var value = line.StartsWith("nar ") ? line.Substring(4).Trim() : line.Substring(line.IndexOf(':') + 1).Trim();
        if (value.StartsWith("\"") && value.EndsWith("\"")) result.Add(new SourceString(Unquote(value), path, number));
    }
    private static string Unquote(string value) => value.Length >= 2 && value[0] == '"' && value[value.Length - 1] == '"' ? value.Substring(1, value.Length - 2).Replace("\"", "''") : value;
}
