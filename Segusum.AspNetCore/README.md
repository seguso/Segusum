# Segusum.AspNetCore

Integrazione ASP.NET Core del motore Segusum. Il package contiene il
controller HTTP condiviso e l'infrastruttura comune: un nuovo gioco non deve
duplicare controller o action `/api`.

## Workflow di un nuovo gioco

Il progetto dell'autore è una normale applicazione ASP.NET Core Web API
`net10.0`. Deve referenziare i package `Segusum` e `Segusum.AspNetCore` e
contenere il proprio `World`, gli oggetti, gli obiettivi e la logica narrativa.
Quella parte è il contenuto del gioco e non può essere fornita dal package.

Il file di avvio può restare ridotto alla configurazione dell'host:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSegusum((language, tutorialMode) =>
    tutorialMode
        ? new MyGame.TutorialWorld(language)
        : new MyGame.World(language));
builder.Services.AddSegusumStorage(options => options.UseFile("data/my-game.json"));

var app = builder.Build();
app.UseSegusumInfrastructure();
app.UseStaticFiles();
app.UseRouting();
app.MapControllers();
app.Run();
```

L'autore non implementa `SegusumController`, non deve conoscere i nomi delle
action HTTP e non deve creare wrapper per ogni endpoint. Può aggiungere
controller propri soltanto per funzionalità specifiche non appartenenti al
motore.

Il package è `net10.0`, quindi è compatibile con host .NET 10
cross-platform, inclusi Linux e Termux. Il gioco deve fornire i propri file
statici e le proprie route di presentazione quando desidera una UI diversa.

Per configurazione senza codice si possono usare `SEGUSUM_STORAGE=file` e
`SEGUSUM_FILE_PATH`; `AddSegusumStorage()` interpreta queste variabili. SQL
Server resta disponibile, ma il provisioning e le migrazioni automatiche non
fanno parte della verifica file-mode descritta qui e saranno documentati in
una fase successiva.
