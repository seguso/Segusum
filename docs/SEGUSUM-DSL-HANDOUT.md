# Segusum DSL — handout operativo

Ultimo aggiornamento: 2026-09-09.

Questo documento riassume lo stato del lavoro sulla DSL `.seg`, sul transpiler C# -> SEG, sul tooling VS Code/LSP e sulla migrazione reale di Litgir. È pensato come punto di ripartenza per un agente o sviluppatore che debba continuare il lavoro senza ricostruire la storia dai commit o dalle chat.

## Repository e branch

- Segusum: `seguso/Segusum`, branch `main`.
- Litgir: `seguso/Litgir`, branch `masternew`.
- Litgir è il consumer reale e il corpus di migrazione.
- Segusum contiene motore, DSL, parser/binder/generator, transpiler C# -> SEG, tooling semantico e VS Code extension.

Commit chiave recenti:

- Segusum `1db13545aa43768bbc8ce093d588b52cd1e46d8d`: materializzazione `IEnumerable<T>` -> `T[]` al boundary di ritorno array.
- Litgir `948b98332788a50ba7bf3aae018cc26db49a27ae`: switch definitivo di `after_action_executed` da C# a SEG.
- Segusum `9a4617d14bbaaf150d9835db0d02f4d2e875adb5`: navigation/definition/references/rename semantico DSL e collision checks.
- Segusum `79a813e8dc2d2d80bb83d08385831111d84c2ea2`: serializzazione RPC del tooling host e timeout per-request dopo effettivo start.

## Principio architetturale

La direzione è che il gameplay viva in SEG; il C# deve restare per engine/runtime/plumbing. Il transpiler deve essere migliorato quando incontra costrutti non supportati: non si devono tradurre manualmente i body C# per aggirare i limiti del transpiler.

Quando un helper o una local function C# contiene gameplay ed è raggiungibile dal codice migrato, deve migrare anch'esso a `def` SEG. Non deve essere parcheggiato in un helper C# residuale solo per rendere più semplice la migrazione.

Preferire il termine "switch definitivo C# -> SEG" per indicare il passaggio del runtime gameplay.

## Regole inderogabili di migrazione

### Frozen input

Gli input storici in `docs/migration-inputs` del repo Litgir sono autorevoli e non vanno modificati da ownership/apply o da script di integrazione.

Il frozen serve a:

- rigenerare deterministicamente il SEG;
- confrontare il comportamento C# originale con quello migrato;
- preservare commenti e storia progettuale;
- evitare che il corpus di migrazione venga alterato dallo switch runtime.

Gli script di removal/apply non devono mai includere `docs/migration-inputs`.

### Commenti

La conservazione dei commenti è obbligatoria: `comment-loss = 0`.

Devono essere preservati, nella posizione semanticamente corrispondente:

- TODO;
- design rationale;
- commenti colloquiali;
- codice disabilitato/commentato;
- commenti di scena/dialogo;
- block comments;
- commenti XML-like;
- storia delle decisioni.

Le stringhe `[[translation]]` non devono essere alterate.

Il merger SEG assegna il leading trivia/comment block alla declaration successiva durante l'estrazione delle sole declaration nuove.

### Declaration audit prima dell'integrazione

Ogni declaration generata deve essere confrontata con tutti i runtime `Gameplay/*.seg` rilevanti e classificata come:

- `AlreadyPresent`: stessa identity/signature e body canonico equivalente -> no-op;
- `New`: non presente -> candidata all'integrazione;
- `Conflict`: stessa identity/signature ma body diverso -> hard stop.

Non si devono duplicare declaration già presenti in un altro file SEG.

Il merger non usa fallback generici del tipo CLR per l'identità delle declaration: per i kind supportati l'identità deve essere esplicita; i kind non supportati devono produrre diagnostica hard, non falsa equivalenza.

### Temporary worktree

Uno switch distruttivo C# -> SEG deve essere provato prima in un worktree temporaneo:

1. pure generation dal frozen;
2. determinismo byte-for-byte;
3. declaration audit;
4. integrazione delle sole `New`;
5. ownership dry-run;
6. ownership apply solo nel worktree;
7. duplicate check;
8. parse/bind/generator/verifier;
9. build bounded;
10. smoke test mirati.

Solo se tutto è verde si applica lo switch al primary.

## Stato delle migrazioni Litgir

### OnRoomChanged

Migrato e attivo in `Gameplay/OnRoomChanged.seg`.

Il vecchio gameplay C# corrispondente è stato rimosso; rimangono soltanto helper runtime/plumbing realmente necessari.

