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
    private static SegusumStorageOptions current = new();

    internal static void Configure(SegusumStorageOptions options) => current = options;

    public static bool IsFile => current.FilePersistenceEnabled;
    public static string FilePath => current.FilePath
        ?? throw new InvalidOperationException(
            "File storage requires a path supplied by the host via UseFile.");
    public static string ConnectionString => current.ConnectionString
        ?? throw new InvalidOperationException(
            "SQL Server storage requires a non-empty connection string supplied " +
            "by the host via UseSqlServer.");
    public static string InMemoryDatabaseName => current.InMemoryDatabaseName;
}
