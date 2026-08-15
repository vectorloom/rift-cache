extern alias RiftCacheAzure;
extern alias RiftCacheCore;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RiftCache.Providers.Azure;
using IPersistenceProvider = RiftCacheCore::RiftCache.Providers.Persistence.IPersistenceProvider;
using Program = RiftCacheAzure::Program;

namespace RiftCache.Azure.Tests;

/// <summary>
/// Proves the composed entry point's DI wiring, not AzureBlobPersistenceProvider's own behavior
/// (already covered by AzureBlobPersistenceProviderIntegrationTests against real Azurite) or
/// AddRiftCacheCore's override behavior (already covered in RiftCache.Tests). Sets
/// RIFTCACHE_BLOB_CONTAINER_URL as a real process environment variable -- not via
/// WebApplicationFactory's ConfigureAppConfiguration -- because Program.cs reads it synchronously
/// between WebApplication.CreateBuilder(args) and builder.Build(), and WebApplicationBuilder picks
/// up real environment variables at CreateBuilder(args) time, before any test-host config
/// injection would be layered in. Tests run sequentially within this class (xUnit default), and
/// each clears the variable in a finally block, since it's process-wide state.
/// </summary>
public class RiftCacheAzureWiringTests
{
    private const string BlobContainerUrlVariable = "RIFTCACHE_BLOB_CONTAINER_URL";

    [Fact]
    public async Task ResolvesAzureBlobPersistenceProvider_WhenBlobContainerUrlConfigured()
    {
        Environment.SetEnvironmentVariable(BlobContainerUrlVariable, "https://example.blob.core.windows.net/riftcache-entries");
        try
        {
            await using var factory = new WebApplicationFactory<Program>();
            using var scope = factory.Services.CreateScope();

            var persistenceProvider = scope.ServiceProvider.GetRequiredService<IPersistenceProvider>();

            Assert.IsType<AzureBlobPersistenceProvider>(persistenceProvider);
        }
        finally
        {
            Environment.SetEnvironmentVariable(BlobContainerUrlVariable, null);
        }
    }

    [Fact]
    public void Startup_ThrowsInvalidOperationException_WhenBlobContainerUrlMissing()
    {
        Environment.SetEnvironmentVariable(BlobContainerUrlVariable, null);

        var exception = Record.Exception(() =>
        {
            using var factory = new WebApplicationFactory<Program>();
            factory.Services.GetService(typeof(IPersistenceProvider));
        });

        Assert.NotNull(exception);
        Assert.Contains(BlobContainerUrlVariable, exception.ToString());
    }
}
