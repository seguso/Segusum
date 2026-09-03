namespace Seg;

/// <summary>Transient message data supplied by an integration layer.</summary>
public sealed record AdminNarrativeMessageClient(long Id, IReadOnlyList<string> NarTexts);
