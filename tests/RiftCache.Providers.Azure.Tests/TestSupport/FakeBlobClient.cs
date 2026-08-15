using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace RiftCache.Providers.Azure.Tests.TestSupport;

internal sealed class FakeBlobClient(string name, Dictionary<string, FakeBlobClient.StoredBlob> store) : BlobClient
{
    internal readonly record struct StoredBlob(byte[] Content, IDictionary<string, string> Metadata);

    public override Task<Response<BlobContentInfo>> UploadAsync(Stream content, BlobUploadOptions options, CancellationToken cancellationToken = default)
    {
        using var buffer = new MemoryStream();
        content.CopyTo(buffer);
        store[name] = new StoredBlob(buffer.ToArray(), options.Metadata ?? new Dictionary<string, string>());

        var info = BlobsModelFactory.BlobContentInfo(default, DateTimeOffset.UtcNow, [], null, 0L);
        return Task.FromResult(Response.FromValue(info, new FakeAzureResponse()));
    }

    public override Task<Response<BlobDownloadResult>> DownloadContentAsync(CancellationToken cancellationToken = default)
    {
        if (!store.TryGetValue(name, out var blob))
        {
            throw new RequestFailedException(404, $"Blob '{name}' not found.");
        }

        var details = BlobsModelFactory.BlobDownloadDetails(metadata: blob.Metadata);
        var result = BlobsModelFactory.BlobDownloadResult(BinaryData.FromBytes(blob.Content), details);
        return Task.FromResult(Response.FromValue(result, new FakeAzureResponse()));
    }

    public override Task<Response<bool>> DeleteIfExistsAsync(
        DeleteSnapshotsOption snapshotsOption = default,
        BlobRequestConditions? conditions = null,
        CancellationToken cancellationToken = default)
    {
        var removed = store.Remove(name);
        return Task.FromResult(Response.FromValue(removed, new FakeAzureResponse()));
    }
}
