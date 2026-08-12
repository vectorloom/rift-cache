using RiftCache.Caching;
using RiftCache.Providers.Persistence;

namespace RiftCache.Tests.Providers;

public class NullPersistenceProviderTests
{
    private readonly NullPersistenceProvider _provider = new();

    [Fact]
    public async Task LoadAsync_AlwaysReturnsNull()
    {
        Assert.Null(await _provider.LoadAsync("any-key"));
    }

    [Fact]
    public async Task PersistAsync_CompletesWithoutStoringAnything()
    {
        await _provider.PersistAsync("key", new CacheEntry([1], null, null));

        Assert.Null(await _provider.LoadAsync("key"));
    }

    [Fact]
    public async Task RemoveAsync_CompletesForUnknownKey()
    {
        await _provider.RemoveAsync("key");
    }
}
