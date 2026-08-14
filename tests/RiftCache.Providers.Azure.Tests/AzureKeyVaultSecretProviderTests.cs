using Azure;
using Azure.Security.KeyVault.Secrets;
using RiftCache.Providers.Azure.Tests.TestSupport;

namespace RiftCache.Providers.Azure.Tests;

public class AzureKeyVaultSecretProviderTests
{
    [Fact]
    public async Task GetSecretAsync_ExistingSecret_ReturnsValue()
    {
        var client = new FakeSecretClient(name => new KeyVaultSecret(name, "dev-key"));
        var provider = new AzureKeyVaultSecretProvider(client);

        Assert.Equal("dev-key", await provider.GetSecretAsync("RIFTCACHE_API_KEY"));
    }

    [Fact]
    public async Task GetSecretAsync_MissingSecret_ReturnsNull()
    {
        var client = new FakeSecretClient(_ => null);
        var provider = new AzureKeyVaultSecretProvider(client);

        Assert.Null(await provider.GetSecretAsync("RIFTCACHE_API_KEY"));
    }

    [Fact]
    public async Task GetSecretAsync_OtherFailure_Propagates()
    {
        var client = new FakeSecretClient(_ => throw new RequestFailedException(403, "Forbidden"));
        var provider = new AzureKeyVaultSecretProvider(client);

        var ex = await Assert.ThrowsAsync<RequestFailedException>(() => provider.GetSecretAsync("RIFTCACHE_API_KEY"));
        Assert.Equal(403, ex.Status);
    }

    [Fact]
    public async Task GetSecretAsync_TranslatesUnderscoresToHyphens()
    {
        var client = new FakeSecretClient(name => new KeyVaultSecret(name, "value"));
        var provider = new AzureKeyVaultSecretProvider(client);

        await provider.GetSecretAsync("RIFTCACHE_API_KEY");

        Assert.Equal("RIFTCACHE-API-KEY", client.LastRequestedName);
    }

    [Fact]
    public async Task GetSecretAsync_TranslatesDoubleUnderscoreTenantNesting()
    {
        var client = new FakeSecretClient(name => new KeyVaultSecret(name, "value"));
        var provider = new AzureKeyVaultSecretProvider(client);

        await provider.GetSecretAsync("RIFTCACHE_API_KEY__TEAM-A");

        Assert.Equal("RIFTCACHE-API-KEY--TEAM-A", client.LastRequestedName);
    }
}