### ActionHandlers

Le registrazioni gameplay sono migrate in `Gameplay/ActionHandlers.seg`.

Il corpus è stato usato come stress test reale per transpiler, parser, binder e generator. È un file grande (~622 KB, ~583 declaration) ed è il riferimento principale per verificare la scalabilità del parser/tooling.

### BeforeRoomChanged

La registrazione/lifecycle `before-room-change` è già migrata in `Gameplay/BeforeRoomChanged.seg`.

Resta come candidato naturale per il prossimo lavoro `beforeWalkPathResetVariables`, che in passato risultava un lifecycle override non ancora mappato dal transpiler.

### AfterActionExecuted

Switch definitivo completato.

Litgir commit:

`948b98332788a50ba7bf3aae018cc26db49a27ae` — `Migrate after-action gameplay to SEG`

Artifact runtime:

`WebApiLitGir/Gameplay/AfterActionExecuted.seg`

SHA256 al momento dello switch:

`1CD660F3AABAA628AE5CC0C73A6F8B7442683925FCB84B775BA1F3DF659C3BB4`

Pure generation dal frozen:

- 10 migration units;
- 11 declaration generate;
- 1 lifecycle `after-action-executed`;
- 10 `def`;
- una `def` deriva dalla local function raggiungibile `bisognaRimettereAPostoQuadroPittore`;
- diagnostics 0;
- artifact pure-generation SHA256 `38593978DD07179593F052FC1C89E313DE14D79E4A7C1CC1E1E0986A9F66147D`.

Al momento dello switch runtime:

- 11 declaration generate;
- 1 `AlreadyPresent`: `resettaAspettoOliviaECamilla()` già presente in `ActionHandlers.seg`;
- 10 `New` integrate in `AfterActionExecuted.seg`;
- 0 conflict;
- 0 duplicati SEG;
- parser/binder diagnostics 0.

Metodi C# rimossi dallo switch:

- `after_action_executed(CutScene, ActionContext)`;
- `getTempoMinOliviaDiceHaIndiziShort()`;
- `ilMagoStaCaricandoIncantesimo()`;
- `haElementiPerSpaccareLegnaConFulmine()`;
- `sonoPrigioniereOInSituazioneDiPericoloOSuspence()`;
- `rimetteAPostoQuadroAuto(LogicObj)`;
- `sbaglianoLaRispostaAlMago()`;
- `getStanzeDoveDraculaTiInsegue()`;
- `getStanzeDoveDraculaTiSenteColFischietto()`.

La local function `bisognaRimettereAPostoQuadroPittore(LogicObj)` è rappresentata dalla `def` SEG e non viene rimossa come metodo C# separato.

Build finale Litgir dopo lo switch:

- 0 errori;
- ~29.5 s;
- picco csc osservato ~301 MB;
- working set totale osservato ~848 MB.

Test Litgir:

- 76 pass;
- 2 failure già note e non correlate:
  - `GermanCatalogues_KeepEverySourceEntryInSourceOrder`;
  - `GermanClient_UsesItalianFallbackInsteadOfEnglishInheritance`.

## Sintassi e convenzioni SEG rilevanti

Esempi di sintassi consolidata:

```seg
world game

def helper arg: int ret bool:
    ret arg > 0
end

for obj in objects:
    obj.putInRoom room
end

var result: List<string> = []

var cyc = new-cycle
add cyc someGlobalCycleElementId
    when condition

    nar: Testo narrativo.
end
next cyc
```

Punti importanti:

- application syntax stile ML/F#: `foo a b`, non `foo(a, b)`;
- nested invocation parenthesizzata solo quando serve;
- collection literal: `[...]`;
- empty list senza contesto deve produrre diagnostic invece di diventare implicitamente `List<object>`;
- list comprehension disponibile: `[from collection item where predicate select expression]`;
- conditional expression: `if condition then a else b`;
- `this` supportato;
- `nar:` è syntax narrativa;
- `narRoom` / `narImg` restano normali invocation quando serve preservare semantica runtime completa;
- `HandlerInput` è un normale parametro runtime, non una primitive DSL;
- gli ID degli elementi cycle sono globalmente significativi e devono restare univoci;
- `add <cycle> <id>` usa l'ID come identità persistente/runtime.

## Materializzazione array al boundary

Durante la migrazione AfterActionExecuted due helper avevano firma `Room[]` ma il SEG generava semanticamente una pipeline enumerable, per esempio:

