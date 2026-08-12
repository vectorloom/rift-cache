using RiftCache.Caching;

namespace RiftCache.Providers.Persistence;

/// <summary>
/// Write-behind / read-through backing store for cache entries. The in-memory store is always
/// the source of truth for a running instance; a persistence provider exists so entries can
/// survive a restart or be shared across replicas. See ARCHITECTURE_NOTES.md section 2.2.
/// </summary>
public interface IPersistenceProvider
{
    Task PersistAsync(string key, CacheEntry entry, CancellationToken token = default);

    Task<CacheEntry?> LoadAsync(string key, CancellationToken token = default);

    Task RemoveAsync(string key, CancellationToken token = default);
}
