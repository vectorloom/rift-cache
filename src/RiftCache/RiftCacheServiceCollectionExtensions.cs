using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using RiftCache.Caching;
using RiftCache.Options;
using RiftCache.Providers.Persistence;
using RiftCache.Providers.Secrets;

namespace RiftCache;

public static class RiftCacheServiceCollectionExtensions
{
    /// <summary>
    /// Registers the core service: in-memory cache store, default providers
    /// (EnvironmentSecretProvider / NullPersistenceProvider), and OpenTelemetry. A composed entry
    /// point for a specific cloud (e.g. one that wires in AzureBlobPersistenceProvider) calls this
    /// first, then registers its own providers afterward -- the DI container resolves the
    /// last-registered implementation for a given interface, so no removal step is needed.
    /// </summary>
    public static IServiceCollection AddRiftCacheCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<RiftCacheServerOptions>(configuration.GetSection(RiftCacheServerOptions.SectionName));

        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<ISecretProvider, EnvironmentSecretProvider>();
        services.AddSingleton<IPersistenceProvider, NullPersistenceProvider>();
        services.AddSingleton<InMemoryCacheStore>();
        services.AddSingleton<CacheMetrics>();
        services.AddSingleton<ICacheStore>(sp =>
            new MeteredCacheStore(sp.GetRequiredService<InMemoryCacheStore>(), sp.GetRequiredService<CacheMetrics>()));
        services.AddHostedService<CacheEvictionService>();

        // Only export via OTLP when an endpoint is actually configured -- otherwise a self-hoster
        // who set nothing gets zero telemetry noise (no attempted connections, no
        // connection-refused warnings). Read through IConfiguration, not
        // Environment.GetEnvironmentVariable, for the same reason EnvironmentSecretProvider does:
        // picks up env vars, user secrets, and config files uniformly.
        var otlpEndpoint = configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

        services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService("RiftCache"))
            .WithTracing(tracing =>
            {
                tracing.AddAspNetCoreInstrumentation();

                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    tracing.AddOtlpExporter();
                }
            })
            .WithMetrics(metrics =>
            {
                metrics.AddAspNetCoreInstrumentation();
                metrics.AddMeter(CacheMetrics.MeterName);

                if (!string.IsNullOrEmpty(otlpEndpoint))
                {
                    metrics.AddOtlpExporter();
                }
            });

        return services;
    }
}
