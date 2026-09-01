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
        Action<SegusumStorageOptions>? configure = null)
    {
        var storageOptions = configure is null
            ? SegusumStorageOptions.FromEnvironment()
            : new SegusumStorageOptions();
        if (configure is not null)
            configure(storageOptions);

        if (storageOptions.Provider == SegusumStorageProvider.SqlServer &&
            string.IsNullOrWhiteSpace(storageOptions.ConnectionString))
            throw new InvalidOperationException("Configurare una connection string SQL Server valida.");

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
            var isApiRequest = context.Request.Path.StartsWithSegments("/api");
            SemaphoreSlim? gate = null;
            var gateWaitMs = 0.0;

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
            }

            try
            {
                await next();
            }
            finally
            {
                gate?.Release();
                stopwatch.Stop();
                SegusumProfiler.Log($"request method={context.Request.Method} path={context.Request.Path} " +
                    $"status={context.Response.StatusCode} elapsed_ms={stopwatch.Elapsed.TotalMilliseconds:F1} " +
                    $"gate_wait_ms={gateWaitMs:F1}");
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
