# Segusum.WebClient

Razor Class Library del client web grafico standard Segusum. È incluso
transitivamente da `Segusum.AspNetCore`; i giochi non devono copiare view,
JavaScript o CSS engine.

Gli asset sono pubblicati come static web assets namespaced sotto
`/_content/Segusum.WebClient/`. Titolo, crediti, percorso icone e prefisso
degli asset del gioco sono configurabili tramite `SegusumOptions`.

Le stringhe UI standard sono cataloghi engine XML embedded nel package e non
vengono copiate nei giochi. Un gioco può sostituire soltanto la source canonica
di una chiave, senza definire traduzioni per lingua:

```csharp
builder.Services.AddSegusum(options =>
{
    options.WorldFactory = (language, tutorial) => new World(language);
    options.OverrideClientString("saveGame", "Store progress");
});
```

La source dell’override viene estratta dal Translator Tool e sincronizzata nel
normale `transl_<language>.xml` del gioco. Il catalogo risolto viene inviato
una sola volta nel bootstrap HTML; le response gameplay non lo contengono.
