using Microsoft.Extensions.DependencyInjection;
using RiftCache.Providers.Secrets;

namespace RiftCache.Providers.Azure.Tests;

public class AzureProviderServiceCollectionExtensionsTests
{
    [Fact]
    public void AddAzureKeyVaultSecretProvider_RegistersISecretProvider_ResolvingToAzureKeyVaultSecretProvider()
    {
        var services = new ServiceCollection();
        services.AddAzureKeyVaultSecretProvider(new Uri("https://example.vault.azure.net/"));

        using var provider = services.BuildServiceProvider();

        Assert.IsType<AzureKeyVaultSecretProvider>(provider.GetRequiredService<ISecretProvider>());
    }
}
