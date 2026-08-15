using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.DependencyInjection;
using RiftCache.Providers.Secrets;

namespace RiftCache.Providers.Azure;

public static class AzureProviderServiceCollectionExtensions
{
    /// <summary>Registers AzureKeyVaultSecretProvider as ISecretProvider, backed by a SecretClient for vaultUri.</summary>
    public static IServiceCollection AddAzureKeyVaultSecretProvider(this IServiceCollection services, Uri vaultUri, TokenCredential? credential = null)
    {
        ArgumentNullException.ThrowIfNull(vaultUri);

        services.AddSingleton(new SecretClient(vaultUri, credential ?? new DefaultAzureCredential()));
        services.AddSingleton<ISecretProvider, AzureKeyVaultSecretProvider>();

        return services;
    }
}
