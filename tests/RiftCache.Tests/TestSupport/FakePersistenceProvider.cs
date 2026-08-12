using RiftCache.Caching;
using RiftCache.Providers.Persistence;

namespace RiftCache.Tests.TestSupport;

internal sealed class FakePersistenceProvider : IPersistenceProvider
{
    private readonly Dictionary<string, CacheEntry> _store = [];

    public int PersistCallCount { get; private set; }

    public int RemoveCallCount { get; private set; }

    public Task PersistAsync(string key, CacheEntry entry, CancellationToken token = default)
    {
        PersistCallCount++;
        _store[key] = entry;
        return Task.CompletedTask;
    }

    public Task<CacheEntry?> LoadAsync(string key, CancellationToken token = default) =>
        Task.FromResult(_store.TryGetValue(key, out var entry) ? entry : null);

    public Task RemoveAsync(string key, CancellationToken token = default)
    {
        RemoveCallCount++;
        _store.Remove(key);
        return Task.CompletedTask;
    }
}
