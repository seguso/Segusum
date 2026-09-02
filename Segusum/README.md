# Segusum

Licenza: MIT.

Segusum è un motore .NET per giochi di avventura. Può essere usato con un
frontend testuale, grafico o con un'altra interfaccia. Il package contiene
le API del motore e i tipi base per definire il proprio `World`; non contiene il
contenuti narrativi o dati di un gioco specifico.

Requisiti: .NET 10.

```bash
dotnet add package Segusum --version 0.1.0
```

Un gioco definisce una classe derivata da `WorldBase` e implementa la propria
logica di stanze, oggetti, obiettivi e handler. Per esporre il gioco via HTTP
si usa il package `Segusum.AspNetCore` e si registra la factory nel `Program.cs`.

```csharp
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSegusum((language, tutorialMode) =>
    new MyGame.World(language));

builder.Services.AddSegusumStorage(options => options.UseFile("data/my-game.json"));

var app = builder.Build();
app.UseSegusumInfrastructure();
app.MapControllers();
app.Run();
```

L'integrazione ASP.NET Core registra automaticamente i controller standard;
la persistenza standard e l'integrazione web sono package separati. L'host
decide autonomamente come ottenere i valori di configurazione e li passa alle
options: Segusum non legge file di configurazione, environment variables o
`IConfiguration`.

`UseFile` è il percorso consigliato per sviluppo locale e installazioni
semplici: mantiene il formato JSON storico (con la migrazione shardata già
supportata) usando internamente la cache EF InMemory. Il percorso può essere
assoluto o relativo e le directory mancanti vengono create quando occorre.
SQL Server resta supportato, ma provisioning del database e migrazioni
automatiche sono rimandati a una fase dedicata e non sono promessi da questo
quick start. Per SQL Server l'host passa una stringa già risolta con
`options.UseSqlServer(connectionString)`. La sorgente può essere scelta
liberamente dall'applicazione (JSON, environment variables, User Secrets o
un provider personalizzato).
