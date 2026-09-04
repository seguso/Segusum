# Segusum.WebClient

Razor Class Library del client web grafico standard Segusum. È incluso
transitivamente da `Segusum.AspNetCore`; i giochi non devono copiare view,
JavaScript o CSS engine.

Gli asset sono pubblicati come static web assets namespaced sotto
`/_content/Segusum.WebClient/`. Titolo, crediti, percorso icone e prefisso
degli asset del gioco sono configurabili tramite `SegusumOptions`.
