# Segusum Translator

`Segusum.Translator.Core` contiene l’estrazione delle stringhe, la sincronizzazione XML, il sequence diff, il fuzzy matching e la scrittura atomica. `Segusum.Translator.Cli` e `Segusum.Translator.Web` usano lo stesso Core.

## Configurazione del gioco

Senza configurazione il Core cerca ricorsivamente i file `*.cs` sotto la root, escludendo `bin`, `obj`, `.git`, `node_modules` e `generated`, e applica gli stessi marker Translator storici. Per limitare la scansione, la root può contenere un `translator.json` dichiarativo:

```json
{
  "include": ["Game", "Story/**/*.cs"],
  "exclude": ["Tests", "Tools/**/*.cs"]
}
```

Gli include sono relativi alla root; directory e glob sono supportati. Gli exclude filtrano directory o glob dopo gli include.

## CLI

```powershell
dotnet run --project Segusum.Translator.Cli -- sync --root C:\path\to\game --lang en
dotnet run --project Segusum.Translator.Cli -- sync --root C:\path\to\game --lang all --dry-run
```

I cataloghi sono file `transl_<language>.xml` cercati sotto la root. Il formato resta `str/orig/transl`; `transl="+"` significa non tradotto e le entry `obsolete="true"` sono preservate dal synchronizer.

## Web locale

```powershell
dotnet run --project Segusum.Translator.Web -- --root C:\path\to\game
```

Aprire l’URL mostrato da ASP.NET. La prima pagina permette di indicare la root e aprire un catalogo. L’editor mantiene l’ordine sincronizzato, mostra obsolete e changed pairs, usa una tabella continua virtualizzata e modifica solo `transl`. `Save` esegue una scrittura temporanea seguita da replace e rileva modifiche esterne tramite hash/timestamp.
