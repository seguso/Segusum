# Web client standard

`Segusum.AspNetCore` porta transitivamente `Segusum.WebClient`, una Razor
Class Library che contiene la home gameplay, il runtime JavaScript, lo skin
di default e le icone standard. Un gioco deve avere solo un riferimento
NuGet diretto a `Segusum.AspNetCore`.

Gli asset del package sono serviti dal sistema ASP.NET Core static web assets
con namespace `/_content/Segusum.WebClient/`. Gli asset del gioco restano nel
proprio `wwwroot` e non entrano in collisione con quelli del motore.

```csharp
var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseStaticWebAssets();
builder.Services.AddSegusum(options =>
{
    options.WorldFactory = (language, tutorial) => new MyGame.World(language);
    options.GameTitle = "Il mio gioco";
    options.InventoryIconsPath = "_content/Segusum.WebClient/assets/icons";
});
builder.Services.AddSegusumStorage(options => options.UseFile("data/game.json"));
var app = builder.Build();
app.UseSegusumInfrastructure();
app.UseStaticFiles();
app.MapControllers();
app.Run();
```

`samples/MinimalGame` è il test di consumo esclusivamente da pacchetto.
