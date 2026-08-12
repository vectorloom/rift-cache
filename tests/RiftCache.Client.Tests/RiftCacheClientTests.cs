using System.Net;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;
using RiftCache.Client.Tests.TestSupport;

namespace RiftCache.Client.Tests;

public class RiftCacheClientTests
{
    [Fact]
    public async Task GetAsync_Found_ReturnsBytes()
    {
        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([1, 2, 3]),
        });

        var result = await client.GetAsync("key");

        Assert.Equal(new byte[] { 1, 2, 3 }, result);
    }

    [Fact]
    public async Task GetAsync_NotFound_ReturnsNull()
    {
        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        Assert.Null(await client.GetAsync("key"));
    }

    [Fact]
    public async Task GetAsync_ServerError_Throws()
    {
        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));

        await Assert.ThrowsAsync<HttpRequestException>(() => client.GetAsync("key"));
    }

    [Fact]
    public async Task GetAsync_EncodesKeyAndPrependsConfiguredTenant()
    {
        var (client, handler) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound), tenantId: "team a");

        await client.GetAsync("key/with slash");

        var requestUri = handler.LastRequest!.RequestUri!.AbsoluteUri;
        Assert.Contains("api/v1/cache/team%20a/key%2Fwith%20slash", requestUri);
    }

    [Fact]
    public async Task SetAsync_WithAbsoluteExpirationRelativeToNow_SendsRelativeSecondsHeader()
    {
        var (client, handler) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        await client.SetAsync("key", [1], new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5),
        });

        Assert.Equal(HttpMethod.Put, handler.LastRequest!.Method);
        Assert.Equal("300", handler.LastRequest.Headers.GetValues("X-RiftCache-Absolute-Expiration-Relative-Seconds").Single());
    }

    [Fact]
    public async Task SetAsync_WithSlidingExpiration_SendsSlidingSecondsHeader()
    {
        var (client, handler) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        await client.SetAsync("key", [1], new DistributedCacheEntryOptions
        {
            SlidingExpiration = TimeSpan.FromSeconds(30),
        });

        Assert.Equal("30", handler.LastRequest!.Headers.GetValues("X-RiftCache-Sliding-Expiration-Seconds").Single());
    }

    [Fact]
    public async Task SetAsync_WithNoExpirationOptions_SendsNoExpirationHeaders()
    {
        var (client, handler) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        await client.SetAsync("key", [1], new DistributedCacheEntryOptions());

        Assert.False(handler.LastRequest!.Headers.Contains("X-RiftCache-Absolute-Expiration-Relative-Seconds"));
        Assert.False(handler.LastRequest.Headers.Contains("X-RiftCache-Sliding-Expiration-Seconds"));
    }

    [Fact]
    public async Task RefreshAsync_PostsToRefreshSubPath()
    {
        var (client, handler) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        await client.RefreshAsync("key");

        Assert.Equal(HttpMethod.Post, handler.LastRequest!.Method);
        Assert.Contains("key/refresh", handler.LastRequest.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task RefreshAsync_NotFound_DoesNotThrow()
    {
        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        await client.RefreshAsync("key");
    }

    [Fact]
    public async Task RemoveAsync_SendsDelete()
    {
        var (client, handler) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        await client.RemoveAsync("key");

        Assert.Equal(HttpMethod.Delete, handler.LastRequest!.Method);
    }

    [Fact]
    public async Task RemoveAsync_NotFound_DoesNotThrow()
    {
        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NotFound));

        await client.RemoveAsync("key");
    }

    [Fact]
    public void Get_SyncWrapper_ReturnsSameResultAsAsync()
    {
        var (client, _) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new ByteArrayContent([7]),
        });

        Assert.Equal(new byte[] { 7 }, client.Get("key"));
    }

    [Fact]
    public void Set_SyncWrapper_SendsRequest()
    {
        var (client, handler) = CreateClient(_ => new HttpResponseMessage(HttpStatusCode.NoContent));

        client.Set("key", [1], new DistributedCacheEntryOptions());

        Assert.Equal(HttpMethod.Put, handler.LastRequest!.Method);
    }

    private static (RiftCacheClient Client, FakeHttpMessageHandler Handler) CreateClient(
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        string? tenantId = null)
    {
        var handler = new FakeHttpMessageHandler(responder);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("http://localhost/") };
        var options = Options.Create(new RiftCacheOptions
        {
            ServiceUrl = "http://localhost/",
            ApiKey = "test-key",
            TenantId = tenantId,
        });

        return (new RiftCacheClient(httpClient, options), handler);
    }
}
