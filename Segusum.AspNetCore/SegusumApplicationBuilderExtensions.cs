using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Seg;
using Segusum.Persistence;

namespace Segusum.AspNetCore;

/// <summary>
/// Integrazione delle parti infrastrutturali comuni dell'host Segusum.
/// Il gioco configura lo storage tramite options DI; il bridge legacy serve
/// soltanto ai modelli EF e ai save storici che non sono ancora parametrizzati.
/// </summary>
public static class SegusumApplicationBuilderExtensions
{
    public static IServiceCollection AddSegusumStorage(this IServiceCollection services,
        Action<SegusumStorageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(configure);
        var storageOptions = new SegusumStorageOptions();
        configure(storageOptions);

        if (storageOptions.Provider == SegusumStorageProvider.SqlServer &&
            string.IsNullOrWhiteSpace(storageOptions.ConnectionString))
            throw new InvalidOperationException(
                "SQL Server storage requires a non-empty connection string supplied by the host via UseSqlServer.");

        StorageOptions.Configure(storageOptions);
        services.AddSingleton(storageOptions);
        services.AddDbContext<segusumDb>(options =>
        {
            if (storageOptions.Provider == SegusumStorageProvider.InMemory)
                options.UseInMemoryDatabase(storageOptions.InMemoryDatabaseName);
            else
                options.UseSqlServer(storageOptions.ConnectionString!);
        });
        services.AddHostedService<SegusumUserCleanupService>();

        return services;
    }

    public static WebApplication UseSegusumInfrastructure(this WebApplication app)
    {
        app.Use(async (context, next) =>
        {
            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var requestId = Guid.NewGuid().ToString("N");
            context.Response.Headers["X-Segusum-Request-Id"] = requestId;
            using var profilingScope = SegusumProfiler.BeginRequest(requestId);
            SegusumProfiler.Log($"phase=request-start method={context.Request.Method} path={context.Request.Path}");
            var isApiRequest = context.Request.Path.StartsWithSegments("/api");
            SemaphoreSlim? gate = null;
            var gateWaitMs = 0.0;
            var gateHoldStopwatch = (System.Diagnostics.Stopwatch?)null;

            if (isApiRequest && !HttpMethods.IsGet(context.Request.Method))
            {
                context.Request.EnableBuffering();
                using var reader = new StreamReader(context.Request.Body, leaveOpen: true);
                var body = await reader.ReadToEndAsync();
                context.Request.Body.Position = 0;

                try
                {
                    using var json = JsonDocument.Parse(body);
                    if (json.RootElement.TryGetProperty("uname", out var unameElement))
                    {
                        var userName = unameElement.GetString();
                        if (!string.IsNullOrWhiteSpace(userName))
                            gate = ApiSerializationGate.ForUser(userName);
                    }
                }
                catch (JsonException)
                {
                    // Il controller produrrà normalmente la risposta per il body non valido.
                }
            }

            if (gate != null)
            {
                var gateStopwatch = System.Diagnostics.Stopwatch.StartNew();
                await gate.WaitAsync();
                gateStopwatch.Stop();
                gateWaitMs = gateStopwatch.Elapsed.TotalMilliseconds;
                gateHoldStopwatch = System.Diagnostics.Stopwatch.StartNew();
                SegusumProfiler.Log($"phase=gate-acquired gate_wait_ms={gateWaitMs:F1}");
            }

            try
            {
                await next();
            }
            finally
            {
                gateHoldStopwatch?.Stop();
                gate?.Release();
                stopwatch.Stop();
                SegusumProfiler.Log($"phase=request-summary method={context.Request.Method} path={context.Request.Path} " +
                    $"status={context.Response.StatusCode} elapsed_ms={stopwatch.Elapsed.TotalMilliseconds:F1} " +
                    $"gate_wait_ms={gateWaitMs:F1} gate_hold_ms={gateHoldStopwatch?.Elapsed.TotalMilliseconds ?? 0:F1}");
            }
        });

        if (StorageOptions.IsFile)
        {
            using var scope = app.Services.CreateScope();
            scope.ServiceProvider.GetRequiredService<segusumDb>().Database.EnsureCreated();
        }

        return app;
    }
}
