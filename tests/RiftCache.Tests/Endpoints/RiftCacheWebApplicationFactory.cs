using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RiftCache.Providers.Secrets;
using RiftCache.Tests.TestSupport;

namespace RiftCache.Tests.Endpoints;

internal sealed class RiftCacheWebApplicationFactory(IReadOnlyDictionary<string, string> secrets, bool multiTenant = false)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        if (multiTenant)
        {
            builder.ConfigureAppConfiguration((_, config) =>
                config.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RiftCache:MultiTenant"] = "true",
                }));
        }

        builder.ConfigureServices(services =>
        {
            services.RemoveAll<ISecretProvider>();
            services.AddSingleton<ISecretProvider>(new StubSecretProvider(secrets));
        });
    }
}
