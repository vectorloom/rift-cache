using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

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
}
