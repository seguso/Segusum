using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Seg;

/// <summary>Database-backed delivery bridge. The engine only sees transient client messages.</summary>
internal static class AdminNarrativeQueue
{
    public static void RefreshPending(WorldBase world, segusumDb db, int userId)
    {
        try
        {
            var activeIds = world.gs is GameStateCutScene active
                ? active.cs.OfType<NarToken>().Where(x => x.adminNarrativeMessageId.HasValue).Select(x => x.adminNarrativeMessageId!.Value).ToHashSet()
                : new HashSet<long>();
            var messages = db.adminNarrativeMessage.AsNoTracking()
                .Where(x => x.userId == userId && !x.cancelled && !x.seenAtUtc.HasValue)
                .OrderBy(x => x.id).ToList();
            world.adminNarrativeMessagesPending = messages.Where(x => !activeIds.Contains(x.id)).Select(x => new AdminNarrativeMessageClient(x.id, ParseTexts(x.narTextsJson))).ToList();
        }
        catch (Exception e)
        {
            // Monitoring must never make the game unavailable.
            SegusumProfiler.Log($"admin narrative refresh failed: {e.GetType().Name}: {e.Message}");
            world.adminNarrativeMessagesPending.Clear();
        }
    }

    public static bool MarkDelivered(segusumDb db, int userId, IEnumerable<long> ids)
    {
        try
        {
            var wanted = ids.Distinct().ToArray();
            if (wanted.Length == 0) return true;
            var now = DateTime.UtcNow;
            var rows = db.adminNarrativeMessage.Where(x => x.userId == userId && wanted.Contains(x.id) && !x.cancelled).ToList();
            foreach (var row in rows) row.deliveredAtUtc ??= now;
            db.SaveChanges();
            return true;
        }
        catch (Exception e) { SegusumProfiler.Log($"admin narrative delivery update failed: {e.GetType().Name}: {e.Message}"); return false; }
    }

    public static void MarkSeen(segusumDb db, int userId, IEnumerable<long> ids)
    {
        try
        {
            var wanted = ids.Distinct().ToArray();
            if (wanted.Length == 0) return;
            var now = DateTime.UtcNow;
            var rows = db.adminNarrativeMessage.Where(x => x.userId == userId && wanted.Contains(x.id) && !x.cancelled).ToList();
            foreach (var row in rows) { row.deliveredAtUtc ??= now; row.seenAtUtc ??= now; }
            db.SaveChanges();
        }
        catch (Exception e) { SegusumProfiler.Log($"admin narrative ACK failed: {e.GetType().Name}: {e.Message}"); }
    }

    private static IReadOnlyList<string> ParseTexts(string json)
    {
        try { return JsonSerializer.Deserialize<string[]>(json) ?? Array.Empty<string>(); }
        catch { return Array.Empty<string>(); }
    }
}
