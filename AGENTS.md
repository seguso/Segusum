# Istruzioni per l'AI

Questo repository contiene Segusum: motore/runtime, DSL `.seg`, parser, binder, generator, transpiler C# -> SEG, migration tooling e tooling VS Code.

Prima di modificare il codice leggere anche `docs/SEGUSUM-DSL-HANDOUT.md` se presente. Il consumer reale usato come corpus di migrazione è il repository `seguso/Litgir`, branch `masternew`.

## Principio architetturale

- La direzione è: gameplay in SEG; C# resta per engine/runtime/plumbing.
- Quando una migrazione incontra un costrutto C# gameplay non supportato, migliorare il transpiler/tooling in modo generico. Non tradurre manualmente il body per aggirare il limite.
- Local function e helper gameplay raggiungibili devono migrare anch'essi a `def` SEG.
- Non introdurre primitive DSL ad hoc quando il concetto può essere espresso come funzione, variabile, proprietà, member call o costrutto già esistente.
- Non aggiungere syntax lambda SEG salvo task esplicito. Per `Count(predicate)` e casi analoghi preferire comprehension/query o loop già supportati.

## Migrazione C# -> SEG

- Gli input frozen in `docs/migration-inputs` del consumer sono storici/autorevoli e non devono essere modificati da apply/removal.
- Prima di integrare una declaration generata, classificarla contro tutti i runtime SEG rilevanti come `AlreadyPresent`, `New` o `Conflict`.
- `AlreadyPresent` = no-op; `New` = integrare; `Conflict` = hard stop.
- Non concatenare output di root separati: dependency closure diverse possono generare la stessa `def`. Usare identity semantica + `SegDocumentMerger`/audit per deduplicare.
- Gli audit/extract devono essere idempotenti. Quando si rigenera il file target già presente nel runtime, escludere quel target dal confronto tramite l'opzione generica prevista dal tooling, senza hardcode del consumer.
- Gli script di ownership/audit non devono trattare `docs/migration-inputs` come runtime.

## Commenti: regola forte

La conservazione dei commenti non è solo cardinalità.

- `comment-loss = 0` è necessario ma non sufficiente.
- Ogni commento deve restare nella posizione semanticamente corrispondente.
- Portare con una migration unit soltanto:
  - commenti realmente interni al metodo/declaration;
  - leading trivia immediatamente associato a quella declaration;
  - trailing/end-of-line comment semanticamente appartenente a quella declaration/statement.
- NON trascinare commenti appartenenti ad altri field, metodi, lifecycle, setup, debug code o membri distanti della classe solo perché precedono testualmente la root selezionata.
- Non duplicare lo stesso commento in più artifact quando root/dependency closure si sovrappongono.
- Preservare TODO, codice commentato, rationale, storia progettuale, block comments e commenti colloquiali.
- Le stringhe `[[translation]]` non vanno alterate.
- I test di comment preservation devono verificare anche associazione/posizione e assenza di commenti estranei, non solo il numero totale.

## Lifecycle SEG già supportati

Tra i lifecycle gameplay supportati ci sono:

- `before-room-change`
- `after-action-executed`
- `before-action-executed`
- `start-game`

`start-game` mappa `startGameCutScene()`. Gli helper chiamati, per esempio `scenaIntro()`, restano normali `def`, non lifecycle speciali.

Per `before-action-executed`, il contesto C# `LogicObj lo, Objective obj, Room ro, out bool cancel` deve mantenere `lo`, `obj`, `ro`, `cancel` come simboli contestuali; `cancel` è assegnabile con normale assignment.

## Modalità grafica/testuale del client

- La modalità grafica/testuale è una preferenza di presentazione del browser, non una proprietà della sessione di autenticazione o del salvataggio.
- `?textmode=1` e `?textmode=0` prevalgono sulla preferenza `localStorage`; senza parametro si usa la preferenza browser, con default grafico.
- Il client invia la scelta tramite l'header centralizzato `X-Segusum-Text-Mode`. Le richieste prive di header usano il default grafico, mai un valore storico di sessione o utente.
- `PresentationModeContext` propaga il valore come contesto `AsyncLocal` per la richiesta. `WorldBase.IsTextMode` è soltanto la superficie di compatibilità per gameplay/helper esistenti e non conserva un campo nel World condiviso.
- `restoreWorldFromMemoryOrDisk` e `restoreWorldFromDisk` non ricevono né assegnano la modalità. `GetRoomRes.grrIsTextMode` descrive la risposta effettiva e guida il layout del client.
- `SegusumSessionStore.SessionData` non conserva la modalità. Il bearer autentica lo stesso giocatore quando cambia la preferenza.
- `user.canPlayGraphicsMode` resta soltanto per round-trip dei record legacy; non va reintrodotto come fonte di rendering né rimosso con una migrazione distruttiva.
- Non aggiungere parametri di modalità a ogni API: il punto di ingresso è il trasporto client centralizzato e la risoluzione in `ApiBase.auth` (più il bootstrap di creazione account, che non passa da `auth`).
- Se cambia JavaScript, incrementare il cache-busting nella Razor page che carica l'asset e verificare il percorso static web asset effettivamente servito.

