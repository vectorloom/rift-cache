using System.Net;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RiftCache.Client.Tests.TestSupport;

namespace RiftCache.Client.Tests;

public class RiftCacheServiceCollectionExtensionsTests
{
    [Fact]
    public void AddRiftCache_RegistersIDistributedCache_ResolvingToRiftCacheClient()
    {
        var services = new ServiceCollection();
        services.AddRiftCache(options =>
        {
            options.ServiceUrl = "http://localhost:8080";
            options.ApiKey = "dev-key";
        });

        using var provider = services.BuildServiceProvider();

        Assert.IsType<RiftCacheClient>(provider.GetRequiredService<IDistributedCache>());
    }

    [Fact]
    public void AddRiftCache_MissingServiceUrl_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        services.AddRiftCache(options => options.ApiKey = "dev-key");

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<RiftCacheOptions>>().Value);
    }

    [Fact]
    public void AddRiftCache_MissingApiKey_FailsValidationOnAccess()
    {
        var services = new ServiceCollection();
        services.AddRiftCache(options => options.ServiceUrl = "http://localhost:8080");

        using var provider = services.BuildServiceProvider();

        Assert.Throws<OptionsValidationException>(() => provider.GetRequiredService<IOptions<RiftCacheOptions>>().Value);
    }

    [Fact]
    public async Task AddRiftCache_RetriesTransientFailures_ThroughTheResiliencePipeline()
    {
        var services = new ServiceCollection();
        services.AddRiftCache(options =>
        {
            options.ServiceUrl = "http://localhost:8080";
            options.ApiKey = "dev-key";
        });

        var callCount = 0;
        services.AddHttpClient<RiftCacheClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new FakeHttpMessageHandler(_ =>
            {
                callCount++;
                return callCount == 1
                    ? new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
                    : new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([9]) };
            }));

        using var provider = services.BuildServiceProvider();
        var cache = provider.GetRequiredService<IDistributedCache>();

        var result = await cache.GetAsync("key");

        Assert.Equal(new byte[] { 9 }, result);
        Assert.True(callCount > 1, $"Expected the resilience handler to retry after the first 503, but the handler was only invoked {callCount} time(s).");
    }
}
