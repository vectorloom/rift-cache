namespace RiftCache.Caching;

public interface ICacheStore
{
    Task<byte[]?> GetAsync(string key, CancellationToken token = default);

    Task SetAsync(string key, byte[] value, DateTimeOffset? absoluteExpiration, TimeSpan? slidingExpiration, CancellationToken token = default);

    /// <summary>Resets the sliding-expiration clock for an entry without returning its value. Returns false if the key doesn't exist or has expired.</summary>
    Task<bool> RefreshAsync(string key, CancellationToken token = default);

    Task<bool> RemoveAsync(string key, CancellationToken token = default);
}
