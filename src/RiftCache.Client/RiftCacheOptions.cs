namespace RiftCache.Client;

public sealed class RiftCacheOptions
{
    /// <summary>Base URL of the RiftCache service, e.g. "http://localhost:8080".</summary>
    public string ServiceUrl { get; set; } = string.Empty;

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Set only when the service is running in multi-tenant mode.</summary>
    public string? TenantId { get; set; }
}
