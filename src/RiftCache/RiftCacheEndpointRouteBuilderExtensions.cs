using RiftCache.Endpoints;
using RiftCache.Options;

namespace RiftCache;

public static class RiftCacheEndpointRouteBuilderExtensions
{
    /// <summary>Maps the root service banner, /healthz, and the cache endpoints (via CacheEndpoints.MapCacheEndpoints).</summary>
    public static IEndpointRouteBuilder MapRiftCacheCore(this IEndpointRouteBuilder endpoints, RiftCacheServerOptions serverOptions)
    {
        endpoints.MapGet("/", () => Results.Ok(new
        {
            service = "RiftCache",
            // Anchored to a type known to live in this assembly rather than the composed entry
            // point's own Program class, so this reports the core library's version regardless of
            // which entry point (this one, or a future cloud-specific composed one) hosts it.
            version = typeof(RiftCacheServerOptions).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
            mode = serverOptions.MultiTenant ? "multi-tenant" : "single-tenant",
            endpoints = new
            {
                health = "/healthz",
                cache = serverOptions.MultiTenant ? "/api/v1/cache/{tenantId}/{key}" : "/api/v1/cache/{key}",
            },
        }));

        endpoints.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }));

        endpoints.MapCacheEndpoints(serverOptions);

        return endpoints;
    }
}
