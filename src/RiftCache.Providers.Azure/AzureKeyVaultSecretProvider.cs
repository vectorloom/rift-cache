using Azure;
using Azure.Security.KeyVault.Secrets;
using RiftCache.Providers.Secrets;

namespace RiftCache.Providers.Azure;

/// <summary>
/// ISecretProvider backed by Azure Key Vault. Register via AddAzureKeyVaultSecretProvider(...)
/// rather than constructing directly, so the SecretClient is configured consistently.
///
/// Key Vault secret names only allow [0-9a-zA-Z-] -- no underscores -- but every key this project
/// looks up follows EnvironmentSecretProvider's underscore convention (RIFTCACHE_API_KEY, and
/// RIFTCACHE_API_KEY__{TENANT} for multi-tenant, per ApiKeyAuthFilter). GetSecretAsync translates
/// "_" to "-" and, to keep the tenant-nesting marker visually distinct, "__" to "--" -- so
/// RIFTCACHE_API_KEY__TEAM-A is looked up as RIFTCACHE-API-KEY--TEAM-A.
/// </summary>
public sealed class AzureKeyVaultSecretProvider(SecretClient client) : ISecretProvider
{
    public async Task<string?> GetSecretAsync(string key, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        try
        {
            var response = await client.GetSecretAsync(ToKeyVaultSecretName(key), cancellationToken: token).ConfigureAwait(false);
            return response.Value.Value;
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    internal static string ToKeyVaultSecretName(string key) => key.Replace("__", "--").Replace('_', '-');
}
