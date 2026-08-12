using System.Net;

namespace RiftCache.Tests.Endpoints;

public class CacheEndpointsTests
{
    private const string ApiKey = "test-api-key";

    [Fact]
    public async Task Root_ReturnsServiceBanner_WithoutAuth()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("RiftCache", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Healthz_ReturnsOk_WithoutAuth()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/healthz");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithoutApiKey_ReturnsUnauthorized()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/v1/cache/some-key");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Get_WithWrongApiKey_ReturnsUnauthorized()
    {
        using var factory = CreateFactory();
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-RiftCache-Api-Key", "wrong-key");

        var response = await client.GetAsync("/api/v1/cache/some-key");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task SetThenGet_RoundTripsValue()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var putResponse = await client.PutAsync("/api/v1/cache/round-trip", new ByteArrayContent("hello"u8.ToArray()));
        Assert.Equal(HttpStatusCode.NoContent, putResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/v1/cache/round-trip");
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);
        Assert.Equal("hello"u8.ToArray(), await getResponse.Content.ReadAsByteArrayAsync());
    }

    [Fact]
    public async Task Get_UnknownKey_ReturnsNotFound()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.GetAsync("/api/v1/cache/never-set");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetThenDelete_RemovesValue()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        await client.PutAsync("/api/v1/cache/to-delete", new ByteArrayContent("bye"u8.ToArray()));

        var deleteResponse = await client.DeleteAsync("/api/v1/cache/to-delete");
        Assert.Equal(HttpStatusCode.NoContent, deleteResponse.StatusCode);

        var getResponse = await client.GetAsync("/api/v1/cache/to-delete");
        Assert.Equal(HttpStatusCode.NotFound, getResponse.StatusCode);
    }

    [Fact]
    public async Task Delete_UnknownKey_ReturnsNotFound()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.DeleteAsync("/api/v1/cache/never-set");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task SetWithSlidingExpiration_ThenRefresh_ReturnsNoContent()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        using var putRequest = new HttpRequestMessage(HttpMethod.Put, "/api/v1/cache/sliding")
        {
            Content = new ByteArrayContent("x"u8.ToArray()),
        };
        putRequest.Headers.Add("X-RiftCache-Sliding-Expiration-Seconds", "60");
        await client.SendAsync(putRequest);

        var refreshResponse = await client.PostAsync("/api/v1/cache/sliding/refresh", content: null);

        Assert.Equal(HttpStatusCode.NoContent, refreshResponse.StatusCode);
    }

    [Fact]
    public async Task Refresh_UnknownKey_ReturnsNotFound()
    {
        using var factory = CreateFactory();
        using var client = CreateAuthorizedClient(factory);

        var response = await client.PostAsync("/api/v1/cache/never-set/refresh", content: null);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    private static RiftCacheWebApplicationFactory CreateFactory() =>
        new(new Dictionary<string, string> { ["RIFTCACHE_API_KEY"] = ApiKey });

    private static HttpClient CreateAuthorizedClient(RiftCacheWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-RiftCache-Api-Key", ApiKey);
        return client;
    }
}
