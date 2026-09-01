using System;
using System.Diagnostics;
using System.IO;

namespace Seg
{
    internal static class SegusumProfiler
    {
        private static readonly object Sync = new();

        private static string LogPath => Environment.GetEnvironmentVariable("SEGUSUM_PROFILE_LOG")
            ?? Path.GetFullPath(Path.Combine(
                AppContext.BaseDirectory,
                "..", ".segusum-logs", "profiling.log"));

        public static void Log(string message)
        {
            try
            {
                lock (Sync)
                {
                    var directory = Path.GetDirectoryName(LogPath);
                    if (!string.IsNullOrEmpty(directory))
                        Directory.CreateDirectory(directory);
                    File.AppendAllText(LogPath,
                        $"{DateTimeOffset.Now:O} [tid={Environment.CurrentManagedThreadId}] {message}{Environment.NewLine}");
                }
            }
            catch
            {
                // Il profiling non deve mai impedire l'esecuzione del gioco.
            }
        }
    }
}
