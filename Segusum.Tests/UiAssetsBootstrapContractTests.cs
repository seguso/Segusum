using System.Text.Json;
using Segusum.WebClient;

namespace Segusum.Tests;

public sealed class UiAssetsBootstrapContractTests
{
    [Fact]
    public void Bootstrap_assets_are_explicit_configured_paths()
    {
        var assets = new SegusumUiAssets
        {
            Hand = "game/hand.svg", Feet = "game/feet.svg", Eyes = "game/eyes.svg",
            Options = "game/options.svg", Talk = "game/talk.svg", TalkAvailable = "game/talk-active.svg",
            Objectives = "game/objectives.svg", ObjectivesAvailable = "game/objectives-active.svg",
            Thinking = "game/thinking.svg", Target = "game/target.svg", Exit = "game/exit.svg",
            ExitDown = "game/exit-down.svg", Separator = "game/separator.svg", Play = "game/play.svg",
            PlayDisabled = "game/play-disabled.svg", InventoryObjectIconsPath = "game/objects"
        };

        assets.Validate();
        var bootstrap = JsonSerializer.Serialize(new
        {
            assets = new
            {
                hand = assets.Hand,
                feet = assets.Feet,
                eyes = assets.Eyes,
                options = assets.Options,
                thinking = assets.Thinking,
                inventoryObjectIconsPath = assets.InventoryObjectIconsPath
            }
        });

        Assert.Contains("game/hand.svg", bootstrap);
        Assert.Contains("game/thinking.svg", bootstrap);
        Assert.Contains("game/objects", bootstrap);
        Assert.DoesNotContain("_content/Segusum.WebClient/assets/icons", bootstrap);
    }
}
