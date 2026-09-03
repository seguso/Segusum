using System;

namespace Seg;

/// <summary>Message authored by an administrator for one game account.</summary>
public sealed class AdminNarrativeMessage
{
    public long id { get; set; }
    public int userId { get; set; }
    public int gameId { get; set; }
    public string category { get; set; } = "";
    public string firstId { get; set; } = "";
    public string secondId { get; set; } = "";
    public string? explanationId { get; set; }
    // This is the message payload, not a game save. It preserves the existing DB schema.
    public string narTextsJson { get; set; } = "[]";
    public DateTime createdAtUtc { get; set; }
    public DateTime? deliveredAtUtc { get; set; }
    public DateTime? seenAtUtc { get; set; }
    public bool cancelled { get; set; }
}