## Tooling semantic / VS Code

- Definition, References e Rename devono usare la stessa identity semantica; niente fallback testuale.
- Un rename deve essere rifiutato prima del WorkspaceEdit se crea collisione nello stesso identity domain/scope; usare le stesse regole del binder. La diagnostica prevista è `SEGTOOL006`.
- Il semantic host serializza le RPC tramite coda/gate. Il timeout client deve partire quando la RPC entra davvero in esecuzione, non mentre è in coda.
- Cancellation deve propagare alle operazioni interattive, incluso parser, parse cache, semantic workspace e rename validation.
- Il parser deve sempre fare progress anche su overlay editor incompleti/malformati. Ogni recovery loop deve consumare input o terminare.

## Performance e guardrail

- Non reintrodurre scansioni globali Roslyn tipo `GetSymbolsWithName` negli hot path.
- Non eseguire due build contemporaneamente.
- Per build diagnostiche usare `UseSharedCompilation=false`, `-m:1`, `/nodeReuse:false`.
- Applicare timeout e memory guard; in caso di runaway terminare l'intero process tree.
- Buildare CLI/tool una volta e invocare il DLL prodotto, invece di ripetere `dotnet run` in loop.
- Il parser di grandi file SEG sani deve stare nell'ordine dei millisecondi/centinaia di ms, non secondi o minuti. Se esplode, verificare prima package/provenance e non-progress.

## Consumer Litgir / package provenance

Litgir NON usa ProjectReference verso questo checkout: consuma package NuGet Segusum.

Prima di dichiarare valida una build Litgir contro modifiche Segusum:

1. commit/push Segusum;
2. creare un nuovo package DEV con il workflow previsto dal consumer;
3. far selezionare a Litgir quella versione;
4. restore;
5. verificare `SegusumVersion`, `project.assets.json`, package path e DLL effettive;
6. solo dopo build/test Litgir.

Non attribuire al checkout corrente un comportamento osservato con package vecchi.

## Worktree e switch definitivo

Per migrazioni distruttive sul consumer usare prima un temporary worktree:

1. pure generation dal frozen;
2. determinismo byte-for-byte;
3. parser/binder diagnostics 0;
4. declaration audit (`AlreadyPresent/New/Conflict`);
5. duplicate declarations 0;
6. comment preservation semantica;
7. ownership dry-run/apply;
8. build/test bounded;
9. solo poi switch definitivo nel primary.

## Commit e push

Quando un task produce modifiche valide e le verifiche concordate sono verdi:

- committare;
- pushare sul branch corretto;
- riportare SHA e working tree status.

Non lasciare modifiche valide soltanto nel working tree salvo istruzione esplicita di fermarsi prima del commit/push.

## Coordinamento AI e handoff operativo

Per continuità tra ChatGPT, Codex e lo sviluppatore usare questi documenti con ruoli distinti:

- `docs/AI-HANDOFF.md`: **handoff corrente**. Deve descrivere lo stato presente del progetto, decisioni vincolanti, problemi aperti, workflow attivo e prossimo lavoro. È il primo documento da leggere quando si riprende il progetto in una nuova conversazione/sessione.
- `docs/SEGUSUM-DSL-HANDOUT.md`: **handout tecnico/storico** della DSL, del transpiler, del tooling e delle migrazioni. Serve per il contesto profondo e la storia delle decisioni; non deve sostituire il handoff corrente.
- `AGENTS.md`: regole operative e architetturali vincolanti.

All'inizio di una nuova sessione:
1. leggere `AGENTS.md`;
2. leggere `docs/AI-HANDOFF.md`;
3. se il task riguarda DSL/migrazione/tooling, leggere anche `docs/SEGUSUM-DSL-HANDOUT.md`;
4. se coinvolge Litgir, leggere anche il suo `AGENTS.md`, `docs/AI-HANDOFF.md` e, quando rilevante, `docs/ENGINE-CONVENTIONS.md`.

Dopo un task importante che cambia uno dei seguenti elementi:
- stato di una migrazione;
- decisione architetturale;
- workflow build/package;
- bug/tooling noto;
- prossimo lavoro consigliato;
- commit/SHA di riferimento operativo;

aggiornare `docs/AI-HANDOFF.md` nello stesso task prima del report finale.

Il handoff NON è un changelog completo: rimuovere o riscrivere informazioni superate e mantenere solo lo stato utile a riprendere il lavoro. Se il codice/HEAD corrente contraddice il handoff, verificare il codice, correggere il handoff e non perpetuare informazioni stale.

Quando un task coinvolge entrambi i repo, aggiornare entrambi i handoff se lo stato condiviso cambia.

Nel report finale riportare sempre:
- SHA finali;
- build/test eseguiti;
- package provenance quando coinvolge Segusum -> Litgir;
- working tree status;
- eventuali punti lasciati aperti.