```seg
def getStanzeDoveDraculaTiInsegue ret Room[]:
    ret [...].Distinct
end
```

Il SEG rimane volutamente astratto. Il generator C# materializza `.ToArray()` solo quando:

- il tipo atteso è `T[]`;
- il tipo effettivo è enumerable compatibile;
- il tipo effettivo non è già array.

Il fix è nel commit Segusum `1db13545...` e non altera byte-for-byte l'artifact SEG.

Non aggiungere `.ToArray()` manualmente al SEG generato per risolvere casi analoghi: la materializzazione appartiene al boundary di code generation.

## Scalabilità parser: root cause e fix

Durante la validazione di AfterActionExecuted è comparso un runaway di `csc.exe` che arrivava a molti GB. La causa non era il nuovo SEG ma un package Segusum vecchio usato dal consumer.

La vera root cause storica del parser era `SourceSpan.From(...)`: ricalcolava linea/colonna riscorrendo il testo dall'inizio per molti span narrativi, producendo costo cumulativo superlineare.

Il fix, già presente dal commit `07b91a4`, usa:

- line-start offsets precomputati;
- `SpanAt(...)` con `Array.BinarySearch`;
- lexer dei block comment lineare.

Benchmark sano di riferimento su `ActionHandlers.seg`:

- ~621,518 byte;
- ~69,922 token;
- 583 declaration;
- parser diagnostics 0;
- parse ~106 ms nella misurazione di riferimento;
- peak processo ~50 MB;
- managed allocation ~5.2 MB.

Il generator locale aggiornato ha mostrato circa:

- ActionHandlers: ~2.5 s / ~201 MB;
- tutti i SEG runtime: ~4.0 s / ~218 MB.

Se si rivedono parse di molti secondi/minuti o crescita multi-GB, prima di cambiare parser verificare la versione/package realmente caricata.

## Packaging Segusum -> Litgir

Questo punto è fondamentale.

Litgir **non** consuma il checkout Segusum tramite `ProjectReference`. Consuma package NuGet reali tramite `PackageReference`.

Conseguenze:

- compilare Segusum non aggiorna automaticamente Litgir;
- modificare sorgenti Segusum non significa che un successivo `dotnet build` Litgir userà quelle modifiche;
- `WebApiLitGir` usa `Segusum.AspNetCore` con `Version="$(SegusumVersion)"`;
- nel workflow DEV `SegusumVersion` viene normalmente da `Segusum.dev.version`;
- il fallback è definito in `Directory.Build.props`.

Prima di attribuire un comportamento del consumer al "Segusum corrente" verificare sempre:

1. `SegusumVersion` effettiva;
2. `project.assets.json`;
3. path package risolto;
4. DLL passate a `csc`;
5. provenance del package/commit.

Durante il runaway di AfterActionExecuted il worktree temporaneo stava inizialmente usando `0.1.37-dev.segminimal4`, package precedente al fix parser. Dopo averlo portato a un package discendente dal fix (`0.1.37-dev.afteraction-ea8`) il runaway è scomparso: build ~25 s e csc ~400 MB.

Per lo switch definitivo è stato poi usato il package DEV:

`0.1.37-dev.1db1354`

Script DEV rilevanti nel repo Litgir:

- `scripts/build-segusum-feed.sh`;
- `scripts/use-latest-segusum-dev.ps1`.

Lo script di build feed attuale richiede checkout Segusum pulito. Non dichiarare che un package identificato solo dal SHA di `HEAD` rappresenti anche modifiche non committate.

## Guardrail build e diagnostica

Per build diagnostiche o migrazioni:

- non eseguire due build contemporaneamente;
- `UseSharedCompilation=false`;
- `-m:1`;
- `/nodeReuse:false`;
- timeout hard;
- memory guard;
- kill dell'intero process tree in caso di runaway;
- buildare CLI/tool una volta e invocare il DLL prodotto, non ripetere `dotnet run` in loop.

Una build Litgir sana recente ha mostrato csc nell'ordine di poche centinaia di MB, non molti GB.

## VS Code / semantic tooling

### Root cause del rename che non trovava i simboli

`Ctrl+R Ctrl+R`, Go to Definition e Find References fallivano con:

`SEGTOOL002: No symbol found at the requested location.`

Il log mostrava:

`matchedReference=<none>`

anche su declaration DSL reali.

Root cause: le declaration DSL non registravano una semantic reference precisa sullo span del token-nome; inoltre il rename aveva fallback testuali.

Il fix in `9a4617d14...` introduce:

