using Azure.Storage.Blobs;

namespace RiftCache.Providers.Azure.Tests.TestSupport;

/// <summary>
/// Subclasses BlobContainerClient/BlobClient rather than using a mocking framework, matching
/// FakeSecretClient's approach. GetBlobClient always returns a FakeBlobClient backed by this
/// container's shared in-memory dictionary, so blobs persist across calls within a test like a
/// real container would.
/// </summary>
internal sealed class FakeBlobContainerClient : BlobContainerClient
{
    private readonly Dictionary<string, FakeBlobClient.StoredBlob> _blobs = [];

    public override BlobClient GetBlobClient(string blobName) => new FakeBlobClient(blobName, _blobs);
}
