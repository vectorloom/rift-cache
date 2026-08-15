using System.Globalization;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using RiftCache.Caching;
using RiftCache.Providers.Persistence;

namespace RiftCache.Providers.Azure;

/// <summary>
/// IPersistenceProvider backed by Azure Blob Storage. Register via
/// AddAzureBlobPersistenceProvider(...) rather than constructing directly.
///
/// The cached bytes (CacheEntry.Value) are the blob's content directly -- inspectable, no envelope
/// format to invent or maintain. AbsoluteExpiration/SlidingExpiration live in blob metadata
/// instead. LastAccessed is deliberately not persisted: InMemoryCacheStore.GetAsync re-Touch()es an
/// entry the moment it's promoted from persistence back into memory, so a stored value would never
/// actually be read.
/// </summary>
public sealed class AzureBlobPersistenceProvider(BlobContainerClient container) : IPersistenceProvider
{
    private const string AbsoluteExpirationMetadataKey = "absoluteExpiration";
    private const string SlidingExpirationMetadataKey = "slidingExpiration";

    public async Task PersistAsync(string key, CacheEntry entry, CancellationToken token = default)
    {
        var blob = container.GetBlobClient(ToBlobName(key));
        var options = new BlobUploadOptions
        {
            Metadata = ToMetadata(entry),
            HttpHeaders = new BlobHttpHeaders { ContentType = "application/octet-stream" },
        };

        using var content = new MemoryStream(entry.Value, writable: false);
        await blob.UploadAsync(content, options, token).ConfigureAwait(false);
    }

    public async Task<CacheEntry?> LoadAsync(string key, CancellationToken token = default)
    {
        var blob = container.GetBlobClient(ToBlobName(key));

        try
        {
            var response = await blob.DownloadContentAsync(token).ConfigureAwait(false);
            var (absoluteExpiration, slidingExpiration) = FromMetadata(response.Value.Details.Metadata);
            return new CacheEntry(response.Value.Content.ToArray(), absoluteExpiration, slidingExpiration);
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            return null;
        }
    }

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        var blob = container.GetBlobClient(ToBlobName(key));
        await blob.DeleteIfExistsAsync(cancellationToken: token).ConfigureAwait(false);
    }

    internal static string ToBlobName(string key) => Uri.EscapeDataString(key);

    private static IDictionary<string, string> ToMetadata(CacheEntry entry)
    {
        var metadata = new Dictionary<string, string>();

        if (entry.AbsoluteExpiration is { } absolute)
        {
            metadata[AbsoluteExpirationMetadataKey] = absolute.ToString("O", CultureInfo.InvariantCulture);
        }

        if (entry.SlidingExpiration is { } sliding)
        {
            metadata[SlidingExpirationMetadataKey] = sliding.TotalSeconds.ToString(CultureInfo.InvariantCulture);
        }

        return metadata;
    }

    private static (DateTimeOffset? Absolute, TimeSpan? Sliding) FromMetadata(IDictionary<string, string> metadata)
    {
        DateTimeOffset? absolute = null;
        if (metadata.TryGetValue(AbsoluteExpirationMetadataKey, out var absoluteRaw) &&
            DateTimeOffset.TryParse(absoluteRaw, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedAbsolute))
        {
            absolute = parsedAbsolute;
        }

        TimeSpan? sliding = null;
        if (metadata.TryGetValue(SlidingExpirationMetadataKey, out var slidingRaw) &&
            double.TryParse(slidingRaw, NumberStyles.Float, CultureInfo.InvariantCulture, out var slidingSeconds))
        {
            sliding = TimeSpan.FromSeconds(slidingSeconds);
        }

        return (absolute, sliding);
    }
}
