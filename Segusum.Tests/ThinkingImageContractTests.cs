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

    private static Segusum.WebClient.SegusumUiAssets Assets(string thinking, string hand = "hand.png") => new()
    {
        Hand = hand, Feet = "feet.png", Eyes = "eyes.png", Options = "options.png",
        Talk = "talk.png", TalkAvailable = "talk-active.png", Objectives = "objectives.png",
        ObjectivesAvailable = "objectives-active.png", Thinking = thinking, Target = "target.png",
        Exit = "exit.png", ExitDown = "exit-down.png", Separator = "separator.png",
        Play = "play.png", PlayDisabled = "play-disabled.png", InventoryObjectIconsPath = "objects"
    };
}
