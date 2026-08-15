using Azure.Storage.Blobs;
using RiftCache.Caching;

namespace RiftCache.Providers.Azure.Tests;

/// <summary>
/// Runs against a real Azurite instance (the official local Azure Storage emulator), not the hand
/// faked BlobContainerClient the unit tests use -- real round-trip coverage AzureKeyVaultSecretProvider
/// couldn't get, since Key Vault has no equivalent local emulator. Requires Azurite listening on
/// localhost:10000 (the CI workflow runs it as a service container; locally:
/// `docker run -p 10000:10000 mcr.microsoft.com/azure-storage/azurite azurite-blob --blobHost 0.0.0.0`,
/// or the Podman equivalent). No skip-if-unavailable logic -- xUnit v2 doesn't support that
/// cleanly, and a clear connection-refused failure without Azurite running is more honest than a
/// silent skip.
///
/// BlobClientOptions below pins an explicit service version rather than floating on the SDK's
/// default (newest) one -- Azurite rejects requests using an API version newer than it knows
/// about ("--skipApiVersionCheck" works around it locally, but GitHub Actions' `services:` block
/// has no way to pass that flag to the container, so it must be fixed on the client side instead).
/// </summary>
public class AzureBlobPersistenceProviderIntegrationTests : IAsyncLifetime
{
    // Azurite's well-known default development account -- fixed and publicly documented by
    // design, not a real secret.
    private const string AzuriteConnectionString =
        "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:10000/devstoreaccount1;";

    private readonly string _containerName = $"riftcache-test-{Guid.NewGuid():N}";
    private BlobContainerClient _container = null!;
    private AzureBlobPersistenceProvider _provider = null!;

    public async Task InitializeAsync()
    {
        var options = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_08_04);
        _container = new BlobContainerClient(AzuriteConnectionString, _containerName, options);
        await _container.CreateIfNotExistsAsync();
        _provider = new AzureBlobPersistenceProvider(_container);
    }

    public async Task DisposeAsync() => await _container.DeleteIfExistsAsync();

    [Fact]
    public async Task PersistThenLoad_RoundTripsAgainstRealAzurite()
    {
        var absolute = DateTimeOffset.UtcNow.AddMinutes(30);
        var sliding = TimeSpan.FromSeconds(45);

        await _provider.PersistAsync("key", new CacheEntry([1, 2, 3], absolute, sliding));
        var loaded = await _provider.LoadAsync("key");

        Assert.NotNull(loaded);
        Assert.Equal(new byte[] { 1, 2, 3 }, loaded.Value);
        Assert.Equal(absolute, loaded.AbsoluteExpiration);
        Assert.Equal(sliding, loaded.SlidingExpiration);
    }

    [Fact]
    public async Task RemoveAsync_ActuallyDeletesFromAzurite()
    {
        await _provider.PersistAsync("key", new CacheEntry([1], null, null));

        await _provider.RemoveAsync("key");

        Assert.Null(await _provider.LoadAsync("key"));
    }

    [Fact]
    public async Task LoadAsync_TrulyNonexistentKey_ReturnsNull()
    {
        Assert.Null(await _provider.LoadAsync("never-set"));
    }
}
