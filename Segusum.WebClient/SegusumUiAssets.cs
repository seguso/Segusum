namespace Segusum.WebClient;

public sealed class SegusumUiAssets
{
    public required string Hand { get; init; }
    public required string Feet { get; init; }
    public required string Eyes { get; init; }
    public required string Options { get; init; }
    public required string Talk { get; init; }
    public required string TalkAvailable { get; init; }
    public required string Objectives { get; init; }
    public required string ObjectivesAvailable { get; init; }
    public required string Thinking { get; init; }
    public required string Target { get; init; }
    public required string Exit { get; init; }
    public required string ExitDown { get; init; }
    public required string Separator { get; init; }
    public required string Play { get; init; }
    public required string PlayDisabled { get; init; }
    public required string InventoryObjectIconsPath { get; init; }

    public void Validate()
    {
        var missing = typeof(SegusumUiAssets)
            .GetProperties()
            .Where(property => string.IsNullOrWhiteSpace((string?)property.GetValue(this)))
            .Select(property => property.Name)
            .ToArray();

        if (missing.Length > 0)
            throw new InvalidOperationException($"Segusum UiAssets mancanti: {string.Join(", ", missing)}.");
    }
}