- span precisi per funzioni;
- stati;
- cicli;
- parametri;
- local variables;
- cycle element IDs;
- named-cutscene IDs;
- identity semantica condivisa declaration/reference;
- deduplicazione riferimenti;
- rename semantico, non text-search;
- collision check per domain/scope;
- diagnostic `SEGTOOL006` per collisioni.

Un rename DSL deve cambiare soltanto declaration e riferimenti semanticamente associati, senza toccare commenti, narrativa o string literals.

### Collision semantics

Un rename verso un'identità già occupata deve essere rifiutato **prima** di produrre il WorkspaceEdit.

Esempi:

- function -> nome di altra function nello stesso domain: reject;
- local/parameter -> nome già occupato nello stesso scope: reject;
- cycle element ID -> ID globale già esistente: reject;
- stesso testo in scope locali indipendenti: consentito.

Diagnostic attesa:

`SEGTOOL006: Cannot rename 'old' to 'new': a ... symbol with that identity already exists.`

### Host runtime stale

La VS Code extension avvia il tooling host direttamente dal checkout locale Segusum, per esempio:

`Segusum.Tooling.Host/bin/Debug/net8.0/Segusum.Tooling.Host.dll`

Quindi reinstallare il VSIX **non** garantisce che il semantic host sia aggiornato.

Dopo modifiche a Core/Semantics/Tooling/Host:

1. terminare il vecchio host;
2. buildare esplicitamente `Segusum.Tooling.Host/Segusum.Tooling.Host.csproj`;
3. verificare che le DLL in `bin/Debug/net8.0` siano aggiornate;
4. `Developer: Reload Window` in VS Code.

Questa distinzione ha spiegato un caso in cui il sorgente conteneva il fix rename ma il runtime continuava a loggare `matchedReference=<none>`.

## RPC concurrency / timeout del semantic host

Dopo il fix rename è emerso un secondo bug: due richieste `definition` concorrenti e un `rename` potevano essere eseguiti contemporaneamente sullo stato semantic condiviso. Inoltre il timeout client partiva già all'invio, quindi includeva il tempo passato in coda.

Effetto osservato:

- una `definition` lenta superava 15 s;
- il client marcava l'host unhealthy;
- le altre pending RPC venivano respinte;
- il rename mostrava erroneamente `RPC 'definition' timed out` invece della propria collision diagnostic.

Il fix in `79a813e8...`:

- serializza le RPC semantic con `rpcGate`;
- il comando `cancel` resta fuori dalla coda e può cancellare una richiesta in esecuzione;
- l'host invia un evento `started` quando la richiesta entra davvero in esecuzione;
- il timeout client parte solo dopo `started`;
- il timeout/cancel riguarda la singola richiesta;
- il client non marca più automaticamente morto l'intero host per il timeout di una RPC;
- logging overlay include path, size, SHA256, parse start/completion, declaration count e diagnostics.

Il test manuale successivo ha mostrato che il rename normale funziona; il test collisione, ripetuto dopo il fix, non ha più manifestato il precedente timeout e risulta operativo nella prova manuale corrente.

Se ricompare un problema simile, il log sano deve mostrare la distinzione:

- `RPC start` = richiesta ricevuta/accodata;
- `RPC execute` = richiesta entrata effettivamente in esecuzione;
- il timeout interactive deve decorrere da `RPC execute`, non da `RPC start`.

## Semantic workspace: aspettative

Definition, References e Rename devono usare la stessa symbol identity.

Categorie già coperte dal lavoro recente:

- DSL functions;
- parameters/local variables;
- states;
- cycles;
- cycle element IDs;
- named-cutscene IDs;
- riferimenti cross-file;
- simboli C# referenziati dalla DSL tramite Roslyn.

Non introdurre regex/text-search come fallback del rename.

Il collision check deve riusare le stesse regole semantic/domain/scope del binder, non inventare una seconda semantica.

## Performance semantic tooling

Su Litgir il semantic workspace completo contiene circa:

- 4 file SEG runtime principali;
- ~781 declaration;
- ~1.1 MB di sorgenti SEG;
- ~15k-17k semantic references, a seconda dello snapshot.

Build semantic tipica osservata dopo i fix: ~0.7-1.7 s, con parse dei singoli file nell'ordine di decine/centinaia di ms.

Gli hotspot principali storici sono binder/Roslyn symbol resolution, non il parser sano.

Evitare di reintrodurre scansioni globali ripetute come `Compilation.GetSymbolsWithName` negli hot path. Il type index e le cache dei C# members sono stati introdotti proprio per evitare questo tipo di regressione.

