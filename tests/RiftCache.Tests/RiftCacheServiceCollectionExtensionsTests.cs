using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using RiftCache.Caching;
using RiftCache.Providers.Persistence;
using RiftCache.Providers.Secrets;
using RiftCache.Tests.TestSupport;

namespace RiftCache.Tests;

public class RiftCacheServiceCollectionExtensionsTests
{
    [Fact]
    public void AddRiftCacheCore_RegistersDefaultSecretProvider()
    {
        var services = new ServiceCollection();
        services.AddRiftCacheCore(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        Assert.IsType<EnvironmentSecretProvider>(provider.GetRequiredService<ISecretProvider>());
    }

    [Fact]
    public void AddRiftCacheCore_RegistersDefaultPersistenceProvider()
    {
        var services = new ServiceCollection();
        services.AddRiftCacheCore(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        Assert.IsType<NullPersistenceProvider>(provider.GetRequiredService<IPersistenceProvider>());
    }

    [Fact]
    public void AddRiftCacheCore_RegistersICacheStore_AsMeteredCacheStore()
    {
        var services = new ServiceCollection();
        services.AddRiftCacheCore(new ConfigurationBuilder().Build());

        using var provider = services.BuildServiceProvider();

        Assert.IsType<MeteredCacheStore>(provider.GetRequiredService<ICacheStore>());
    }

    /// <summary>
    /// The whole point of extracting AddRiftCacheCore(): a composed entry point for a specific
    /// cloud provider registers this first, then its own provider afterward, relying on the DI
    /// container resolving the last-registered implementation for a given interface -- no removal
    /// step needed. This proves that actually holds.
    /// </summary>
    [Fact]
    public void AddRiftCacheCore_LaterRegistration_OverridesDefaultPersistenceProvider()
    {
        var services = new ServiceCollection();
        services.AddRiftCacheCore(new ConfigurationBuilder().Build());
        services.AddSingleton<IPersistenceProvider, FakePersistenceProvider>();

        using var provider = services.BuildServiceProvider();

        Assert.IsType<FakePersistenceProvider>(provider.GetRequiredService<IPersistenceProvider>());
    }
}
