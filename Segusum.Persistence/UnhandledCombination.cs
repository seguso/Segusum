using System;

namespace Seg;

/// <summary>EF model for the dynamic audit of currently available combinations.</summary>
public class UnhandledCombination
{
    public long id { get; set; }
    public int gameId { get; set; }
    public string category { get; set; } = "";
    public string firstId { get; set; } = "";
    public string? firstCodeName { get; set; }
    public string firstName { get; set; } = "";
    public string firstKind { get; set; } = "";
    public string secondId { get; set; } = "";
    public string? secondCodeName { get; set; }
    public string secondName { get; set; } = "";
    public string secondKind { get; set; } = "";
    public DateTime firstSeenUtc { get; set; }
    public DateTime lastSeenUtc { get; set; }
    public int seenCount { get; set; }
    public bool isIgnored { get; set; }
    public string? ignoreReason { get; set; }
    public DateTime? ignoredAtUtc { get; set; }
}
