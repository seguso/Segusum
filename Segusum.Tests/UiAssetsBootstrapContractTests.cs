using System.Text.Json;
using System.IO;
using Segusum.WebClient;

namespace Segusum.Tests;

public sealed class UiAssetsBootstrapContractTests
{
    [Fact]
    public void Razor_does_not_reconstruct_favicon_paths()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Segusum.WebClient", "Views", "Shared", "Index.cshtml")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var razor = File.ReadAllText(Path.Combine(directory!.FullName, "Segusum.WebClient", "Views", "Shared", "Index.cshtml"));

        Assert.DoesNotContain("img/favicon", razor);
        Assert.DoesNotContain("apple-icon-", razor);
        Assert.DoesNotContain("android-icon-", razor);
        Assert.DoesNotContain("favicon-32x32.png", razor);
        Assert.DoesNotContain("manifest.json", razor);
        Assert.Contains("Model.UiAssets.Favicons.Manifest", razor);
    }

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
            PlayDisabled = "game/play-disabled.svg", InventoryObjectIconsPath = "game/objects",
            Favicons = new SegusumFaviconAssets
            {
                Apple57 = "game/apple57.svg", Apple60 = "game/apple60.svg", Apple72 = "game/apple72.svg",
                Apple76 = "game/apple76.svg", Apple114 = "game/apple114.svg", Apple120 = "game/apple120.svg",
                Apple144 = "game/apple144.svg", Apple152 = "game/apple152.svg", Apple180 = "game/apple180.svg",
                Android192 = "game/android192.svg", Favicon32 = "game/favicon32.svg", Favicon96 = "game/favicon96.svg",
                Favicon16 = "game/favicon16.svg", MsTile144 = "game/mstile144.svg", Manifest = "game/manifest.json"
            }
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
                inventoryObjectIconsPath = assets.InventoryObjectIconsPath,
                talk = assets.Talk,
                talkAvailable = assets.TalkAvailable,
                objectives = assets.Objectives,
                objectivesAvailable = assets.ObjectivesAvailable,
                target = assets.Target,
                exit = assets.Exit,
                exitDown = assets.ExitDown,
                separator = assets.Separator,
                play = assets.Play,
                playDisabled = assets.PlayDisabled,
                favicons = new
                {
                    apple57 = assets.Favicons.Apple57,
                    apple180 = assets.Favicons.Apple180,
                    android192 = assets.Favicons.Android192,
                    favicon32 = assets.Favicons.Favicon32,
                    favicon96 = assets.Favicons.Favicon96,
                    favicon16 = assets.Favicons.Favicon16,
                    msTile144 = assets.Favicons.MsTile144,
                    manifest = assets.Favicons.Manifest
                }
            }
        });

        Assert.Contains("game/hand.svg", bootstrap);
        Assert.Contains("game/thinking.svg", bootstrap);
        Assert.Contains("game/objects", bootstrap);
        Assert.Contains("game/talk-active.svg", bootstrap);
        Assert.Contains("game/objectives-active.svg", bootstrap);
        Assert.Contains("game/play-disabled.svg", bootstrap);
        Assert.Contains("game/manifest.json", bootstrap);
        Assert.DoesNotContain("_content/Segusum.WebClient/assets/icons", bootstrap);
    }
}
