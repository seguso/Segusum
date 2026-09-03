using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Seg;

internal static class UnhandledCombinationCandidates
{
    private const string Combine = "combine";
    private const string UseFor = "useFor";
    private const string SpecialVerb = "specialVerb";

    internal static IReadOnlyList<UnhandledCombinationCandidate> Find(WorldBase world)
    {
        if (world.activeChar == null || world.curRoom == null)
            return Array.Empty<UnhandledCombinationCandidate>();

        var inventory = world.activeChar.inv.Where(IsRealObject).Distinct().ToList();
        var combinableInventory = inventory.Where(x => x.HoverActionWhenInInv == HoverActionWhenInInv.UseWith).ToList();
        var useForInventory = inventory.Where(x => x.HoverActionWhenInInv == HoverActionWhenInInv.UseFor).ToList();
        var roomTargets = world.curRoom.objectsInRoom.Where(IsSelectableRoomTarget).Distinct().ToList();
        var useForRoomObjects = roomTargets.Where(x => x.UseKindWhenInRoom == UseKindForRoomObjects.UseFor).ToList();
        var result = new List<UnhandledCombinationCandidate>();

        foreach (var first in combinableInventory)
        foreach (var second in roomTargets)
            if (!world.combineHandlers.Any(h => h.lo1 == first && h.lo2 == second))
                result.Add(ObjectCandidate(Combine, first, second, "object",
                    second is Character ? "character" : "object", world));

        foreach (var objective in world.curObjectives.Distinct())
        foreach (var first in useForInventory.Concat(useForRoomObjects).Distinct())
            if (!world.useForHandlers.Any(h => h.Lo == first && h.Objective == objective))
                result.Add(new UnhandledCombinationCandidate(UseFor, first, null, null, objective,
                    "object", "objective", CodeName(world, first), CodeName(world, objective)));

        AddSpecialVerbCandidates(result, world.loHideInside(), roomTargets, world);
        AddSpecialVerbCandidates(result, world.loClimb(), roomTargets, world);

        var disguise = world.loDisguiseAs();
        if (disguise != null)
            foreach (var character in roomTargets.OfType<Character>())
                if (!world.combineHandlers.Any(h => h.lo1 == disguise && h.lo2 == character))
                    result.Add(ObjectCandidate(SpecialVerb, disguise, character,
                        "specialVerb", "character", world));

        return result;
    }

    private static void AddSpecialVerbCandidates(List<UnhandledCombinationCandidate> result,
        LogicObj? verb, IEnumerable<LogicObj> roomTargets, WorldBase world)
    {
        if (verb == null) return;
        foreach (var target in roomTargets.Where(x => x is not Character))
            if (!world.combineHandlers.Any(h => h.lo1 == verb && h.lo2 == target))
                result.Add(ObjectCandidate(SpecialVerb, verb, target, "specialVerb", "object", world));
    }

    private static UnhandledCombinationCandidate ObjectCandidate(string category, LogicObj first,
        LogicObj second, string firstKind, string secondKind, WorldBase world) =>
        new(category, first, null, second, null, firstKind, secondKind,
            CodeName(world, first), CodeName(world, second));

    private static bool IsRealObject(LogicObj lo) =>
        lo != null && !lo.IsConcept && !lo.IsConversationTopic && !lo.onlyInGraphics;

    private static bool IsSelectableRoomTarget(LogicObj lo) =>
        IsRealObject(lo) && !lo.IsExit && !lo.isInCurParty();

    private static string? CodeName(WorldBase world, object value) =>
        FindFields(world).FirstOrDefault(f => ReferenceEquals(f.GetValue(world), value))?.Name
        ?? (value as LogicObj)?.loId ?? (value as Objective)?.serId;

    private static IEnumerable<FieldInfo> FindFields(WorldBase world)
    {
        for (var type = world.GetType(); type != null; type = type.BaseType)
            foreach (var field in type.GetFields(BindingFlags.Instance | BindingFlags.Public |
                                                 BindingFlags.NonPublic | BindingFlags.DeclaredOnly))
                yield return field;
    }
}
