# Public repository boundary

Questo tree è una copia autonoma e senza storia del repository privato del
gioco. Contiene soltanto i sorgenti compilati dal core Segusum, la persistenza
standard, l'integrazione ASP.NET Core e la documentazione generica.

Sono esclusi gioco, World concreto, puzzle, dialoghi, cutscene, traduzioni,
asset, database, salvataggi, strumenti di traduzione, strumenti di annotazione,
guide private e script specifici dell'ambiente del gioco.

`Segusum` resta il livello base; `Segusum.Persistence` dipende da esso; `Segusum.AspNetCore`
dipende da entrambi. Nessun progetto dipende dal gioco privato.
