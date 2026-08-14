using RiftCache.Caching;

namespace RiftCache.Tests.TestSupport;

internal sealed class FakeCacheStore : ICacheStore
{
    public byte[]? NextGetResult { get; set; }

    public bool NextRefreshResult { get; set; } = true;

    public bool NextRemoveResult { get; set; } = true;

    public int SetCallCount { get; private set; }

    public int RefreshCallCount { get; private set; }

    public int RemoveCallCount { get; private set; }

    public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
        Task.FromResult(NextGetResult);

    public Task SetAsync(string key, byte[] value, DateTimeOffset? absoluteExpiration, TimeSpan? slidingExpiration, CancellationToken token = default)
    {
        SetCallCount++;
        return Task.CompletedTask;
    }

    public Task<bool> RefreshAsync(string key, CancellationToken token = default)
    {
        RefreshCallCount++;
        return Task.FromResult(NextRefreshResult);
    }

    public Task<bool> RemoveAsync(string key, CancellationToken token = default)
    {
        RemoveCallCount++;
        return Task.FromResult(NextRemoveResult);
    }
}
