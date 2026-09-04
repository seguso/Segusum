using System.Xml.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using Segusum.WebClient;

namespace Segusum.Tests;

public sealed class ClientCatalogTests
{
    [Fact]
    public void EngineCatalogHasStableUniqueKeysAndCanonicalSources()
    {
        var catalog = SegusumClientCatalog.Load("en");
        Assert.NotEmpty(catalog);
        Assert.Equal(catalog.Count, catalog.Values.Select(x => x.Key).Distinct(StringComparer.Ordinal).Count());
        Assert.All(catalog.Values, value => Assert.False(string.IsNullOrWhiteSpace(value.Source)));
    }

    [Fact]
    public void DuplicateEngineKeyIsRejected()
    {
        var document = XDocument.Parse("<root><str key='same' orig='A' transl='A'/><str key='same' orig='B' transl='B'/></root>");
        Assert.Throws<InvalidOperationException>(() => SegusumClientCatalog.Parse(document));
    }

    [Fact]
    public void MarkupAndLiteralLookupsExistInEngineCatalog()
    {
        var root = FindRepositoryRoot();
        var source = File.ReadAllText(Path.Combine(root, "Segusum.WebClient", "Views", "Shared", "Index.cshtml"))
            + File.ReadAllText(Path.Combine(root, "Segusum.WebClient", "wwwroot", "js", "main43.js"));
        var keys = Regex.Matches(source, @"my_transl=""([A-Za-z0-9_]+)""|[""']([A-Za-z0-9_]+)[""']\.tr\(\)")
            .Select(x => x.Groups[1].Success ? x.Groups[1].Value : x.Groups[2].Value)
            .ToHashSet(StringComparer.Ordinal);
        var defaults = SegusumClientCatalog.Load("en");
        Assert.All(keys, key => Assert.Contains(key, defaults.Keys));
    }

    [Fact]
    public void BootstrapSerializationEscapesUntrustedStringContent()
    {
        var json = JsonSerializer.Serialize(new
        {
            language = "en",
            strings = new Dictionary<string, string>
            {
                ["quote"] = "It's \"safe\" <script>& {1}"
            }
        });

        Assert.DoesNotContain("<script>", json, StringComparison.Ordinal);
        Assert.Contains("\\u003Cscript\\u003E", json, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("{1}", json, StringComparison.Ordinal);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Segusum.slnx")))
            directory = directory.Parent;
        return directory?.FullName ?? throw new InvalidOperationException("Segusum repository root not found.");
    }
}
