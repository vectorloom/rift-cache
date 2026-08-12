using System.Collections.Concurrent;
using RiftCache.Providers.Persistence;

namespace RiftCache.Caching;

/// <summary>
/// ConcurrentDictionary-backed cache with TTL (absolute + sliding) and, when configured with a
/// bounded capacity, approximate LRU eviction. Reads to a missing/expired key fall through to
/// the configured IPersistenceProvider (a no-op for the default NullPersistenceProvider).
/// </summary>
public sealed class InMemoryCacheStore : ICacheStore
{
    private readonly ConcurrentDictionary<string, CacheEntry> _entries = new();
    private readonly IPersistenceProvider _persistence;
    private readonly TimeProvider _timeProvider;

    public InMemoryCacheStore(IPersistenceProvider persistence, TimeProvider timeProvider)
    {
        _persistence = persistence;
        _timeProvider = timeProvider;
    }

    public int Count => _entries.Count;

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        var now = _timeProvider.GetUtcNow();

        if (_entries.TryGetValue(key, out var entry))
        {
            if (entry.IsExpired(now))
            {
                _entries.TryRemove(key, out _);
                await _persistence.RemoveAsync(key, token).ConfigureAwait(false);
                return null;
            }

            entry.Touch(now);
            return entry.Value;
        }

        var loaded = await _persistence.LoadAsync(key, token).ConfigureAwait(false);
        if (loaded is null || loaded.IsExpired(now))
        {
            return null;
        }

        loaded.Touch(now);
        _entries[key] = loaded;
        return loaded.Value;
    }

    public async Task SetAsync(string key, byte[] value, DateTimeOffset? absoluteExpiration, TimeSpan? slidingExpiration, CancellationToken token = default)
    {
        var entry = new CacheEntry(value, absoluteExpiration, slidingExpiration, _timeProvider.GetUtcNow());
        _entries[key] = entry;
        await _persistence.PersistAsync(key, entry, token).ConfigureAwait(false);
    }

    public async Task<bool> RefreshAsync(string key, CancellationToken token = default)
    {
        var now = _timeProvider.GetUtcNow();

        if (!_entries.TryGetValue(key, out var entry))
        {
            return false;
        }

        if (entry.IsExpired(now))
        {
            _entries.TryRemove(key, out _);
            await _persistence.RemoveAsync(key, token).ConfigureAwait(false);
            return false;
        }

        entry.Touch(now);
        return true;
    }

    public async Task<bool> RemoveAsync(string key, CancellationToken token = default)
    {
        var removed = _entries.TryRemove(key, out _);
        await _persistence.RemoveAsync(key, token).ConfigureAwait(false);
        return removed;
    }

    internal void EvictExpired(DateTimeOffset now)
    {
        foreach (var (key, entry) in _entries)
        {
            if (entry.IsExpired(now))
            {
                _entries.TryRemove(key, out _);
            }
        }
    }

    internal void EvictLeastRecentlyUsed(int maxItems)
    {
        if (maxItems <= 0)
        {
            return;
        }

        var overflow = _entries.Count - maxItems;
        if (overflow <= 0)
        {
            return;
        }

        var staleKeys = _entries
            .OrderBy(kvp => kvp.Value.LastAccessed)
            .Take(overflow)
            .Select(kvp => kvp.Key)
            .ToArray();

        foreach (var key in staleKeys)
        {
            _entries.TryRemove(key, out _);
        }
    }
}
