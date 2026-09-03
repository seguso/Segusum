using Microsoft.EntityFrameworkCore;
using Seg;
using Segusum.Admin;
using Xunit;

namespace Segusum.Admin.Tests;

public sealed class AdminDataServiceTests
{
    [Fact]
    public async Task UnhandledRowsCanBeFilteredIgnoredAndReactivated()
    {
        var options = new DbContextOptionsBuilder<segusumDb>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using (var seed = new segusumDb(options))
        {
            seed.unhandledCombination.Add(new UnhandledCombination { gameId = 3, category = "combine", firstId = "a", secondId = "b", firstName = "a", secondName = "b", firstKind = "object", secondKind = "object", firstSeenUtc = DateTime.UtcNow, lastSeenUtc = DateTime.UtcNow, seenCount = 4 });
            seed.unhandledCombination.Add(new UnhandledCombination { gameId = 3, category = "useFor", firstId = "c", secondId = "goal", firstName = "c", secondName = "goal", firstKind = "object", secondKind = "objective", firstSeenUtc = DateTime.UtcNow, lastSeenUtc = DateTime.UtcNow, seenCount = 1, isIgnored = true, ignoreReason = "spoiler", ignoredAtUtc = DateTime.UtcNow });
            await seed.SaveChangesAsync();
        }
        var service = new AdminDataService(new Factory(options));
        Assert.Single(await service.FindUnhandledAsync(new(3, "a", "combine", false, 2, "count", true)));
        var id = (await service.FindUnhandledAsync(new(3, "a", "combine", false, 1, "count", true))).Single().id;
        await service.SetIgnoredAsync(id, "nonsense");
        Assert.Empty(await service.FindUnhandledAsync(new(3, "a", "combine", false, 1, "count", true)));
        await service.ReactivateAsync(id);
        Assert.Single(await service.FindUnhandledAsync(new(3, "a", "combine", false, 1, "count", true)));
    }

    [Fact]
    public void ParsesCurrentAndLegacyXmlPastActionsAndCycles()
    {
        var u = new user { id = 8, uname = "m", gameId = 2 };
        var actions = AdminDataService.ParseActions("<world><past_action_use_with lo1Id=\"a\" lo2Id=\"b\" expl=\"e\" handler_called=\"N\" time=\"2026-01-01T00:00:00Z\" /><past_action_use_for loId=\"a\" objId=\"o\" time=\"2026-01-01T00:01:00Z\" /></world>", u);
        Assert.Equal(2, actions.Count);
        Assert.False(actions[0].HandlerCalled);
        Assert.Null(actions[1].HandlerCalled); // old XML without handler_called remains readable
        Assert.Equal(2, AdminDataService.ParseCycles("<world><cycleElem id=\"ciao\" howMany=\"3\" lastTime=\"2026-01-01T00:00:00Z\" /><cycleElem name=\"old\" howMany=\"1\" /></world>").Count);
    }

    [Fact]
    public async Task PastActionsCanHideHandledAttempts()
    {
        var options = new DbContextOptionsBuilder<segusumDb>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using (var seed = new segusumDb(options))
        {
            seed.user.Add(new user { id = 1, uname = "m", gameId = 2 });
            seed.savegame.Add(new savegame { idUser = 1, savegameTitle = "", dateModified = DateTime.UtcNow, savegameXml = "<world><past_action_use_with lo1Id=\"a\" lo2Id=\"b\" handler_called=\"Y\" time=\"2026-01-01T00:00:00Z\" /><past_action_use_with lo1Id=\"c\" lo2Id=\"d\" handler_called=\"N\" time=\"2026-01-01T00:00:00Z\" /></world>" });
            await seed.SaveChangesAsync();
        }
        var service = new AdminDataService(new Factory(options));
        var onlyUnhandled = await service.FindPastActionsAsync(new(2, "", "", null, "", 1, true, true));
        Assert.Single(onlyUnhandled);
        Assert.Equal("c", onlyUnhandled[0].FirstId);
        var all = await service.FindPastActionsAsync(new(2, "", "", null, "", 1, true, false));
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task ProblemCombinationsAreGroupedPerUserAndTrackMessageProgress()
    {
        var options = new DbContextOptionsBuilder<segusumDb>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var firstAttempt = DateTime.UtcNow.AddMinutes(-10);
        await using (var seed = new segusumDb(options))
        {
            seed.user.AddRange(new user { id = 1, uname = "a", gameId = 2 }, new user { id = 2, uname = "b", gameId = 2 });
            seed.savegame.AddRange(
                new savegame { idUser = 1, savegameTitle = "", dateModified = DateTime.UtcNow, savegameXml =
                    $"<world>{string.Join("", Enumerable.Range(0, 5).Select(i => $"<past_action_use_with lo1Id=\"key\" lo2Id=\"door\" expl=\"e{i % 2}\" handler_called=\"N\" time=\"{firstAttempt.AddMinutes(i):O}\" />"))}</world>" },
                new savegame { idUser = 2, savegameTitle = "", dateModified = DateTime.UtcNow, savegameXml = $"<world><past_action_use_with lo1Id=\"key\" lo2Id=\"door\" expl=\"e0\" handler_called=\"N\" time=\"{firstAttempt:O}\" /></world>" });
            seed.adminNarrativeMessage.Add(new AdminNarrativeMessage
            {
                userId = 1, gameId = 2, category = "useWith", firstId = "key", secondId = "door", explanationId = "e0",
                narTextsJson = "[\"try again\"]", createdAtUtc = firstAttempt.AddMinutes(1), seenAtUtc = firstAttempt.AddMinutes(2)
            });
            await seed.SaveChangesAsync();
        }

        var service = new AdminDataService(new Factory(options));
        var rows = await service.FindProblemCombinationsAsync(new(2, "", "", null, "", 1, true, true));

        Assert.Equal(3, rows.Count); // user 1 has two explanations; user 2 has one
        var userOneE0 = Assert.Single(rows.Where(x => x.UserId == 1 && x.Explanation == "e0"));
        Assert.Equal(3, userOneE0.AttemptCount);
        Assert.Equal(1, userOneE0.SentMessageCount);
        Assert.Equal(1, userOneE0.SeenMessageCount);
        Assert.Equal(firstAttempt.AddMinutes(2), userOneE0.LastMessageSeenUtc);
        Assert.True(userOneE0.RetriedAfterLastMessage);
        Assert.All(rows.Where(x => x.UserId == 2), x => Assert.Equal(0, x.SentMessageCount));
    }

    [Fact]
    public async Task GenericAdminMessageKeepsUserAndOptionalCombinationEmpty()
    {
        var options = new DbContextOptionsBuilder<segusumDb>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        var service = new AdminDataService(new Factory(options));
        var id = await service.QueueAdminMessageAsync(42, 7, "", "", "", null, "Prima /  Second");

        await using var db = new segusumDb(options);
        var row = await db.adminNarrativeMessage.SingleAsync(x => x.id == id);
        Assert.Equal(42, row.userId);
        Assert.Equal("", row.category);
        Assert.Equal("", row.firstId);
        Assert.Equal("", row.secondId);
        Assert.Equal("[\"Prima\",\"Second\"]", row.narTextsJson);
    }

    private sealed class Factory(DbContextOptions<segusumDb> options) : IDbContextFactory<segusumDb>
    {
        public segusumDb CreateDbContext() => new(options);
        public Task<segusumDb> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(new segusumDb(options));
    }
}