## Problemi e failure note

### Test tedeschi

Due failure Litgir note e non correlate alla migrazione SEG:

- `GermanCatalogues_KeepEverySourceEntryInSourceOrder`;
- `GermanClient_UsesItalianFallbackInsteadOfEnglishInheritance`.

Non usarle come segnale di regressione SEG senza evidenza aggiuntiva.

### Testhost

In alcune esecuzioni complete sono comparsi abort/orphan di `testhost`. Le suite mirate sono preferibili durante iterazioni del tooling; al termine verificare sempre che non restino processi `csc`, `MSBuild`, `VBCSCompiler` o `testhost` residui.

### Antivirus / PowerShell diagnostico

Windows Defender ha una volta classificato come `Trojan:Win32/ClickFix...` un comando PowerShell diagnostico inline che caricava `Segusum.Scripting.Core.dll` via reflection e invocava `DslParser`. Il contenuto corrispondeva al probe diagnostico usato durante il debugging.

Per evitare falsi positivi e script opachi:

- non usare PowerShell inline complesso con `Assembly.LoadFrom`/reflection per benchmark del parser;
- preferire piccoli tool C# temporanei o test xUnit con reference normali;
- non disabilitare Defender e non aggiungere esclusioni globali.

## Checklist per una futura migrazione C# -> SEG

1. Identificare il file/method corpus C# e congelarlo in `docs/migration-inputs`.
2. Verificare che il frozen non venga modificato dai tool.
3. Eseguire `migrate-csharp` sul frozen.
4. `Manual = 0` come obiettivo; se qualcosa non traduce, migliorare il transpiler genericamente.
5. Doppia generazione byte-identica.
6. Parse generated SEG: diagnostics 0.
7. Declaration audit contro tutti i `Gameplay/*.seg`.
8. `Conflict = 0`.
9. Estrarre/integrare solo `New`, preservando leading comments.
10. Ownership dry-run con lista esatta dei metodi da rimuovere.
11. Apply solo in worktree temporaneo.
12. Duplicate declaration check = 0.
13. Parse/bind/generator/verifier verdi.
14. Verificare **prima** che il consumer stia usando il package DEV Segusum giusto.
15. Build bounded Litgir.
16. Smoke test mirati.
17. Solo allora switch sul primary.
18. Build/test primary.
19. Commit/push Segusum prima del package DEV quando il consumer deve usare nuovi fix.
20. Commit/push Litgir.

## Checklist per bug VS Code semantic tooling

1. Verificare che il Tooling Host sia realmente stato ricompilato.
2. `Developer: Reload Window`.
3. Controllare nel log il path esatto della DLL host avviata.
4. Controllare `matchedReference=<symbol>`; se è `<none>`, il problema è prima del rename.
5. Per collisioni, aspettarsi `SEGTOOL006`, non edit parziali.
6. Distinguere `RPC start` da `RPC execute`.
7. Una richiesta in coda non deve consumare il proprio timeout interactive.
8. Un timeout/cancel di una RPC non deve avvelenare tutte le altre.
9. Dopo un rename/collision test, fare subito Go to Definition per verificare che l'host resti sano.
10. Non usare fallback text-search.

## Prossimo lavoro consigliato

Il candidato naturale per la prossima migrazione è `beforeWalkPathResetVariables` nel vecchio `worldBeforeRoomChange.cs`, perché era rimasto come lifecycle override non mappato.

Prima di iniziare, verificare lo stato corrente del corpus: parti del vecchio C# potrebbero essere state già migrate in altri SEG. Applicare sempre declaration audit `AlreadyPresent/New/Conflict` invece di assumere che il file target debba contenere tutto l'output.

In alternativa, se la priorità è il tooling, consolidare ulteriormente:

- test live/automatici per la sequenza rename valido -> rename in collisione -> definition;
- coalescing di richieste `definition` identiche se diventa necessario per latenza;
- eventuale riduzione del costo di ricostruzione semantic workspace, senza riaprire la concorrenza non protetta sullo stesso stato.

## Regola di fondo

Quando un comportamento osservato sembra contraddire un test locale, prima di cambiare codice verificare sempre **quale assembly/package/processo stia realmente girando**.

Questa singola regola ha spiegato due dei problemi più costosi incontrati durante questo lavoro:

- Litgir che usava un package Segusum vecchio e faceva esplodere `csc`;
- VS Code che usava un Tooling Host stale nonostante il sorgente rename fosse già corretto.
