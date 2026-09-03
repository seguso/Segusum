# Segusum Admin

Dashboard amministrativa separata dal gioco. Avviare con `dotnet run --project Segusum.Admin`; configurare `ConnectionStrings:Segusum` oppure `SEGUSUM_CONNECTION_STRING`. Il progetto non è referenziato da Litgir e non espone route nel server del gioco: va pubblicato su una porta/processo amministrativo dedicato e protetto dall’infrastruttura di deployment.
