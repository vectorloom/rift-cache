using System.Globalization;
using RiftCache.Auth;
using RiftCache.Caching;
using RiftCache.Options;

namespace RiftCache.Endpoints;

public static class CacheEndpoints
{
    public const string AbsoluteExpirationHeader = "X-RiftCache-Absolute-Expiration";
    public const string AbsoluteExpirationRelativeSecondsHeader = "X-RiftCache-Absolute-Expiration-Relative-Seconds";
    public const string SlidingExpirationSecondsHeader = "X-RiftCache-Sliding-Expiration-Seconds";

    public static IEndpointRouteBuilder MapCacheEndpoints(this IEndpointRouteBuilder app, RiftCacheServerOptions serverOptions)
    {
        var pattern = serverOptions.MultiTenant
            ? "/api/v1/cache/{tenantId}/{key}"
            : "/api/v1/cache/{key}";

        var group = app.MapGroup(pattern).AddEndpointFilter<ApiKeyAuthFilter>();

        group.MapGet("", GetAsync);
        group.MapPut("", SetAsync);
        group.MapPost("/refresh", RefreshAsync);
        group.MapDelete("", RemoveAsync);

        return app;
    }

    private static async Task<IResult> GetAsync(HttpContext httpContext, ICacheStore store, CancellationToken token)
    {
        if (ResolveCompositeKey(httpContext) is not { } key)
        {
            return Results.BadRequest("key is required.");
        }

        var value = await store.GetAsync(key, token).ConfigureAwait(false);
        return value is null ? Results.NotFound() : Results.Bytes(value, "application/octet-stream");
    }

    private static async Task<IResult> SetAsync(HttpContext httpContext, ICacheStore store, CancellationToken token)
    {
        if (ResolveCompositeKey(httpContext) is not { } key)
        {
            return Results.BadRequest("key is required.");
        }

        using var buffer = new MemoryStream();
        await httpContext.Request.Body.CopyToAsync(buffer, token).ConfigureAwait(false);

        var (absoluteExpiration, slidingExpiration) = ParseExpiration(httpContext.Request.Headers);
        await store.SetAsync(key, buffer.ToArray(), absoluteExpiration, slidingExpiration, token).ConfigureAwait(false);
        return Results.NoContent();
    }

    private static async Task<IResult> RefreshAsync(HttpContext httpContext, ICacheStore store, CancellationToken token)
    {
        if (ResolveCompositeKey(httpContext) is not { } key)
        {
            return Results.BadRequest("key is required.");
        }

        var refreshed = await store.RefreshAsync(key, token).ConfigureAwait(false);
        return refreshed ? Results.NoContent() : Results.NotFound();
    }

    private static async Task<IResult> RemoveAsync(HttpContext httpContext, ICacheStore store, CancellationToken token)
    {
        if (ResolveCompositeKey(httpContext) is not { } key)
        {
            return Results.BadRequest("key is required.");
        }

        var removed = await store.RemoveAsync(key, token).ConfigureAwait(false);
        return removed ? Results.NoContent() : Results.NotFound();
    }

    /// <summary>Composes the route's {key} (and, in multi-tenant mode, {tenantId}) into the store's flat key namespace.</summary>
    private static string? ResolveCompositeKey(HttpContext httpContext)
    {
        var routeValues = httpContext.Request.RouteValues;

        if (routeValues.TryGetValue("key", out var keyValue) is false ||
            keyValue is not string { Length: > 0 } key)
        {
            return null;
        }

        if (routeValues.TryGetValue("tenantId", out var tenantIdValue) &&
            tenantIdValue is string { Length: > 0 } tenantId)
        {
            return $"{tenantId}:{key}";
        }

        return key;
    }

    private static (DateTimeOffset? Absolute, TimeSpan? Sliding) ParseExpiration(IHeaderDictionary headers)
    {
        DateTimeOffset? absolute = null;

        if (headers.TryGetValue(AbsoluteExpirationHeader, out var absoluteHeader) &&
            DateTimeOffset.TryParse(absoluteHeader, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedAbsolute))
        {
            absolute = parsedAbsolute;
        }
        else if (headers.TryGetValue(AbsoluteExpirationRelativeSecondsHeader, out var relativeHeader) &&
            double.TryParse(relativeHeader, NumberStyles.Float, CultureInfo.InvariantCulture, out var relativeSeconds))
        {
            absolute = DateTimeOffset.UtcNow.AddSeconds(relativeSeconds);
        }

        TimeSpan? sliding = null;

        if (headers.TryGetValue(SlidingExpirationSecondsHeader, out var slidingHeader) &&
            double.TryParse(slidingHeader, NumberStyles.Float, CultureInfo.InvariantCulture, out var slidingSeconds))
        {
            sliding = TimeSpan.FromSeconds(slidingSeconds);
        }

        return (absolute, sliding);
    }
}
