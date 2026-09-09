using Microsoft.Extensions.DependencyInjection;
using Segusum.AspNetCore;

namespace Segusum.Tests;

public sealed class ThinkingImageContractTests
{
    [Fact]
    public void AddSegusum_rejects_a_game_without_thinking_image()
    {
        var services = new ServiceCollection();

        var exception = Assert.Throws<InvalidOperationException>(() => services.AddSegusum(options =>
        {
            options.WorldFactory = (_, _) => null!;
        }));

        Assert.Contains("ThinkingImagePath", exception.Message);
    }

    [Fact]
    public void AddSegusum_accepts_the_game_thinking_image_path()
    {
        var services = new ServiceCollection();

        services.AddSegusum(options =>
        {
            options.WorldFactory = (_, _) => null!;
            options.ThinkingImagePath = "img/character-thinking.png";
        });

        var options = services.BuildServiceProvider().GetRequiredService<SegusumOptions>();
        Assert.Equal("img/character-thinking.png", options.ThinkingImagePath);
    }
}
