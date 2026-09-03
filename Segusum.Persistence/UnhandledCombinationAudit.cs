using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Seg;

/// <summary>
/// Database adapter for the engine's dynamic candidate audit. Database errors
/// are deliberately isolated from gameplay.
/// </summary>
public static class UnhandledCombinationAudit
{
    private sealed class WorldState
    {
        public string? Fingerprint { get; set; }
        public DateTime LastAttemptUtc { get; set; }
    }

    private static readonly ConditionalWeakTable<WorldBase, WorldState> State = new();

    public static void Synchronize(WorldBase world, segusumDb db, int gameId)
    {
        try
        {
            var candidates = world.GetUnhandledCombinationCandidates()
                .GroupBy(Key, StringComparer.Ordinal)
                .Select(group => group.First())
                .ToList();
            // A new engine turn is a new observation even when it happens to
            // expose the same candidate set again. Repeated rendering within
            // one turn is the case we suppress.
            var fingerprint = world.cur_time + "\n" +
                string.Join("\n", candidates.Select(Key).OrderBy(x => x, StringComparer.Ordinal));
            var state = State.GetOrCreateValue(world);
            var now = DateTime.UtcNow;

            // A room description can be requested more than once without any
            // game-state change. Avoid a pointless database round-trip then,
            // while allowing a retry after a transient outage.
            if (state.Fingerprint == fingerprint && now - state.LastAttemptUtc < TimeSpan.FromMinutes(1))
                return;
            state.Fingerprint = fingerprint;
            state.LastAttemptUtc = now;

            var rows = db.unhandledCombination.Where(x => x.gameId == gameId).ToList();
            foreach (var row in rows.Where(x => !ReferencesExistingGameEntities(world, x)).ToList())
            {
                db.unhandledCombination.Remove(row);
                rows.Remove(row);
            }
            foreach (var row in rows.Where(x => IsHandled(world, x)).ToList())
            {
                db.unhandledCombination.Remove(row);
                rows.Remove(row);
            }

            foreach (var candidate in candidates)
            {
                var firstId = IdOf(candidate.FirstObject, candidate.FirstObjective);
                var secondId = IdOf(candidate.SecondObject, candidate.SecondObjective);
                var row = rows.FirstOrDefault(x => x.category == candidate.Category &&
                    x.firstId == firstId && x.secondId == secondId);
                row ??= rows.FirstOrDefault(x => x.category == candidate.Category &&
                    x.firstCodeName == candidate.FirstCodeName && x.secondCodeName == candidate.SecondCodeName);

                if (row == null)
                {
                    row = new UnhandledCombination
                    {
                        gameId = gameId, category = candidate.Category,
                        firstId = firstId, firstCodeName = candidate.FirstCodeName,
                        firstName = Name(world, candidate.FirstObject, candidate.FirstObjective),
                        firstKind = candidate.FirstKind,
                        secondId = secondId, secondCodeName = candidate.SecondCodeName,
                        secondName = Name(world, candidate.SecondObject, candidate.SecondObjective),
                        secondKind = candidate.SecondKind,
                        firstSeenUtc = now, lastSeenUtc = now, seenCount = 1
                    };
                    db.unhandledCombination.Add(row);
                    rows.Add(row);
                }
                else
                {
                    row.firstId = firstId;
                    row.firstCodeName = candidate.FirstCodeName;
                    row.firstName = Name(world, candidate.FirstObject, candidate.FirstObjective);
                    row.secondId = secondId;
                    row.secondCodeName = candidate.SecondCodeName;
                    row.secondName = Name(world, candidate.SecondObject, candidate.SecondObjective);
                    row.lastSeenUtc = now;
                    row.seenCount++;
                }
            }
            db.SaveChanges();
        }
        catch (Exception ex)
        {
            // Audit is observability only: an unavailable database must never
            // prevent the player from continuing the game.
            System.Diagnostics.Debug.WriteLine($"[combination-audit] {ex}");
        }
    }

    private static string Key(UnhandledCombinationCandidate c) =>
        $"{c.Category}\u001f{IdOf(c.FirstObject, c.FirstObjective)}\u001f{IdOf(c.SecondObject, c.SecondObjective)}";

    private static string IdOf(LogicObj? obj, Objective? objective) =>
        obj?.loId ?? objective?.serId ?? "";

    private static string Name(WorldBase world, LogicObj? obj, Objective? objective) =>
        obj != null ? obj.dynamicNameTranslated(world.getXdocObjIndexedCached(), false, false)
        : objective?.translated_name(world, world.getXdocObjIndexedCached()) ?? "";

    private static bool ReferencesExistingGameEntities(WorldBase world, UnhandledCombination row)
    {
        if (row.category == "useFor")
            return ExistingLogicObj(world, row.firstId, row.firstCodeName) &&
                   ExistingObjective(world, row.secondId, row.secondCodeName);
        return ExistingLogicObj(world, row.firstId, row.firstCodeName) &&
               ExistingLogicObj(world, row.secondId, row.secondCodeName);
    }

    private static bool ExistingLogicObj(WorldBase world, string id, string? codeName) =>
        (!string.IsNullOrWhiteSpace(id) && world.loOfId.ContainsKey(id)) ||
        FindFieldValue<LogicObj>(world, codeName) != null;

    private static bool ExistingObjective(WorldBase world, string id, string? codeName) =>
        (!string.IsNullOrWhiteSpace(id) && world.objectiveOfId.ContainsKey(id)) ||
        FindFieldValue<Objective>(world, codeName) != null;

    private static bool IsHandled(WorldBase world, UnhandledCombination row)
    {
        if (row.category == "useFor")
        {
            var first = FindLogicObj(world, row.firstId, row.firstCodeName);
            if (first != null && !CanBeUsedFor(first)) return true;
            return world.useForHandlers.Any(h => h.Lo.loId == row.firstId && h.Objective.serId == row.secondId);
        }
        if (row.category == "combine")
        {
            var first = FindLogicObj(world, row.firstId, row.firstCodeName);
            if (first != null && first.HoverActionWhenInInv != HoverActionWhenInInv.UseWith) return true;
        }
        return world.combineHandlers.Any(h => h.lo1.loId == row.firstId && h.lo2.loId == row.secondId);
    }

    private static LogicObj? FindLogicObj(WorldBase world, string id, string? codeName) =>
        (!string.IsNullOrWhiteSpace(id) && world.loOfId.TryGetValue(id, out var byId))
            ? byId : FindFieldValue<LogicObj>(world, codeName);

    private static bool CanBeUsedFor(LogicObj obj) =>
        obj.HoverActionWhenInInv == HoverActionWhenInInv.UseFor ||
        obj.UseKindWhenInRoom == UseKindForRoomObjects.UseFor;

    private static T? FindFieldValue<T>(WorldBase world, string? name) where T : class =>
        string.IsNullOrWhiteSpace(name) ? null : FindFields(world)
            .FirstOrDefault(f => f.Name == name)?.GetValue(world) as T;

    private static IEnumerable<System.Reflection.FieldInfo> FindFields(WorldBase world)
    {
        for (var type = world.GetType(); type != null; type = type.BaseType)
            foreach (var field in type.GetFields(System.Reflection.BindingFlags.Instance |
                System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic |
                System.Reflection.BindingFlags.DeclaredOnly)) yield return field;
    }
}
