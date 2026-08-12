using System.Net;

namespace RiftCache.Tests.Endpoints;

public class MultiTenantCacheEndpointsTests
{
    private static readonly Dictionary<string, string> TenantSecrets = new()
    {
        ["RIFTCACHE_API_KEY__TEAM-A"] = "team-a-key",
        ["RIFTCACHE_API_KEY__TEAM-B"] = "team-b-key",
    };

    [Fact]
    public async Task SetThenGet_IsScopedPerTenant_AndDoesNotLeakAcrossTenants()
    {
        using var factory = new RiftCacheWebApplicationFactory(TenantSecrets, multiTenant: true);

        using var teamAClient = factory.CreateClient();
        teamAClient.DefaultRequestHeaders.Add("X-RiftCache-Api-Key", "team-a-key");

        using var teamBClient = factory.CreateClient();
        teamBClient.DefaultRequestHeaders.Add("X-RiftCache-Api-Key", "team-b-key");

        await teamAClient.PutAsync("/api/v1/cache/team-a/shared-key", new ByteArrayContent("a-value"u8.ToArray()));

        var teamBGet = await teamBClient.GetAsync("/api/v1/cache/team-b/shared-key");
        Assert.Equal(HttpStatusCode.NotFound, teamBGet.StatusCode);

        var teamAGet = await teamAClient.GetAsync("/api/v1/cache/team-a/shared-key");
        Assert.Equal(HttpStatusCode.OK, teamAGet.StatusCode);
    }

    [Fact]
    public async Task Get_WithAnotherTenantsKey_ReturnsUnauthorized()
    {
        using var factory = new RiftCacheWebApplicationFactory(TenantSecrets, multiTenant: true);
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Add("X-RiftCache-Api-Key", "team-b-key");

        var response = await client.GetAsync("/api/v1/cache/team-a/shared-key");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
