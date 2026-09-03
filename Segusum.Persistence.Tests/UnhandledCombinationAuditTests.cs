using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Seg;

namespace Segusum.Persistence.Tests;

public sealed class UnhandledCombinationAuditTests
{
    [Fact]
    public void ReusesHistoricalRowAndPreservesManualIgnoreFields()
    {
        var world = new AuditWorld();
        using var db = new segusumDb(new DbContextOptionsBuilder<segusumDb>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString("N"))
            .Options);

        UnhandledCombinationAudit.Synchronize(world, db, 17);
        var row = Assert.Single(db.unhandledCombination.Where(x =>
            x.category == "combine" && x.firstId == world.Tool.loId && x.secondId == world.Target.loId));
        var firstSeen = row.firstSeenUtc;
        var ignoredAt = new DateTime(2026, 8, 20, 12, 30, 0, DateTimeKind.Utc);
        row.isIgnored = true;
        row.ignoreReason = "nonsense";
        row.ignoredAtUtc = ignoredAt;
        db.SaveChanges();

        // The audit suppresses repeated observations of the same engine state.
        // Advance the engine clock to represent a later observation.
        typeof(WorldBase).GetField("cur_time", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(world, (ulong)1);
        UnhandledCombinationAudit.Synchronize(world, db, 17);

        var same = Assert.Single(db.unhandledCombination.Where(x => x.id == row.id));
        Assert.Equal(row.id, same.id);
        Assert.Equal(firstSeen, same.firstSeenUtc);
        Assert.Equal(2, same.seenCount);
        Assert.True(same.isIgnored);
        Assert.Equal("nonsense", same.ignoreReason);
        Assert.Equal(ignoredAt, same.ignoredAtUtc);

        // Historical audit rows remain available when a candidate disappears.
        world.Target.removeFromWorld();
        typeof(WorldBase).GetField("cur_time", BindingFlags.Instance | BindingFlags.NonPublic)!
            .SetValue(world, (ulong)2);
        UnhandledCombinationAudit.Synchronize(world, db, 17);
        Assert.Contains(db.unhandledCombination, x => x.id == row.id);
    }

    private sealed class AuditWorld : WorldBase
    {
        private readonly Character active = new() { loId = "active", name = "active" };
        private readonly Room room = new() { roomId = "room" };
        private readonly LogicObj tool = new() { loId = "tool", name = "tool", HoverActionWhenInInv = HoverActionWhenInInv.UseWith };
        private readonly LogicObj target = new() { loId = "target", name = "target" };

        internal LogicObj Target => target;
        internal LogicObj Tool => tool;

        internal AuditWorld() : base("it")
        {
            ActiveChar = active;
            active.putInRoom(room);
            active.pickUp(tool);
            target.putInRoom(room);
        }

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
    }
}
