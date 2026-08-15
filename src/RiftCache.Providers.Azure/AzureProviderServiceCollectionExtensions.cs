using Azure.Core;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Azure.Storage.Blobs;
using Microsoft.Extensions.DependencyInjection;
using RiftCache.Providers.Persistence;
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

    /// <summary>Registers AzureBlobPersistenceProvider as IPersistenceProvider, backed by a BlobContainerClient for containerUri. The container must already exist.</summary>
    public static IServiceCollection AddAzureBlobPersistenceProvider(this IServiceCollection services, Uri containerUri, TokenCredential? credential = null)
    {
        ArgumentNullException.ThrowIfNull(containerUri);

        // Pinned rather than left on the SDK's default (newest) service version: real Azure
        // Storage supports old API versions for years, so there's no functional downside, and it
        // means AzureBlobPersistenceProvider works against a local Azurite instance out of the box
        // -- Azurite rejects requests using an API version newer than it knows about, which the
        // SDK's floating default triggers as it releases new versions.
        var options = new BlobClientOptions(BlobClientOptions.ServiceVersion.V2024_08_04);
        services.AddSingleton(new BlobContainerClient(containerUri, credential ?? new DefaultAzureCredential(), options));
        services.AddSingleton<IPersistenceProvider, AzureBlobPersistenceProvider>();

        return services;
    }
}
