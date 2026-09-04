using Segusum.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();
builder.Services.AddSegusum(options =>
{
    options.WorldFactory = (language, tutorialMode) => new MinimalGame.World(language);
    options.GameTitle = "Minimal Segusum Game";
});
builder.Services.AddSegusumStorage(options => options.UseInMemory("minimal-game"));
var app = builder.Build();
app.UseSegusumInfrastructure();
app.UseStaticFiles();
app.UseRouting();
app.MapControllers();
app.Run();
