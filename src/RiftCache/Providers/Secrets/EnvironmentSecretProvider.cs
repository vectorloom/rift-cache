using Microsoft.Extensions.Configuration;

namespace RiftCache.Providers.Secrets;

/// <summary>
/// Default secret provider: zero cloud dependency, what self-hosters use. Reads through
/// IConfiguration, so it picks up whatever sources the host has configured — environment
/// variables always, plus user secrets in Development and appsettings.json if you choose to
/// layer those in too. Falls back to a mounted file referenced by a "{KEY}_FILE" entry (the
/// Docker/Kubernetes secrets-mount convention) when the key itself isn't set.
///
/// Keys use "__" for tenant nesting (RIFTCACHE_API_KEY__{TENANT}) to mirror the
/// EnvironmentVariablesConfigurationProvider's own "__" → ":" convention, so the same key
/// resolves whether it arrives as a real env var or a nested appsettings/user-secrets entry.
/// </summary>
public sealed class EnvironmentSecretProvider(IConfiguration configuration) : ISecretProvider
{
    public async Task<string?> GetSecretAsync(string key, CancellationToken token = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var value = configuration[ToConfigurationKey(key)];
        if (!string.IsNullOrEmpty(value))
        {
            return value;
        }

        var filePath = configuration[ToConfigurationKey($"{key}_FILE")];
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
        {
            return null;
        }

        var fileContents = await File.ReadAllTextAsync(filePath, token).ConfigureAwait(false);
        return fileContents.Trim();
    }

    private static string ToConfigurationKey(string key) => key.Replace("__", ConfigurationPath.KeyDelimiter);
}
