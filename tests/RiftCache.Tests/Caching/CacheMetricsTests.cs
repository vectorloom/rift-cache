using System.Diagnostics.Metrics;
using RiftCache.Caching;
using RiftCache.Providers.Persistence;
using RiftCache.Tests.TestSupport;

namespace RiftCache.Tests.Caching;

public class CacheMetricsTests
{
    [Fact]
    public void RecordHit_PublishesToHitsCounter()
    {
        var metrics = new CacheMetrics(new InMemoryCacheStore(new FakePersistenceProvider(), TimeProvider.System));

        // Filters by exact Meter instance, not just CacheMetrics.MeterName -- multiple tests
        // create their own CacheMetrics (and therefore their own Meter with the same name), and
        // xUnit can run different test classes in parallel, so name-only filtering would leak
        // measurements across tests.
        using var listener = new MeterListener();
        var measurements = new List<long>();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter) && instrument.Name == "riftcache.cache.hits")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => measurements.Add(measurement));
        listener.Start();

        metrics.RecordHit();
        metrics.RecordHit();

        Assert.Equal([1, 1], measurements);
    }

    [Fact]
    public void RecordMiss_PublishesToMissesCounter()
    {
        var metrics = new CacheMetrics(new InMemoryCacheStore(new FakePersistenceProvider(), TimeProvider.System));

        using var listener = new MeterListener();
        var measurements = new List<long>();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter) && instrument.Name == "riftcache.cache.misses")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, measurement, _, _) => measurements.Add(measurement));
        listener.Start();

        metrics.RecordMiss();

        Assert.Equal([1], measurements);
    }

    [Fact]
    public async Task EntriesGauge_ReportsLiveStoreCount()
    {
        var store = new InMemoryCacheStore(new FakePersistenceProvider(), TimeProvider.System);
        await store.SetAsync("a", [1], null, null);
        await store.SetAsync("b", [2], null, null);
        var metrics = new CacheMetrics(store);

        using var listener = new MeterListener();
        var measurements = new List<int>();
        listener.InstrumentPublished = (instrument, l) =>
        {
            if (ReferenceEquals(instrument.Meter, metrics.Meter) && instrument.Name == "riftcache.cache.entries")
            {
                l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<int>((_, measurement, _, _) => measurements.Add(measurement));
        listener.Start();
        listener.RecordObservableInstruments();

        Assert.Equal([2], measurements);
    }
}
