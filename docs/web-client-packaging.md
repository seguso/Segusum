# Web client standard

`Segusum.AspNetCore` porta transitivamente `Segusum.WebClient`, una Razor
Class Library che contiene la home gameplay e il runtime JavaScript. Gli asset
visuali usati dall'interfaccia sono forniti esplicitamente dal gioco: il
motore non installa più una cartella di icone con nomi convenzionali.

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
    options.UiAssets = new SegusumUiAssets
    {
        Hand = "img/my-game/hand.png",
        Feet = "img/my-game/feet.png",
        Eyes = "img/my-game/eyes.png",
        Options = "img/my-game/options.png",
        Talk = "img/my-game/talk.png",
        TalkAvailable = "img/my-game/talk-active.png",
        Objectives = "img/my-game/objectives.png",
        ObjectivesAvailable = "img/my-game/objectives-active.png",
        Thinking = "img/my-game/thinking.png",
        Target = "img/my-game/target.png",
        Exit = "img/my-game/exit.png",
        ExitDown = "img/my-game/exit-down.png",
        Separator = "img/my-game/separator.png",
        Play = "img/my-game/play.png",
        PlayDisabled = "img/my-game/play-disabled.png",
        InventoryObjectIconsPath = "img/my-game/objects"
    };
});
builder.Services.AddSegusumStorage(options => options.UseFile("data/game.json"));
var app = builder.Build();
app.UseSegusumInfrastructure();
app.UseStaticFiles();
app.MapControllers();
app.Run();
```

`samples/MinimalGame` è il test di consumo esclusivamente da pacchetto.
