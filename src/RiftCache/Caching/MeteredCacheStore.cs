namespace RiftCache.Caching;

/// <summary>
/// ICacheStore decorator that records hit/miss metrics around GetAsync. Kept separate from
/// InMemoryCacheStore rather than injecting CacheMetrics directly into it, since CacheMetrics
/// itself depends on InMemoryCacheStore (for the entry-count gauge) -- injecting it the other way
/// too would create a circular dependency.
/// </summary>
internal sealed class MeteredCacheStore(ICacheStore inner, CacheMetrics metrics) : ICacheStore
{
    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        var value = await inner.GetAsync(key, token).ConfigureAwait(false);

        if (value is null)
        {
            metrics.RecordMiss();
        }
        else
        {
            metrics.RecordHit();
        }

        return value;
    }

    public Task SetAsync(string key, byte[] value, DateTimeOffset? absoluteExpiration, TimeSpan? slidingExpiration, CancellationToken token = default) =>
        inner.SetAsync(key, value, absoluteExpiration, slidingExpiration, token);

    public Task<bool> RefreshAsync(string key, CancellationToken token = default) =>
        inner.RefreshAsync(key, token);

    public Task<bool> RemoveAsync(string key, CancellationToken token = default) =>
        inner.RemoveAsync(key, token);
}
