using Segusum.AspNetCore;
using Segusum.WebClient;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();
builder.Services.AddSegusum(options =>
{
    options.WorldFactory = (language, tutorialMode) => new MinimalGame.World(language);
    options.GameTitle = "Minimal Segusum Game";
    options.UiAssets = new SegusumUiAssets
    {
        Hand = "sample.svg", Feet = "sample.svg", Eyes = "sample.svg", Options = "sample.svg",
        Talk = "sample.svg", TalkAvailable = "sample.svg", Objectives = "sample.svg",
        ObjectivesAvailable = "sample.svg", Thinking = "sample.svg", Target = "sample.svg",
        Exit = "sample.svg", ExitDown = "sample.svg", Separator = "sample.svg",
        Play = "sample.svg", PlayDisabled = "sample.svg", InventoryObjectIconsPath = "sample.svg",
        Favicons = new SegusumFaviconAssets
        {
            Apple57 = "sample.svg", Apple60 = "sample.svg", Apple72 = "sample.svg", Apple76 = "sample.svg",
            Apple114 = "sample.svg", Apple120 = "sample.svg", Apple144 = "sample.svg", Apple152 = "sample.svg",
            Apple180 = "sample.svg", Android192 = "sample.svg", Favicon32 = "sample.svg",
            Favicon96 = "sample.svg", Favicon16 = "sample.svg", MsTile144 = "sample.svg", Manifest = "sample.svg"
        }
    };
    options.OverrideClientString("saveGame", "Store progress");
});
builder.Services.AddSegusumStorage(options => options.UseInMemory("minimal-game"));
var app = builder.Build();
app.UseSegusumInfrastructure();
app.UseStaticFiles();
app.UseRouting();
app.MapControllers();
app.Run();
