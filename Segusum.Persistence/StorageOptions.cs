using System;
using System.IO;

namespace Segusum.Persistence;

public enum SegusumStorageProvider
{
    SqlServer,
    InMemory
}

/// <summary>
/// Configurazione dello storage standard di Segusum.
/// Il gioco la configura tramite <c>AddSegusumStorage</c>; non contiene
/// riferimenti a EF o al provider concreto così l'API resta semplice.
/// </summary>
public sealed class SegusumStorageOptions
{
    public SegusumStorageProvider Provider { get; private set; } = SegusumStorageProvider.SqlServer;
    public string? ConnectionString { get; private set; }
    public string InMemoryDatabaseName { get; private set; } = "segusum-memory";
    public string? FilePath { get; private set; }

    internal bool FilePersistenceEnabled => FilePath is not null;

    public SegusumStorageOptions UseSqlServer(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new ArgumentException("La connection string SQL Server non può essere vuota.", nameof(connectionString));

        Provider = SegusumStorageProvider.SqlServer;
        ConnectionString = connectionString;
        FilePath = null;
        return this;
    }

    public SegusumStorageOptions UseInMemory(string databaseName = "segusum-memory")
    {
        if (string.IsNullOrWhiteSpace(databaseName))
            throw new ArgumentException("Il nome del database InMemory non può essere vuoto.", nameof(databaseName));

        Provider = SegusumStorageProvider.InMemory;
        InMemoryDatabaseName = databaseName;
        ConnectionString = null;
        FilePath = null;
        return this;
    }

    /// <summary>
    /// Usa EF InMemory come cache compatibile con la vecchia persistenza su file.
    /// Il formato JSON storico e la migrazione a file shardati restano invariati.
    /// </summary>
    public SegusumStorageOptions UseFile(string filePath, string databaseName = "segusum-file-cache")
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("Il percorso del file di persistenza non può essere vuoto.", nameof(filePath));

        UseInMemory(databaseName);
        FilePath = Path.GetFullPath(filePath);
        return this;
    }

    /// <summary>
    /// Builds options for the file and in-memory modes from the generic
    /// SEGUSUM_* environment variables. SQL configuration belongs to the
    /// host application, which should read ConnectionStrings:Segusum through
    /// its normal configuration and call UseSqlServer explicitly.
    /// </summary>
    public static SegusumStorageOptions FromEnvironment()
    {
        var options = new SegusumStorageOptions();
        var mode = Environment.GetEnvironmentVariable("SEGUSUM_STORAGE");
        if (string.Equals(mode, "file", StringComparison.OrdinalIgnoreCase))
        {
            var path = Environment.GetEnvironmentVariable("SEGUSUM_FILE_PATH")
                ?? Path.Combine(AppContext.BaseDirectory, "data", "segusum.json");
            options.UseFile(path);
        }
        else if (string.Equals(mode, "memory", StringComparison.OrdinalIgnoreCase))
        {
            options.UseInMemory(Environment.GetEnvironmentVariable("SEGUSUM_MEMORY_DATABASE") ?? "segusum-memory");
        }
        else
        {
            var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Segusum");
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException(
                    "SQL Server storage requires ConnectionStrings:Segusum. " +
                    "Configure it in the host and call UseSqlServer explicitly.");

            options.UseSqlServer(connectionString);
        }

        return options;
    }
}
