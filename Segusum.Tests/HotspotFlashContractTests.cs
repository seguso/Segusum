using System.IO;

namespace Segusum.Tests;

public sealed class HotspotFlashContractTests
{
    [Fact]
    public void Eye_click_uses_a_single_visible_target_flash_without_recreating_hotspots()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Segusum.WebClient", "wwwroot", "js", "main43.js")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        var javascript = File.ReadAllText(Path.Combine(directory!.FullName, "Segusum.WebClient", "wwwroot", "js", "main43.js"));

        Assert.Contains("$(\".eyeIcon\").click", javascript);
        Assert.Contains("flashVisibleTargetsOnce();", javascript);
        Assert.Contains("targets.stop(true, true).show().css(\"opacity\", 1).fadeOut(150).fadeIn(150);", javascript);
        Assert.Contains(".imgTarget, .imgTargetExit, .imgTargetExitDown", javascript);
        Assert.DoesNotContain("mostraHotspot();\n    });\n\n    if (is_touch_device1())", javascript);
        Assert.Contains("gClientScriptVersion = \"92\"", javascript);
    }
}
