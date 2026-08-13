using System.Diagnostics.Metrics;
using RiftCache.Caching;
using RiftCache.Providers.Persistence;
using RiftCache.Tests.TestSupport;

namespace RiftCache.Tests.Caching;

public class MeteredCacheStoreTests
{
    [Fact]
    public async Task GetAsync_ValueFound_ReturnsItAndRecordsHit()
    {
        var inner = new FakeCacheStore { NextGetResult = [1, 2, 3] };
        var metrics = new CacheMetrics(new InMemoryCacheStore(new FakePersistenceProvider(), TimeProvider.System));
        var store = new MeteredCacheStore(inner, metrics);

        using var listener = new MeterListener();
        var hits = new List<long>();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter) && instrument.Name == "riftcache.cache.hits")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => hits.Add(measurement));
        listener.Start();

        var result = await store.GetAsync("key");

        Assert.Equal(new byte[] { 1, 2, 3 }, result);
        Assert.Equal([1], hits);
    }

    [Fact]
    public async Task GetAsync_ValueMissing_ReturnsNullAndRecordsMiss()
    {
        var inner = new FakeCacheStore { NextGetResult = null };
        var metrics = new CacheMetrics(new InMemoryCacheStore(new FakePersistenceProvider(), TimeProvider.System));
        var store = new MeteredCacheStore(inner, metrics);

        using var listener = new MeterListener();
        var misses = new List<long>();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter) && instrument.Name == "riftcache.cache.misses")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => misses.Add(measurement));
        listener.Start();

        var result = await store.GetAsync("key");

        Assert.Null(result);
        Assert.Equal([1], misses);
    }

    [Fact]
    public async Task SetAsync_DelegatesToInner()
    {
        var store = CreateStore(out var inner);

        await store.SetAsync("key", [1], null, null);

        Assert.Equal(1, inner.SetCallCount);
    }

    [Fact]
    public async Task RefreshAsync_DelegatesToInnerAndReturnsItsResult()
    {
        var store = CreateStore(out var inner);
        inner.NextRefreshResult = false;

        Assert.False(await store.RefreshAsync("key"));
        Assert.Equal(1, inner.RefreshCallCount);
    }

    [Fact]
    public async Task RemoveAsync_DelegatesToInnerAndReturnsItsResult()
    {
        var store = CreateStore(out var inner);
        inner.NextRemoveResult = false;

        Assert.False(await store.RemoveAsync("key"));
        Assert.Equal(1, inner.RemoveCallCount);
    }

    private static MeteredCacheStore CreateStore(out FakeCacheStore inner)
    {
        inner = new FakeCacheStore();
        var metrics = new CacheMetrics(new InMemoryCacheStore(new FakePersistenceProvider(), TimeProvider.System));
        return new MeteredCacheStore(inner, metrics);
    }
}
