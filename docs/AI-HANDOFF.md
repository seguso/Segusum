# AI Handoff — Segusum

Ultimo aggiornamento: 2026-10-22.

Questo file è il punto di continuità operativo tra ChatGPT, Codex e lo sviluppatore. Va letto insieme ad `AGENTS.md` e, per la storia tecnica dettagliata della DSL/migrazione, a `docs/SEGUSUM-DSL-HANDOUT.md`.

## Repository e relazione col consumer

- Repo: `seguso/Segusum`
- Branch operativo: `main`
- Consumer reale: `seguso/Litgir`, branch `masternew`
- Segusum contiene engine/runtime, DSL SEG, parser/binder/generator, transpiler C# -> SEG, migration tooling, semantic tooling/VS Code extension e web client generico.
- Litgir NON usa un ProjectReference al checkout Segusum: usa package NuGet DEV. Prima di attribuire a Segusum un comportamento osservato in Litgir verificare sempre la provenance del package.

## Stato corrente consolidato

### Gameplay/DSL

La direzione architetturale è: gameplay in SEG; C# resta per engine/runtime/plumbing e contract del game.

Lifecycle SEG già in uso nel consumer:
- `before-room-change`
- `after-action-executed`
- `before-action-executed`
- `start-game`

Le local function/helper gameplay raggiungibili migrano a normali `def`. Non aggiungere primitive ad hoc quando basta una funzione/proprietà/member call/costrutto esistente. Per ora niente syntax lambda SEG salvo task esplicito.

Il transpiler deve fare la traduzione: non tradurre manualmente body gameplay per aggirare un limite.

### Migration tooling

Workflow obbligatorio:
1. frozen input autorevole nel consumer;
2. pure generation;
3. rigenerazione byte-identica;
4. parser/binder diagnostics 0;
5. declaration audit `AlreadyPresent/New/Conflict`;
6. conflict = hard stop;
7. dedup tramite identity/merger, mai concatenazione cieca di output root separati;
8. comment ownership Roslyn limitata alle migration unit selezionate;
9. temporary worktree per lo switch distruttivo;
10. build/test bounded;
11. solo dopo apply al primary.

Gli audit/extract devono escludere genericamente il runtime target già integrato per restare idempotenti.

### Comment preservation

Non basta `comment-loss = 0`: ogni commento deve restare associato semanticamente alla declaration corretta. Il fix recente ha eliminato il leakage da comment inventory globale e usa ownership Roslyn sulle unit selezionate. Preservare anche TODO, codice commentato, rationale, block comments e storia progettuale.

### Semantic tooling / VS Code

Definition, References e Rename condividono identity semantica. Rename verso un nome già occupato deve essere rifiutato semanticamente (`SEGTOOL006`), senza fallback testuale.

Il Tooling Host:
- serializza le RPC semantic tramite gate/coda;
- fa partire il timeout solo quando la request entra in esecuzione;
- propaga cancellation fino a parser, parse cache, semantic workspace e rename validation;
- deve restare responsive anche con overlay editor incompleti/malformati.

Ricordare che la VS Code extension può avviare direttamente il Tooling Host dal checkout locale: dopo fix al tooling host, ricompilare la DLL e fare Reload Window; reinstallare il VSIX da solo non prova che il host runtime sia aggiornato.

### UI assets engine/game

Decisione architetturale consolidata: Segusum NON deve fornire identità grafica game-specific tramite PNG/SVG di default.

Il game deve configurare esplicitamente `SegusumUiAssets`, inclusi:
- Hand
- Feet
- Eyes
- Options
- Talk / TalkAvailable
- Objectives / ObjectivesAvailable
- Thinking
- Target
- Exit / ExitDown
- Separator
- Play / PlayDisabled
- InventoryObjectIconsPath
- Favicons (15 path obbligatori tramite `SegusumFaviconAssets`)

Mancanze/null/vuoti devono fallire a startup con errore esplicito. Non reintrodurre directory/filename convenzionali hardcoded nell'engine.

I favicon/manifest sono anch'essi game assets obbligatori, non convenzioni dell'engine.

### Modalità grafica/testuale del client

