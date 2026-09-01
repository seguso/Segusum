namespace Seg
{
    public enum TutorialPromptKind
    {
        UseWith,
        UseFor,
        IsActually,
        HideInside,
        DisguiseAs
    }

    /// <summary>
    /// Contesto calcolato dal motore prima di aprire una finestra tutorial.
    /// Il mondo autore riceve il risultato della selezione delle explanation,
    /// non deve ricostruirlo in parallelo.
    /// </summary>
    public sealed class TutorialPromptContext
    {
        public TutorialPromptKind Kind { get; init; }
        public bool IsCasual { get; init; }
        public LogicObj FirstObject { get; init; }
        public LogicObj SecondObject { get; init; }
        public Objective Objective { get; init; }
        public string ActivePreamble { get; init; }
        public Explanation[] VisibleExplanations { get; init; } = System.Array.Empty<Explanation>();
        public bool ExplanationWillBeRequested { get; init; }
    }
}
