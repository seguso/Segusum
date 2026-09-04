using Seg;

namespace PackageConsumer;

public partial class World : WorldBase
{
    protected World() : base("en") { }

    public Character mike = null!;
    public Objective goal = null!;
    public Explanation explanation = null!;

    protected override void configureActionHandlers() { }

    public override EndGameStuffClient getEndGameData() => new("", Array.Empty<string>());
    public override Explanation[] getGlobalExplanations() => Array.Empty<Explanation>();
    public override bool fillerIsVisible(Filler fi) => true;
    public override bool templateIsVisible(Template te) => true;
    public override bool explanationIsVisibleForTextInput(TextInput ti, Explanation e) => true;
    public override bool explanationIsVisible(Explanation e) => true;
    public override List<Dialog> dialogsToSerialize() => new();
    public override void after_action_executed(CutScene cs, ActionContext actionContext) { }
    public override string dynamicObjectName(LogicObj lo, bool withArticle, bool isForDialog) => lo.name;
    public override string dynamicRoomName(Room ro) => ro.roomId;
    public override void startGameCutScene() { }
    public override void beforeWalkPathResetVariables() { }
    public override void beforeRoomChangeManual(Room from, Room to, WalkPath pathFromTo, WalkPath completePath, BeforeRoomChangeInput i) { }
    public override void beforeRoomChangeManualAndAutoSetRoomAspects(Room roomTarget) { }
    public override void beforeExecuteDialogSetAspects() { }
    public override bool rebuildXmlToTranslateObjects(out string lang) { lang = ""; return false; }
    public override void onWalkPathNotFound(Room roomTarget) { }
    public override LogicObj loHideInside() => null!;
    public override LogicObj loClimb() => null!;
    public override LogicObj loDisguiseAs() => null!;
    public override string graphicsRootFolderName() => "";
    public override Cycle getRoomCycle(Room r) => new();
    public override void setStartState() { }
    public override void beforeActionExecuted(LogicObj lo, Objective obj, Room ro, out bool cancel) => cancel = false;
    public override string imgNotAvailable() => "";
    public override void rememberFailedOnObject(LogicObj lo) { }
    public CycleElemId GeneratedCycleElement => xww7;
    public Cycle CallGeneratedDslHelper() => creaCicloMikeNonRipete();
}
