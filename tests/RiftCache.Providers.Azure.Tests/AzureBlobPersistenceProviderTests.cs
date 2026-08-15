using RiftCache.Caching;
using RiftCache.Providers.Azure.Tests.TestSupport;

namespace RiftCache.Providers.Azure.Tests;

public class AzureBlobPersistenceProviderTests
{
    [Fact]
    public async Task PersistThenLoad_RoundTripsValueAndBothExpirations()
    {
        var provider = new AzureBlobPersistenceProvider(new FakeBlobContainerClient());
        var absolute = DateTimeOffset.UtcNow.AddMinutes(30);
        var sliding = TimeSpan.FromSeconds(45);

        await provider.PersistAsync("key", new CacheEntry([1, 2, 3], absolute, sliding));
        var loaded = await provider.LoadAsync("key");

        Assert.NotNull(loaded);
        Assert.Equal(new byte[] { 1, 2, 3 }, loaded.Value);
        Assert.Equal(absolute, loaded.AbsoluteExpiration);
        Assert.Equal(sliding, loaded.SlidingExpiration);
    }

    [Fact]
    public async Task PersistThenLoad_NoExpiration_RoundTripsNullFields()
    {
        var provider = new AzureBlobPersistenceProvider(new FakeBlobContainerClient());

        await provider.PersistAsync("key", new CacheEntry([1], null, null));
        var loaded = await provider.LoadAsync("key");

        Assert.NotNull(loaded);
        Assert.Null(loaded.AbsoluteExpiration);
        Assert.Null(loaded.SlidingExpiration);
    }

    [Fact]
    public async Task LoadAsync_UnknownKey_ReturnsNull()
    {
        var provider = new AzureBlobPersistenceProvider(new FakeBlobContainerClient());

        Assert.Null(await provider.LoadAsync("missing"));
    }

    [Fact]
    public async Task RemoveAsync_ExistingKey_DeletesBlob()
    {
        var provider = new AzureBlobPersistenceProvider(new FakeBlobContainerClient());
        await provider.PersistAsync("key", new CacheEntry([1], null, null));

        await provider.RemoveAsync("key");

        Assert.Null(await provider.LoadAsync("key"));
    }

    [Fact]
    public async Task RemoveAsync_UnknownKey_DoesNotThrow()
    {
        var provider = new AzureBlobPersistenceProvider(new FakeBlobContainerClient());

        await provider.RemoveAsync("missing");
    }

    [Fact]
    public async Task PersistThenLoad_KeyWithSlashesAndSpaces_RoundTrips()
    {
        var provider = new AzureBlobPersistenceProvider(new FakeBlobContainerClient());
        const string key = "team a/cache key";

        await provider.PersistAsync(key, new CacheEntry([9], null, null));
        var loaded = await provider.LoadAsync(key);

        Assert.NotNull(loaded);
        Assert.Equal(new byte[] { 9 }, loaded.Value);
    }

    [Theory]
    [InlineData("simple", "simple")]
    [InlineData("team a/key", "team%20a%2Fkey")]
    public void ToBlobName_EncodesKeyConsistently(string key, string expectedBlobName) =>
        Assert.Equal(expectedBlobName, AzureBlobPersistenceProvider.ToBlobName(key));
}
