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
        Play = "sample.svg", PlayDisabled = "sample.svg", InventoryObjectIconsPath = "sample.svg"
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
