using Seg;

namespace Segusum.Tests;

public sealed class RememberTests
{
    [Fact]
    public void RememberabilityUsesOnlySeenNamedCutScenes()
    {
        var world = new RememberWorld();

        Assert.False(world.CanRemember(world.Object));

        var unseen = world.AddScene("unseen", world.Object);
        Assert.False(world.CanRemember(world.Object));

        world.MarkSeen(unseen);
        Assert.True(world.CanRemember(world.Object));
    }

    [Fact]
    public void CharactersUseTheSameRememberabilityRuleAndPreserveSceneOrder()
    {
        var world = new RememberWorld();
        var first = world.AddScene("first", world.Character);
        var second = world.AddScene("second", world.Character);
        world.MarkSeen(first);
        world.MarkSeen(second);

        Assert.True(world.CanRemember(world.Character));
        Assert.Equal(new[] { "first", "second" },
            world.RememberingScenes(world.Character).Select(x => x.id.serId));
    }

    [Fact]
    public void RememberPastActionKeepsTheLogicObjectAndTimestamp()
    {
        var at = new DateTime(2026, 9, 6, 12, 34, 56, DateTimeKind.Utc);
        var world = new RememberWorld();
        var action = new PastActionRemember(world.Object, at);

        Assert.Same(world.Object, action.lo);
        Assert.Equal(at, action.dateTime);
    }

    [Fact]
    public void RememberPastActionSurvivesSavegameRoundTrip()
    {
        var at = new DateTime(2026, 9, 6, 12, 34, 56, DateTimeKind.Utc);
        var world = new RememberWorld();
        world.AddPastAction(new PastActionRemember(world.Object, at));

        var serialized = world.serialize();
        var restored = new RememberWorld();
        restored.deserialize(serialized, out var invalid);

        Assert.False(invalid);
        var action = Assert.Single(restored.PastActions.OfType<PastActionRemember>());
        Assert.Equal("object", action.lo.loId);
        Assert.Equal(at, action.dateTime);
    }

    [Fact]
    public void BeforeRoomChangeGeneratedHookTakesOwnershipWithoutDoubleInvocation()
    {
        var world = new RememberWorld();
        var from = world.Room;
        var to = new Room { roomId = "to" };
        var segment = new WalkPath { locations = new[] { from, to }.ToList() };
        var completePath = new WalkPath { locations = new[] { from, to, world.Room }.ToList() };
        var input = new BeforeRoomChangeInput();

        world.GeneratedBeforeRoomChange = true;
        world.RunBeforeRoomChange(from, to, segment, completePath, input);

        Assert.Equal(0, world.ManualBeforeRoomChangeCalls);
        Assert.Equal(1, world.GeneratedBeforeRoomChangeCalls);
        Assert.Same(from, world.LastGeneratedFrom);
        Assert.Same(to, world.LastGeneratedTo);
        Assert.Same(segment, world.LastGeneratedSegment);
        Assert.Same(completePath, world.LastGeneratedFullPath);
        Assert.Same(input, world.LastGeneratedInput);
        Assert.False(input.canChangeRoom);
    }

    [Fact]
    public void BeforeRoomChangeWithoutGeneratedDeclarationUsesLegacyOverride()
    {
        var world = new RememberWorld();
        var room = world.Room;
        var path = new WalkPath { locations = new[] { room }.ToList() };
        var input = new BeforeRoomChangeInput();

        world.RunBeforeRoomChange(room, room, path, path, input);

        Assert.Equal(1, world.ManualBeforeRoomChangeCalls);
        Assert.Equal(0, world.GeneratedBeforeRoomChangeCalls);
        Assert.True(input.canChangeRoom);
    }

    private sealed class RememberWorld : WorldBase
    {
        private readonly Character active = new() { loId = "active", name = "active" };
        private readonly LogicObj objectToRemember = new() { loId = "object", name = "object" };
        private readonly Character characterToRemember = new() { loId = "character", name = "character" };
        private readonly Room room = new() { roomId = "room" };

        internal RememberWorld() : base("it")
        {
            ActiveChar = active;
            active.putInRoom(room);
            objectToRemember.putInRoom(room);
            characterToRemember.putInRoom(room);
            gs = new GameStateViewingRoom();
        }

        internal LogicObj Object => objectToRemember;
        internal Character Character => characterToRemember;
        internal Room Room => room;
        internal bool GeneratedBeforeRoomChange { get; set; }
        internal int ManualBeforeRoomChangeCalls { get; private set; }
        internal int GeneratedBeforeRoomChangeCalls { get; private set; }
        internal Room? LastGeneratedFrom { get; private set; }
        internal Room? LastGeneratedTo { get; private set; }
        internal WalkPath? LastGeneratedSegment { get; private set; }
        internal WalkPath? LastGeneratedFullPath { get; private set; }
        internal BeforeRoomChangeInput? LastGeneratedInput { get; private set; }
        internal bool CanRemember(LogicObj lo) => canBeRemembered(lo);
        internal List<NamedCutScene> RememberingScenes(LogicObj lo) => namedCutScenesRemembering(lo);
        internal IReadOnlyList<PastAction> PastActions => pastActions;
        internal void AddPastAction(PastAction action) => pastActions.Add(action);
        internal void RunBeforeRoomChange(Room from, Room to, WalkPath segment, WalkPath fullPath, BeforeRoomChangeInput input)
            => invokeBeforeRoomChange(from, to, segment, fullPath, input);

        internal NamedCutScene AddScene(string id, LogicObj mentioned)
        {
            var scene = new NamedCutScene(new NamedCutSceneId { serId = id, titleUntranslated = id })
            {
                cs = new CutScene(canBeSkipped: true),
                oggettiMenzionati = new List<Mentionable> { mentioned },
                roomDoveEri = room
            };
            return scene;
        }

        internal void MarkSeen(NamedCutScene scene) => namedCutScenesSeen.Add(scene);

        public override EndGameStuffClient getEndGameData() => null!;
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
        protected override bool hasGeneratedBeforeRoomChange => GeneratedBeforeRoomChange;
        protected override void beforeRoomChangeGenerated(Room from, Room to, WalkPath pathFromTo, WalkPath completePath, BeforeRoomChangeInput i)
        {
            GeneratedBeforeRoomChangeCalls++;
            LastGeneratedFrom = from;
            LastGeneratedTo = to;
            LastGeneratedSegment = pathFromTo;
            LastGeneratedFullPath = completePath;
            LastGeneratedInput = i;
            i.canChangeRoom = false;
        }
        public override void beforeRoomChangeManual(Room from, Room to, WalkPath pathFromTo, WalkPath completePath, BeforeRoomChangeInput i) => ManualBeforeRoomChangeCalls++;
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
    }
}
