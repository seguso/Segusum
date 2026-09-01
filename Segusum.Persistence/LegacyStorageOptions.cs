using System;
using System.IO;
using Segusum.Persistence;

namespace Seg;

/// <summary>
/// Bridge interna per i costruttori EF e il codice storico dei salvataggi.
/// La configurazione pubblica è <see cref="SegusumStorageOptions"/> via DI.
/// </summary>
internal static class StorageOptions
{
    private static SegusumStorageOptions current = SegusumStorageOptions.FromEnvironment();

    internal static void Configure(SegusumStorageOptions options) => current = options;

    public static bool IsFile => current.FilePersistenceEnabled;
    public static string FilePath => current.FilePath
        ?? Path.Combine(AppContext.BaseDirectory, "data", "segusum.json");
    public static string ConnectionString => current.ConnectionString
        ?? "Server=.\\SQLEXPRESS;Database=segusum;Trusted_Connection=True;TrustServerCertificate=True;MultipleActiveResultSets=True";
    public static string InMemoryDatabaseName => current.InMemoryDatabaseName;
}
