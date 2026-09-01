# Segusum

Segusum è un engine .NET per avventure testuali interattive. Questo repository
contiene soltanto il motore generico e la sua integrazione web: non contiene un
gioco, contenuti narrativi o asset di un progetto specifico.

Stato: 0.1.0, early development. Richiede .NET 10 ed è distribuito con licenza
MIT.

## Package

- `Segusum`: tipi e logica base dell'engine;
- `Segusum.Persistence`: persistenza standard, inclusa la modalità file;
- `Segusum.AspNetCore`: controller e infrastruttura ASP.NET Core standard.

## Quick start

Un gioco definisce il proprio `WorldBase` e la propria logica narrativa. Il suo
host può restare minimale:

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSegusum((language, tutorialMode) =>
    new MyGame.World(language));
builder.Services.AddSegusumStorage(options =>
    options.UseFile("data/my-game.json"));

var app = builder.Build();
app.UseSegusumInfrastructure();
app.MapControllers();
app.Run();
```

Non servono controller custom né la registrazione manuale degli application
part: gli endpoint standard sono forniti dall'integrazione `Segusum.AspNetCore`.
`UseFile` è la modalità semplice e cross-platform per sviluppo locale e
installazioni semplici. SQL Server è supportato, ma provisioning e migrazioni
automatiche saranno rifiniti e verificati in una fase successiva su Windows.

Per configurare la modalità file senza hardcodare il percorso si possono usare
`SEGUSUM_STORAGE=file` e `SEGUSUM_FILE_PATH`.
