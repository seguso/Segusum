using Microsoft.Extensions.DependencyInjection;
using Segusum.AspNetCore;

namespace Segusum.Tests;

public sealed class ThinkingImageContractTests
{
    [Fact]
    public void AddSegusum_rejects_a_game_without_ui_assets()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddSegusum(options =>
        {
            options.WorldFactory = (_, _) => null!;
        }));

        Assert.Contains("UiAssets", exception.Message);
    }

    [Fact]
    public void AddSegusum_accepts_the_game_thinking_image_path()
    {
        var services = new ServiceCollection();

        services.AddSegusum(options =>
        {
            options.WorldFactory = (_, _) => null!;
            options.UiAssets = Assets("img/character-thinking.png");
        });

        var options = services.BuildServiceProvider().GetRequiredService<SegusumOptions>();
        Assert.Equal("img/character-thinking.png", options.UiAssets!.Thinking);
    }

    [Fact]
    public void AddSegusum_reports_the_missing_ui_asset_name()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddSegusum(options =>
        {
            options.WorldFactory = (_, _) => null!;
            options.UiAssets = Assets("img/character-thinking.png", hand: " ");
        }));

        Assert.Contains("Hand", exception.Message);
    }

    [Fact]
    public void AddSegusum_rejects_missing_favicons()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddSegusum(options =>
        {
            options.WorldFactory = (_, _) => null!;
            options.UiAssets = Assets("img/character-thinking.png", missingFavicons: true);
        }));

        Assert.Contains("Favicons", exception.Message);
    }

    [Fact]
    public void AddSegusum_reports_the_missing_favicon_property_name()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddSegusum(options =>
        {
            options.WorldFactory = (_, _) => null!;
            options.UiAssets = Assets("img/character-thinking.png", missingFavicon: true);
        }));

        Assert.Contains("Favicons.Apple57", exception.Message);
    }

    private static Segusum.WebClient.SegusumUiAssets Assets(
        string thinking,
        string hand = "hand.png",
        Segusum.WebClient.SegusumFaviconAssets? favicons = null,
        bool missingFavicon = false,
        bool missingFavicons = false) => new()
    {
        Hand = hand, Feet = "feet.png", Eyes = "eyes.png", Options = "options.png",
        Talk = "talk.png", TalkAvailable = "talk-active.png", Objectives = "objectives.png",
        ObjectivesAvailable = "objectives-active.png", Thinking = thinking, Target = "target.png",
        Exit = "exit.png", ExitDown = "exit-down.png", Separator = "separator.png",
        Play = "play.png", PlayDisabled = "play-disabled.png", InventoryObjectIconsPath = "objects"
        , Favicons = missingFavicons ? null! : favicons ?? Favicons(missingFavicon)
    };

    private static Segusum.WebClient.SegusumFaviconAssets Favicons(bool missingFirst = false) => new()
    {
        Apple57 = missingFirst ? " " : "apple57.png", Apple60 = "apple60.png", Apple72 = "apple72.png", Apple76 = "apple76.png",
        Apple114 = "apple114.png", Apple120 = "apple120.png", Apple144 = "apple144.png", Apple152 = "apple152.png",
        Apple180 = "apple180.png", Android192 = "android192.png", Favicon32 = "favicon32.png",
        Favicon96 = "favicon96.png", Favicon16 = "favicon16.png", MsTile144 = "mstile144.png", Manifest = "manifest.json"
    };
}
