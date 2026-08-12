namespace RiftCache.Options;

public sealed class RiftCacheServerOptions
{
    public const string SectionName = "RiftCache";

    /// <summary>
    /// When true, cache keys are scoped under /api/v1/cache/{tenantId}/{key} and each
    /// tenant's API key is looked up as RIFTCACHE_API_KEY__{TENANTID} via ISecretProvider.
    /// When false (default), a single RIFTCACHE_API_KEY secures /api/v1/cache/{key}.
    /// </summary>
    public bool MultiTenant { get; set; }

    /// <summary>Maximum number of entries the in-memory store holds before evicting least-recently-used entries. 0 = unlimited.</summary>
    public int MaxItems { get; set; }

    public TimeSpan EvictionScanInterval { get; set; } = TimeSpan.FromSeconds(30);
}
