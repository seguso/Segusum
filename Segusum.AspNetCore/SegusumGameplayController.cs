using Microsoft.AspNetCore.Mvc;
using Segusum.WebClient;

namespace Segusum.AspNetCore;

[Route("")]
public sealed class SegusumGameplayController : Controller
{
    private readonly SegusumOptions options;

    public SegusumGameplayController(SegusumOptions options) => this.options = options;

    [HttpGet("")]
    [HttpGet("{language:regex(^en|it|de$)}")]
    public IActionResult Index(string? language = null)
    {
        var lang = language is "it" or "de" ? language : "en";
        var prefix = string.Concat(Request.Scheme, "://", Request.Host, Request.PathBase);
        return View("~/Views/Shared/Index.cshtml", new SegusumHomeModel
        {
            ApiPrefix = prefix,
            Language = lang,
            InventoryIconsPath = options.InventoryIconsPath,
            GameAssetPrefix = options.GameAssetPrefix,
            Title = options.GameTitle,
            Credits = options.Credits
        });
    }
}
