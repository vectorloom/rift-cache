using RiftCache.Caching;
using RiftCache.Providers.Persistence;
using RiftCache.Tests.TestSupport;

namespace RiftCache.Tests.Caching;

public class InMemoryCacheStoreTests
{
    [Fact]
    public async Task SetThenGet_ReturnsStoredValue()
    {
        var store = CreateStore(out _, out _);

        await store.SetAsync("key", "value"u8.ToArray(), null, null);

        Assert.Equal("value"u8.ToArray(), await store.GetAsync("key"));
    }

    [Fact]
    public async Task Get_MissingKey_ReturnsNull()
    {
        var store = CreateStore(out _, out _);

        Assert.Null(await store.GetAsync("missing"));
    }

    [Fact]
    public async Task Get_PastAbsoluteExpiration_ReturnsNullAndRemovesEntry()
    {
        var store = CreateStore(out var time, out _);
        await store.SetAsync("key", [1], time.GetUtcNow().AddSeconds(10), null);

        time.Advance(TimeSpan.FromSeconds(11));

        Assert.Null(await store.GetAsync("key"));
        Assert.Equal(0, store.Count);
    }

    [Fact]
    public async Task Get_WithinSlidingExpiration_TouchesEntryAndExtendsItsLife()
    {
        var store = CreateStore(out var time, out _);
        await store.SetAsync("key", [1], null, TimeSpan.FromSeconds(10));

        time.Advance(TimeSpan.FromSeconds(8));
        Assert.NotNull(await store.GetAsync("key"));

        // Without the touch above this would now be past the original 10s sliding window.
        time.Advance(TimeSpan.FromSeconds(8));
        Assert.NotNull(await store.GetAsync("key"));
    }

    [Fact]
    public async Task Refresh_PastSlidingExpiration_ReturnsFalseAndRemovesEntry()
    {
        var store = CreateStore(out var time, out _);
        await store.SetAsync("key", [1], null, TimeSpan.FromSeconds(5));

        time.Advance(TimeSpan.FromSeconds(6));

        Assert.False(await store.RefreshAsync("key"));
        Assert.Null(await store.GetAsync("key"));
    }

    [Fact]
    public async Task Refresh_UnknownKey_ReturnsFalse()
    {
        var store = CreateStore(out _, out _);

        Assert.False(await store.RefreshAsync("missing"));
    }

    [Fact]
    public async Task Remove_ExistingKey_ReturnsTrueAndNotifiesPersistenceProvider()
    {
        var store = CreateStore(out _, out var persistence);
        await store.SetAsync("key", [1], null, null);

        Assert.True(await store.RemoveAsync("key"));
        Assert.Equal(1, persistence.RemoveCallCount);
    }

    [Fact]
    public async Task Remove_UnknownKey_ReturnsFalse()
    {
        var store = CreateStore(out _, out _);

        Assert.False(await store.RemoveAsync("missing"));
    }

    [Fact]
    public async Task Set_NotifiesPersistenceProvider()
    {
        var store = CreateStore(out _, out var persistence);

        await store.SetAsync("key", [1], null, null);

        Assert.Equal(1, persistence.PersistCallCount);
    }

    [Fact]
    public async Task Get_MissFallsThroughToPersistenceProvider_AndPromotesEntryToMemory()
    {
        var store = CreateStore(out var time, out var persistence);
        await persistence.PersistAsync("key", new CacheEntry([9], null, null, time.GetUtcNow()));

        var value = await store.GetAsync("key");

        Assert.Equal(new byte[] { 9 }, value);
        Assert.Equal(1, store.Count);
    }

    [Fact]
    public async Task EvictExpired_RemovesOnlyExpiredEntries()
    {
        var store = CreateStore(out var time, out _);
        await store.SetAsync("expires-soon", [1], time.GetUtcNow().AddSeconds(5), null);
        await store.SetAsync("no-expiration", [2], null, null);

        time.Advance(TimeSpan.FromSeconds(6));
        store.EvictExpired(time.GetUtcNow());

        Assert.Equal(1, store.Count);
    }

    [Fact]
    public async Task EvictLeastRecentlyUsed_OverCapacity_RemovesOldestAccessedEntriesFirst()
    {
        // NullPersistenceProvider here, not the CreateStore fake: eviction only demotes an
        // entry out of the memory tier, so a real read-through provider would still be able to
        // serve it. To observe the entry as genuinely gone, there must be nothing behind it.
        var time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        var store = new InMemoryCacheStore(new NullPersistenceProvider(), time);

        await store.SetAsync("a", [1], null, null);
        time.Advance(TimeSpan.FromSeconds(1));
        await store.SetAsync("b", [2], null, null);
        time.Advance(TimeSpan.FromSeconds(1));
        await store.SetAsync("c", [3], null, null);

        store.EvictLeastRecentlyUsed(2);

        Assert.Equal(2, store.Count);
        Assert.Null(await store.GetAsync("a"));
    }

    [Fact]
    public async Task EvictLeastRecentlyUsed_UnderCapacity_RemovesNothing()
    {
        var store = CreateStore(out _, out _);
        await store.SetAsync("a", [1], null, null);

        store.EvictLeastRecentlyUsed(10);

        Assert.Equal(1, store.Count);
    }

    private static InMemoryCacheStore CreateStore(out ManualTimeProvider time, out FakePersistenceProvider persistence)
    {
        time = new ManualTimeProvider(DateTimeOffset.UtcNow);
        persistence = new FakePersistenceProvider();
        return new InMemoryCacheStore(persistence, time);
    }
}
