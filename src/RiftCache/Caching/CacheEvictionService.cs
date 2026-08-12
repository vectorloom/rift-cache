using Microsoft.Extensions.Options;
using RiftCache.Options;

namespace RiftCache.Caching;

internal sealed class CacheEvictionService(
    InMemoryCacheStore store,
    IOptions<RiftCacheServerOptions> options,
    TimeProvider timeProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var interval = options.Value.EvictionScanInterval;
        using var timer = new PeriodicTimer(interval, timeProvider);

        while (await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false))
        {
            store.EvictExpired(timeProvider.GetUtcNow());
            store.EvictLeastRecentlyUsed(options.Value.MaxItems);
        }
    }
}
