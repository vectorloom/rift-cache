using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace RiftCache.Client;

public static class RiftCacheServiceCollectionExtensions
{
    /// <summary>Registers RiftCacheClient as the app's IDistributedCache, backed by an HttpClient pointed at RiftCacheOptions.ServiceUrl.</summary>
    public static IServiceCollection AddRiftCache(this IServiceCollection services, Action<RiftCacheOptions> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(configureOptions);

        services.AddOptions<RiftCacheOptions>()
            .Configure(configureOptions)
            .Validate(o => !string.IsNullOrWhiteSpace(o.ServiceUrl), "RiftCacheOptions.ServiceUrl is required.")
            .Validate(o => !string.IsNullOrWhiteSpace(o.ApiKey), "RiftCacheOptions.ApiKey is required.")
            .ValidateOnStart();

        services.AddHttpClient<RiftCacheClient>((sp, http) =>
        {
            var options = sp.GetRequiredService<IOptions<RiftCacheOptions>>().Value;
            http.BaseAddress = new Uri(options.ServiceUrl.TrimEnd('/') + "/", UriKind.Absolute);
            http.DefaultRequestHeaders.Add(RiftCacheClient.ApiKeyHeaderName, options.ApiKey);
        })
            // Every RiftCacheClient call is idempotent -- GetAsync/RemoveAsync are inherently so,
            // SetAsync unconditionally overwrites, and RefreshAsync (POST /refresh) re-refreshing
            // the same sliding window has the same effect as once -- so retrying any of them on a
            // transient failure is safe with the standard handler's default (verb-agnostic) policy.
            .AddStandardResilienceHandler();

        services.AddTransient<IDistributedCache>(sp => sp.GetRequiredService<RiftCacheClient>());

        return services;
    }
}
