using RiftCache.Caching;

namespace RiftCache.Providers.Persistence;

/// <summary>
/// Default persistence provider: in-memory only, no durability. Fine for local dev and
/// low-stakes caching. Self-hosters who need entries to survive a restart should configure
/// a real IPersistenceProvider (e.g. Azure Blob, S3, GCS) instead.
/// </summary>
public sealed class NullPersistenceProvider : IPersistenceProvider
{
    public Task PersistAsync(string key, CacheEntry entry, CancellationToken token = default) => Task.CompletedTask;

    public Task<CacheEntry?> LoadAsync(string key, CancellationToken token = default) => Task.FromResult<CacheEntry?>(null);

    public Task RemoveAsync(string key, CancellationToken token = default) => Task.CompletedTask;
}
