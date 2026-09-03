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

    private sealed class Factory(DbContextOptions<segusumDb> options) : IDbContextFactory<segusumDb>
    {
        public segusumDb CreateDbContext() => new(options);
        public Task<segusumDb> CreateDbContextAsync(CancellationToken cancellationToken = default) => Task.FromResult(new segusumDb(options));
    }
}
