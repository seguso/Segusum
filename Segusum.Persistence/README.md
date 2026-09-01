# Segusum.Persistence

Il package è distribuito con licenza MIT.

Persistenza standard per Segusum. `UseFile(path)` offre una modalità
cross-platform semplice per sviluppo locale e installazioni contenute: il
runtime usa EF InMemory come cache e mantiene utenti, IP e salvataggi nel
formato file supportato dal motore.

La configurazione pubblica avviene tramite `AddSegusumStorage`; non è
necessario conoscere `segusumDb` o configurare EF direttamente.
