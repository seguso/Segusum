namespace Segusum.WebClient;

public sealed class SegusumHomeModel
{
    public string ApiPrefix { get; init; } = "";
    public string Language { get; init; } = "en";
    public int? GameId { get; init; }
    public string InventoryIconsPath { get; init; } = "_content/Segusum.WebClient/assets/icons";
    public string GameAssetPrefix { get; init; } = "";
    public string Title { get; init; } = "Segusum game";
    public string Credits { get; init; } = "A game made with Segusum.";
}
