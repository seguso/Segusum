using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Seg
{
    /// <summary>
    /// Optional low-overhead profiler for local performance investigations.
    /// It is deliberately disabled unless SEGUSUM_PROFILE_ENABLED is 1/true.
    /// </summary>
    internal static class SegusumProfiler
    {
        private sealed class RequestContext
        {
            public RequestContext(string id, int startThread, long startAllocated)
            {
                Id = id;
                StartThread = startThread;
                StartAllocated = startAllocated;
            }

            public string Id { get; }
            public int StartThread { get; }
            public long StartAllocated { get; }
        }

        private static readonly bool Enabled = IsEnabled(Environment.GetEnvironmentVariable("SEGUSUM_PROFILE_ENABLED"));
        private static readonly AsyncLocal<RequestContext?> Current = new();
        private static readonly Channel<string>? Queue = Enabled
            ? Channel.CreateUnbounded<string>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false,
                AllowSynchronousContinuations = true
            })
            : null;
        private static readonly Task? Writer = Enabled ? Task.Run(WriteLoopAsync) : null;

        private static string LogPath => Environment.GetEnvironmentVariable("SEGUSUM_PROFILE_LOG")
            ?? Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", ".segusum-logs", "profiling.log"));

        public static IDisposable BeginRequest(string requestId)
        {
            if (!Enabled) return NoopScope.Instance;
            Current.Value = new RequestContext(requestId, Environment.CurrentManagedThreadId,
                GC.GetAllocatedBytesForCurrentThread());
            return new RequestScope();
        }

        public static void Log(string message)
        {
            if (!Enabled || Queue == null) return;
            var context = Current.Value;
            var prefix = context == null ? "" : $"request_id={context.Id} ";
            Queue.Writer.TryWrite($"{DateTimeOffset.UtcNow:O} [tid={Environment.CurrentManagedThreadId}] {prefix}{message}");
        }

        public static void Log(Func<string> messageFactory)
        {
            if (!Enabled) return;
            Log(messageFactory());
        }

        public static bool IsProfilingEnabled => Enabled;

        private static bool IsEnabled(string? value) =>
            value == "1" || string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase);

        private static async Task WriteLoopAsync()
        {
            if (Queue == null) return;
            StreamWriter? writer = null;
            try
            {
                await foreach (var line in Queue.Reader.ReadAllAsync())
                {
                    writer ??= CreateWriter();
                    if (writer == null) continue;
                    await writer.WriteLineAsync(line);
                    await writer.FlushAsync();
                }
            }
            catch
            {
                // Profiling must never affect gameplay or persistence.
            }
            finally
            {
                writer?.Dispose();
            }
        }

        private static StreamWriter? CreateWriter()
        {
            try
            {
                var directory = Path.GetDirectoryName(LogPath);
                if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                return new StreamWriter(new FileStream(LogPath, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                    System.Text.Encoding.UTF8);
            }
            catch { return null; }
        }

        private sealed class RequestScope : IDisposable
        {
            private readonly RequestContext? context = Current.Value;
            private readonly Stopwatch stopwatch = Stopwatch.StartNew();
            public void Dispose()
            {
                stopwatch.Stop();
                if (context == null) return;
                var allocation = Environment.CurrentManagedThreadId == context.StartThread
                    ? $"alloc_current_thread_bytes={GC.GetAllocatedBytesForCurrentThread() - context.StartAllocated}"
                    : "alloc_current_thread_bytes=unavailable_thread_changed";
                var gc = GC.GetGCMemoryInfo();
                Log($"phase=request-end elapsed_ms={stopwatch.Elapsed.TotalMilliseconds:F1} {allocation} " +
                    $"gen0={GC.CollectionCount(0)} gen1={GC.CollectionCount(1)} gen2={GC.CollectionCount(2)} " +
                    $"heap_size_bytes={gc.HeapSizeBytes} fragmented_bytes={gc.FragmentedBytes} " +
                    $"total_committed_bytes={gc.TotalCommittedBytes}");
                Current.Value = null;
            }
        }

        private sealed class NoopScope : IDisposable
        {
            public static readonly NoopScope Instance = new();
            public void Dispose() { }
        }
    }
}
