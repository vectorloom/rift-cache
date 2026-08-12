using RiftCache.Providers.Secrets;

namespace RiftCache.Tests.TestSupport;

internal sealed class StubSecretProvider(IReadOnlyDictionary<string, string> secrets) : ISecretProvider
{
    public Task<string?> GetSecretAsync(string key, CancellationToken token = default) =>
        Task.FromResult(secrets.TryGetValue(key, out var value) ? value : null);
}
