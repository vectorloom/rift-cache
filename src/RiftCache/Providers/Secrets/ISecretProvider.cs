namespace RiftCache.Providers.Secrets;

public interface ISecretProvider
{
    Task<string?> GetSecretAsync(string key, CancellationToken token = default);
}
