namespace Segusum.Translator.Core;

internal enum DiffKind { Equal, Replace, Delete, Insert }

internal sealed record DiffOp(DiffKind Kind, int OldStart, int OldLength, int NewStart, int NewLength)
{
    public int Length => Math.Min(OldLength, NewLength);
}

/// <summary>
/// Ordered anchor diff. Exact strings are matched globally, then a longest
/// increasing subsequence selects the largest deterministic set of anchors
/// that is common to both sequences. The gaps between anchors are the small
/// blocks where fuzzy matching is allowed. This avoids the old global
/// all-against-all distance calculation and is robust to moved entries.
/// </summary>
internal static class SequenceDiff
{
    public static IReadOnlyList<DiffOp> Build(IReadOnlyList<string> oldValues, IReadOnlyList<string> newValues)
    {
        var positions = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < newValues.Count; i++)
            positions.TryAdd(newValues[i], i);

        var candidates = new List<(int Old, int New)>();
        var seenOld = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (value, oldIndex) in oldValues.Select((value, index) => (value, index)))
            if (seenOld.Add(value) && positions.TryGetValue(value, out var newIndex))
                candidates.Add((oldIndex, newIndex));

        var anchors = LongestIncreasingSubsequence(candidates);

        var result = new List<DiffOp>(); var oldCursor = 0; var newCursor = 0;
        foreach (var anchor in anchors)
        {
            AddGap(result, oldCursor, anchor.Old - oldCursor, newCursor, anchor.New - newCursor);
            result.Add(new DiffOp(DiffKind.Equal, anchor.Old, 1, anchor.New, 1));
            oldCursor = anchor.Old + 1; newCursor = anchor.New + 1;
        }
        AddGap(result, oldCursor, oldValues.Count - oldCursor, newCursor, newValues.Count - newCursor);
        return result;
    }

    private static List<(int Old, int New)> LongestIncreasingSubsequence(
        IReadOnlyList<(int Old, int New)> candidates)
    {
        if (candidates.Count == 0) return new();
        var tails = new List<int>();
        var previous = Enumerable.Repeat(-1, candidates.Count).ToArray();
        for (var i = 0; i < candidates.Count; i++)
        {
            var low = 0; var high = tails.Count;
            while (low < high)
            {
                var middle = low + (high - low) / 2;
                if (candidates[tails[middle]].New < candidates[i].New) low = middle + 1;
                else high = middle;
            }
            if (low > 0) previous[i] = tails[low - 1];
            if (low == tails.Count) tails.Add(i);
            else tails[low] = i;
        }

        var result = new List<(int Old, int New)>();
        for (var index = tails[^1]; index >= 0; index = previous[index]) result.Add(candidates[index]);
        result.Reverse();
        return result;
    }

    private static void AddGap(List<DiffOp> result, int oldStart, int oldLength, int newStart, int newLength)
    {
        if (oldLength == 0 && newLength == 0) return;
        result.Add(new DiffOp(DiffKind.Replace, oldStart, oldLength, newStart, newLength));
    }
}
