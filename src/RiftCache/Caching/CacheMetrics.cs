using System.Diagnostics.Metrics;

namespace RiftCache.Caching;

/// <summary>
/// Cache hit/miss counters and a live entry-count gauge, published via System.Diagnostics.Metrics
/// so the OpenTelemetry SDK picks them up once MeterName is registered with .AddMeter(...).
/// </summary>
public sealed class CacheMetrics
{
    public const string MeterName = "RiftCache.Caching";

    private readonly Meter _meter;
    private readonly Counter<long> _hits;
    private readonly Counter<long> _misses;

    public CacheMetrics(InMemoryCacheStore store)
    {
        _meter = new Meter(MeterName);
        _hits = _meter.CreateCounter<long>("riftcache.cache.hits", description: "Cache reads that found a live entry.");
        _misses = _meter.CreateCounter<long>("riftcache.cache.misses", description: "Cache reads that found nothing, or an expired entry.");
        _meter.CreateObservableGauge("riftcache.cache.entries", () => store.Count, description: "Entries currently held in the in-memory store.");
    }

    /// <summary>Exposed for tests to filter a MeterListener by exact instance rather than just MeterName, since multiple CacheMetrics instances (one per test) share that name.</summary>
    internal Meter Meter => _meter;

    public void RecordHit() => _hits.Add(1);

    public void RecordMiss() => _misses.Add(1);
}
