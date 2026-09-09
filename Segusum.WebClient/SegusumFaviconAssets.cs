namespace Segusum.WebClient;

public sealed class SegusumFaviconAssets
{
    public required string Apple57 { get; init; }
    public required string Apple60 { get; init; }
    public required string Apple72 { get; init; }
    public required string Apple76 { get; init; }
    public required string Apple114 { get; init; }
    public required string Apple120 { get; init; }
    public required string Apple144 { get; init; }
    public required string Apple152 { get; init; }
    public required string Apple180 { get; init; }
    public required string Android192 { get; init; }
    public required string Favicon32 { get; init; }
    public required string Favicon96 { get; init; }
    public required string Favicon16 { get; init; }
    public required string MsTile144 { get; init; }
    public required string Manifest { get; init; }

    public IEnumerable<string> MissingProperties() => typeof(SegusumFaviconAssets)
        .GetProperties()
        .Where(property => string.IsNullOrWhiteSpace((string?)property.GetValue(this)))
        .Select(property => $"Favicons.{property.Name}");
}
