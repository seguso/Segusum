# Segusum Translator

`Segusum.Translator.Core` contiene estrazione delle stringhe, sincronizzazione XML, sequence diff, fuzzy matching e scrittura atomica. CLI e Web usano lo stesso Core.

## Configurazione

Senza configurazione il Core cerca ricorsivamente `*.cs` sotto la root, escludendo `bin`, `obj`, `.git`, `node_modules` e `generated`, e applica i marker Translator esistenti. Un `translator.json` nella root puo restringere la scansione:

```json
{
  "include": ["Game", "Story/**/*.cs"],
  "exclude": ["Tests", "Tools/**/*.cs"]
}
```

Gli include e gli exclude sono relativi alla root e supportano directory e glob.

## Cataloghi e CLI

```powershell
dotnet run --project Segusum.Translator.Cli -- sync --root C:\path\to\game --lang en
dotnet run --project Segusum.Translator.Cli -- sync --root C:\path\to\game --lang fr
dotnet run --project Segusum.Translator.Cli -- sync --root C:\path\to\game --lang all --dry-run
```

I cataloghi sono `transl_<language>.xml`. `sync --lang <language>` crea nella root un catalogo mancante e non sovrascrive mai uno esistente. `--dry-run` e `--check` non scrivono. Il formato resta `str/orig/transl`; `transl="+"` significa non tradotto e `obsolete="true"` e preservato dal synchronizer.

## Web locale

```powershell
dotnet run --project Segusum.Translator.Web -- --root C:\path\to\game
```

La pagina `Catalogs` consente di indicare la root, sincronizzare esplicitamente cataloghi esistenti o crearne uno nuovo. `Open editor` carica il contenuto corrente senza sincronizzare e senza scrivere. `Synchronize` e `Refresh / Synchronize from source` usano la stessa `TranslationCatalogOperations` del CLI.

L'editor mantiene l'ordine, mostra obsolete e changed pairs, usa una tabella continua virtualizzata e modifica solo `transl`. `Save` usa scrittura temporanea e replace e rileva modifiche esterne tramite hash/timestamp.