- La preferenza viene risolta nel browser: `?textmode=0/1/2` prevale su `localStorage`; senza parametro si ripristina la preferenza browser, con default grafico.
- `main43.js` mantiene il valore tri-state e invia `0/1/2` con `X-Segusum-Text-Mode` dal wrapper centralizzato `doPost`. La risposta aggiorna la scelta client usando `grrPresentationMode`; `grrIsTextMode` resta il booleano derivato per il layout e la compatibilità.
- `ApiBase.auth` risolve l'header per ogni richiesta; il bootstrap `createUserAndStartGame` usa lo stesso resolver perché non passa da `auth`. Modalità assente o non valida significa grafica.
- `SegusumSessionStore.SessionData` contiene solo autenticazione/game mode casuale/game ID; non contiene più `IsTextMode`. `loadGame` può riusare il bearer durante un reload.
- `PresentationModeContext.Mode` è l'enum `Graphics=0`, `Text=1`, `TextWithRoomBackgroundPreview=2`, propagato con `AsyncLocal`. `WorldBase.IsTextMode` è soltanto la vista derivata `Mode != Graphics`; il valore non è una proprietà del World condiviso, non è serializzato e non viene passato ai metodi di restore.
- `GetRoomRes.grrPresentationMode` restituisce il valore effettivo e `grrIsTextMode` la vista derivata. La UI mostra `.roomTextMode` per 1 e 2 e la scena grafica per 0; `narImg` e `narRoom` continuano a usare il contesto booleano derivato.
- `GetRoomRes.grrTextModePreviewImg` è nullable e usa `Room.imgPath()` senza fallback. Solo in modalità 2 il client mostra il fondale configurato sopra il titolo nella colonna testuale; l'immagine è nel flusso, limitata alla colonna e non interattiva.
- `user.canPlayGraphicsMode` e le mappe di `FilePersistence` restano per conservare i record legacy; il campo non viene più letto per autenticazione/rendering né inizializzato sui nuovi account.
- Il gameplay SEG non è stato modificato: le sue condizioni testuali/grafiche restano nel runtime e vedono il contesto corrente durante la richiesta.
- La pagina Razor carica `main43.js?ver=94` e `segusum.css?ver=1` dal package `Segusum.WebClient`.
- La verifica `TextModePresentationTests.GameplayState` in Litgir esclude posizioni/elementi di una lista di oggetti di presentazione dal confronto; non dimostra identita completa del World serializzato. Il cambio modalita con save/load va completato con prova manuale in gioco.

### Ultimo task UX noto

Il click sull'icona occhio/Guarda ora fa lampeggiare una volta gli hotspot già visibili con fade-out -> fade-in. Commit di riferimento su main: `1b37a74d68d170187caa6d497419fc2b1b2fd62c` (`Flash visible hotspots on eye click`).

Non riaprire questo task salvo nuova evidenza di bug.

## Build/package verso Litgir

Per provare modifiche Segusum nel consumer:
1. checkout Segusum pulito;
2. commit + push Segusum;
3. creare package DEV;
4. far selezionare quella versione a Litgir;
5. restore;
6. verificare `Segusum.dev.version` e `project.assets.json`;
7. build/test Litgir.

Guardrail:
- no build parallele;
- `UseSharedCompilation=false`;
- `-m:1`;
- `/nodeReuse:false`;
- timeout e process-tree kill;
- niente console esterne visibili; usare terminale integrato / NoNewWindow.

## Coordinamento ChatGPT <-> Codex

Questo file è il **handoff corrente**. Regola:
- ChatGPT/Codex devono leggerlo all'inizio di una nuova sessione;
- dopo un task importante che cambia stato, decisioni, workflow o prossimo lavoro, aggiornare questo file;
- non usarlo come changelog completo: contenere solo stato attuale, decisioni vincolanti, problemi aperti e prossimo lavoro;
- la storia tecnica dettagliata della DSL resta in `SEGUSUM-DSL-HANDOUT.md`;
- se il codice contraddice questo file, verificare il codice/commit corrente e aggiornare il handoff: non perpetuare informazioni stale;
- quando un task è verde, commit + push sempre salvo istruzione esplicita contraria;
- riportare sempre SHA finali di Segusum e, se coinvolto, Litgir.

## Problemi/aperti immediati

- Il workflow VS Code Litgir `Debug LitGir Server (existing Segusum packages)` presuppone attualmente l'esistenza del local feed. È stato identificato come fragilità: il comportamento desiderato è fallback automatico al workflow fresh quando il feed manca o è incompleto. Se non ancora implementato, questo è un piccolo task infrastrutturale aperto.
- La prossima grossa migrazione gameplay da `world.cs` non è stata ancora scelta definitivamente. `setStartState` e `configureGameRestrictions` erano stati deliberatamente rimandati; non migrarli senza task esplicito.

## File di riferimento

- `AGENTS.md`: regole operative vincolanti.
- `docs/AI-HANDOFF.md`: questo stato operativo corrente.
- `docs/SEGUSUM-DSL-HANDOUT.md`: handout tecnico/storico dettagliato della DSL e migrazioni.
- Consumer: `seguso/Litgir` -> `AGENTS.md`, `docs/AI-HANDOFF.md`, `docs/ENGINE-CONVENTIONS.md`.
