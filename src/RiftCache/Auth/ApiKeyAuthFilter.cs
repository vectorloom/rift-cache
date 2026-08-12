using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using RiftCache.Options;
using RiftCache.Providers.Secrets;

namespace RiftCache.Auth;

/// <summary>
/// Validates the X-RiftCache-Api-Key header against ISecretProvider. In single-tenant mode
/// (default) the expected secret is RIFTCACHE_API_KEY. In multi-tenant mode each tenant's key
/// is looked up as RIFTCACHE_API_KEY__{TENANTID}, per ARCHITECTURE_NOTES.md section 2.1.
/// </summary>
public sealed class ApiKeyAuthFilter : IEndpointFilter
{
    public const string ApiKeyHeaderName = "X-RiftCache-Api-Key";
    private const string SingleTenantSecretName = "RIFTCACHE_API_KEY";

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var httpContext = context.HttpContext;
        var providedKey = httpContext.Request.Headers[ApiKeyHeaderName].ToString();
        if (string.IsNullOrEmpty(providedKey))
        {
            return Results.Unauthorized();
        }

        var serverOptions = httpContext.RequestServices.GetRequiredService<IOptions<RiftCacheServerOptions>>().Value;

        string secretName;
        if (serverOptions.MultiTenant)
        {
            if (httpContext.Request.RouteValues.TryGetValue("tenantId", out var tenantIdValue) &&
                tenantIdValue is string tenantId &&
                !string.IsNullOrWhiteSpace(tenantId))
            {
                secretName = $"RIFTCACHE_API_KEY__{tenantId.ToUpperInvariant()}";
            }
            else
            {
                return Results.BadRequest("tenantId is required in multi-tenant mode.");
            }
        }
        else
        {
            secretName = SingleTenantSecretName;
        }

        var secretProvider = httpContext.RequestServices.GetRequiredService<ISecretProvider>();
        var expectedKey = await secretProvider.GetSecretAsync(secretName, httpContext.RequestAborted).ConfigureAwait(false);

        if (string.IsNullOrEmpty(expectedKey) || !FixedTimeEquals(providedKey, expectedKey))
        {
            return Results.Unauthorized();
        }

        return await next(context).ConfigureAwait(false);
    }

    private static bool FixedTimeEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(left), Encoding.UTF8.GetBytes(right));
}
