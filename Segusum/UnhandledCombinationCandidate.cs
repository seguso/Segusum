using System;

namespace Seg;

/// <summary>
/// A combination concretely available in the current world state without a handler.
/// </summary>
public sealed class UnhandledCombinationCandidate
{
    internal UnhandledCombinationCandidate(string category, LogicObj? firstObject,
        Objective? firstObjective, LogicObj? secondObject, Objective? secondObjective,
        string firstKind, string secondKind, string? firstCodeName, string? secondCodeName)
    {
        Category = category;
        FirstObject = firstObject;
        FirstObjective = firstObjective;
        SecondObject = secondObject;
        SecondObjective = secondObjective;
        FirstKind = firstKind;
        SecondKind = secondKind;
        FirstCodeName = firstCodeName;
        SecondCodeName = secondCodeName;
    }

    public string Category { get; }
    public LogicObj? FirstObject { get; }
    public Objective? FirstObjective { get; }
    public LogicObj? SecondObject { get; }
    public Objective? SecondObjective { get; }
    public string FirstKind { get; }
    public string SecondKind { get; }
    public string? FirstCodeName { get; }
    public string? SecondCodeName { get; }
}
