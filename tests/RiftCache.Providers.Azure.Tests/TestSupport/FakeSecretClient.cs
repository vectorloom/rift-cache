using Azure;
using Azure.Security.KeyVault.Secrets;

namespace RiftCache.Providers.Azure.Tests.TestSupport;

/// <summary>
/// Subclasses SecretClient rather than using a mocking framework -- GetSecretAsync is virtual and
/// SecretClient exposes a protected parameterless constructor specifically for this, matching how
/// the rest of this codebase hand-writes fakes instead of pulling in Moq/NSubstitute.
/// </summary>
internal sealed class FakeSecretClient(Func<string, KeyVaultSecret?> lookup) : SecretClient
{
    public string? LastRequestedName { get; private set; }

    // SecretClient.GetSecretAsync(name, version, cancellationToken) is a virtual 3-parameter
    // overload, but calling it with only name + cancellationToken (as AzureKeyVaultSecretProvider
    // does) actually dispatches to this 4-parameter overload at runtime -- confirmed by reflecting
    // over the installed 4.11.0 assembly after the 3-parameter override alone threw a
    // NullReferenceException from inside the real base implementation. Override both so neither
    // call shape can slip through to the real (network-backed) implementation.
    public override Task<Response<KeyVaultSecret>> GetSecretAsync(string name, string? version, SecretContentType? outContentType, CancellationToken cancellationToken = default) =>
        Resolve(name);

    public override Task<Response<KeyVaultSecret>> GetSecretAsync(string name, string? version = null, CancellationToken cancellationToken = default) =>
        Resolve(name);

    private Task<Response<KeyVaultSecret>> Resolve(string name)
    {
        LastRequestedName = name;
        var secret = lookup(name);

        if (secret is null)
        {
            throw new RequestFailedException(404, $"Secret '{name}' not found.");
        }

        return Task.FromResult(Response.FromValue(secret, new FakeAzureResponse()));
    }
}
