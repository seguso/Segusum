using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

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
    public IReadOnlyList<SourceString> Extract(string repositoryRoot,
        IEnumerable<string>? relativeFiles = null, SourceDiscoveryOptions? options = null)
    {
        options ??= new SourceDiscoveryOptions();
        var result = new List<SourceString>();
        var files = relativeFiles?.ToArray() ?? DiscoverFiles(repositoryRoot, options);
        foreach (var relativePath in files)
        {
            var fullPath = Path.Combine(repositoryRoot, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath)) continue;
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(fullPath), path: relativePath);
            var visitor = new TranslatingInvocationVisitor(relativePath);
            visitor.Visit(tree.GetRoot());
            result.AddRange(visitor.Results);
        }
        return result.GroupBy(x => x.Value, StringComparer.Ordinal).Select(x => x.First()).ToList();
    }

    private static IReadOnlyList<string> DiscoverFiles(string root, SourceDiscoveryOptions options)
    {
        var all = Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories);
        var candidates = options.Include.Count == 0 ? all : all.Where(path => options.Include.Any(pattern => Matches(root, path, pattern)));
        return candidates.Where(path => !options.Exclude.Any(exclude => IsExcluded(root, path, exclude)))
            .Distinct(StringComparer.OrdinalIgnoreCase).Select(path => Path.GetRelativePath(root, path))
            .OrderBy(path => path, StringComparer.OrdinalIgnoreCase).ToArray();
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
        var regex = "^" + Regex.Escape(normalized).Replace("\\\\*", ".*").Replace("\\\\?", ".") + "$";
        return Regex.IsMatch(relative, regex, RegexOptions.IgnoreCase) || Regex.IsMatch(relative, regex.TrimEnd('$') + "/.*$", RegexOptions.IgnoreCase);
    }

    private sealed class TranslatingInvocationVisitor(string relativePath) : CSharpSyntaxWalker
    {
        public List<SourceString> Results { get; } = new();

        public override void VisitInvocationExpression(InvocationExpressionSyntax node)
        {
            if (node.Expression is MemberAccessExpressionSyntax member && member.Name.Identifier.ValueText == "translatable")
                Add(member.Expression);
            else if (node.Expression is MemberAccessExpressionSyntax clientOverride &&
                     clientOverride.Name.Identifier.ValueText == "OverrideClientString")
                Add(node.ArgumentList.Arguments.FirstOrDefault(x =>
                    x.NameColon?.Name.Identifier.ValueText == "source")?.Expression
                    ?? node.ArgumentList.Arguments.ElementAtOrDefault(1)?.Expression);
            else if (node.Expression is IdentifierNameSyntax identifier)
            {
                var argument = identifier.Identifier.ValueText switch
                {
                    "dial" => TextArgument(node, 1, "testo"),
                    "nar" or "narText" or "narImg" or "narRoom" => TextArgument(node, 0, "s"),
                    "addHandlerCombine" => TextArgument(node, 2, "fullSentenceUntransl", "dynamicSentenceUntransl"),
                    "addHandlerLook" => FirstTextArgumentAfter(node, 0, "dynamicSentence", "sentence"),
                    "fatinaDiceQui" => TextArgument(node, 2, "frase"),
                    _ => null
                };
                if (argument is not null) Add(argument);
            }
            base.VisitInvocationExpression(node);
        }

        private static ExpressionSyntax? TextArgument(InvocationExpressionSyntax node, int ordinal, params string[] names)
        {
            var named = node.ArgumentList.Arguments.FirstOrDefault(x => x.NameColon is not null && names.Contains(x.NameColon.Name.Identifier.ValueText, StringComparer.Ordinal));
            return named?.Expression ?? node.ArgumentList.Arguments.ElementAtOrDefault(ordinal)?.Expression;
        }

        private static ExpressionSyntax? FirstTextArgumentAfter(InvocationExpressionSyntax node, int firstIndex, params string[] names)
        {
            var named = node.ArgumentList.Arguments.FirstOrDefault(x => x.NameColon is not null && names.Contains(x.NameColon.Name.Identifier.ValueText, StringComparer.Ordinal));
            return named?.Expression ?? node.ArgumentList.Arguments.Skip(firstIndex + 1).Select(x => x.Expression).FirstOrDefault(IsStringLiteral);
        }

        private void Add(ExpressionSyntax? expression)
        {
            expression = Unwrap(expression);
            if (expression is not LiteralExpressionSyntax literal || !literal.IsKind(SyntaxKind.StringLiteralExpression)) return;
            var line = literal.GetLocation().GetLineSpan().StartLinePosition.Line + 1;
            Results.Add(new SourceString(ReplaceQuotes(literal.Token.ValueText), relativePath, line));
        }

        private static string ReplaceQuotes(string value) => value.Replace("\"", "''", StringComparison.Ordinal);

        private static bool IsStringLiteral(ExpressionSyntax expression) => Unwrap(expression) is LiteralExpressionSyntax x && x.IsKind(SyntaxKind.StringLiteralExpression);
        private static ExpressionSyntax? Unwrap(ExpressionSyntax? expression) => expression is ParenthesizedExpressionSyntax p ? Unwrap(p.Expression) : expression;
    }
}
