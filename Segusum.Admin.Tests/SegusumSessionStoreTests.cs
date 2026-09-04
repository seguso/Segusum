using Segusum.AspNetCore;
using Xunit;

namespace Segusum.Admin.Tests;

public sealed class SegusumSessionStoreTests
{
    [Fact]
    public void CreatesOpaqueHighEntropyTokenAndFindsSession()
    {
        var store = new SegusumSessionStore();
        var first = store.Create(7, "mau", false, true, 42);
        var second = store.Create(7, "mau", false, true, 42);

        Assert.NotEqual(first, second);
        Assert.True(first.Length >= 43);
        Assert.True(store.TryGet(first, out var session));
        Assert.Equal(7, session.UserId);
        Assert.Equal("mau", session.Username);
        Assert.True(session.IsCasualMode);
    }

    [Fact]
    public async Task ConcurrentLookupAndRemovalAreSafe()
    {
        var store = new SegusumSessionStore();
        var tokens = Enumerable.Range(1, 100).Select(i => store.Create(i, $"u{i}", false, false, null)).ToArray();
        await Task.WhenAll(tokens.Select(token => Task.Run(() =>
        {
            for (var i = 0; i < 100; i++) store.TryGet(token, out _);
            store.Remove(token);
        })));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public void ExpiredSessionIsRejected()
    {
        var store = new SegusumSessionStore(TimeSpan.Zero);
        var token = store.Create(1, "u", false, false, null);
        Assert.False(store.TryGet(token, out _));
    }

    [Fact]
    public void TokenFromPreviousProcessIsNotAcceptedByFreshStore()
    {
        var oldStore = new SegusumSessionStore();
        var oldToken = oldStore.Create(7, "mau", false, false, 42);
        var restartedStore = new SegusumSessionStore();

        Assert.False(restartedStore.TryGet(oldToken, out _));

        var newToken = restartedStore.Create(7, "mau", false, false, 42);
        Assert.NotEqual(oldToken, newToken);
        Assert.True(restartedStore.TryGet(newToken, out var session));
        Assert.False(session.IsTextMode);
    }

    [Fact]
    public void AccessPersistenceIsThrottledPerUser()
    {
        var store = new SegusumSessionStore();
        var first = DateTimeOffset.UtcNow;
        Assert.True(store.ShouldPersistAccess(7, first));
        Assert.False(store.ShouldPersistAccess(7, first.AddSeconds(59)));
        Assert.True(store.ShouldPersistAccess(7, first.AddSeconds(60)));
        Assert.True(store.ShouldPersistAccess(8, first.AddSeconds(1)));
    }
}
