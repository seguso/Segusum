using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Seg;

namespace Segusum.AspNetCore;

/// <summary>
/// Rimuove dalla cache in memoria i mondi degli utenti inattivi.
/// È registrato una sola volta dall'host, invece di essere avviato da ogni
/// istanza del controller HTTP.
/// </summary>
internal sealed class SegusumUserCleanupService : BackgroundService
{
    private static readonly TimeSpan CheckInterval = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan InactivityLimit = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory scopeFactory;
    private readonly ILogger<SegusumUserCleanupService> logger;

    public SegusumUserCleanupService(IServiceScopeFactory scopeFactory,
        ILogger<SegusumUserCleanupService> logger)
    {
        this.scopeFactory = scopeFactory;
        this.logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(CheckInterval);

        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
                CleanupInactiveUsers();
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Arresto normale dell'host.
        }
    }

    private void CleanupInactiveUsers()
    {
        try
        {
            var ids = eng.CachedWorldUserIds.ToList();
            if (ids.Count == 0)
                return;

            using var scope = scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<segusumDb>();
            var now = DateTime.Now;

            var inactiveIds = Utils.retry(() => db.user
                    .Where(u => ids.Contains(u.id))
                    .Select(u => new { u.id, u.dateLastAccess })
                    .ToList())
                .Where(u => u.dateLastAccess.HasValue
                    && now - u.dateLastAccess.Value > InactivityLimit)
                .Select(u => u.id)
                .ToList();

            foreach (var id in inactiveIds)
                eng.TryRemoveCachedWorld(id);
        }
        catch (Exception e)
        {
            // Il cleanup è opportunistico: un errore non deve arrestare l'host.
            logger.LogWarning(e, "Cleanup utenti Segusum fallito; verrà ritentato al prossimo intervallo.");
        }
    }
}
