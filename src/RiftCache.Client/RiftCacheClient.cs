using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.Options;

namespace RiftCache.Client;

/// <summary>
/// Drop-in IDistributedCache implementation backed by the RiftCache REST API. Register via
/// AddRiftCache(...) rather than constructing directly, so the underlying HttpClient is
/// pooled and configured by IHttpClientFactory.
/// </summary>
public sealed class RiftCacheClient : IDistributedCache
{
    internal const string ApiKeyHeaderName = "X-RiftCache-Api-Key";
    internal const string AbsoluteExpirationHeader = "X-RiftCache-Absolute-Expiration";
    internal const string AbsoluteExpirationRelativeSecondsHeader = "X-RiftCache-Absolute-Expiration-Relative-Seconds";
    internal const string SlidingExpirationSecondsHeader = "X-RiftCache-Sliding-Expiration-Seconds";

    private static readonly MediaTypeHeaderValue OctetStreamContentType = new("application/octet-stream");

    private readonly HttpClient _httpClient;
    private readonly string? _tenantId;

    public RiftCacheClient(HttpClient httpClient, IOptions<RiftCacheOptions> options)
    {
        _httpClient = httpClient;
        _tenantId = options.Value.TenantId;
    }

    public byte[]? Get(string key) => GetAsync(key).GetAwaiter().GetResult();

    public async Task<byte[]?> GetAsync(string key, CancellationToken token = default)
    {
        using var response = await _httpClient.GetAsync(BuildUri(key), HttpCompletionOption.ResponseHeadersRead, token).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }

        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(token).ConfigureAwait(false);
    }

    public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
        SetAsync(key, value, options).GetAwaiter().GetResult();

    public async Task SetAsync(string key, byte[] value, DistributedCacheEntryOptions options, CancellationToken token = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, BuildUri(key))
        {
            Content = new ByteArrayContent(value),
        };
        request.Content.Headers.ContentType = OctetStreamContentType;
        ApplyExpirationHeaders(request, options);

        using var response = await _httpClient.SendAsync(request, token).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();
    }

    public void Refresh(string key) => RefreshAsync(key).GetAwaiter().GetResult();

    public async Task RefreshAsync(string key, CancellationToken token = default)
    {
        using var response = await _httpClient.PostAsync($"{BuildUri(key)}/refresh", content: null, token).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    public void Remove(string key) => RemoveAsync(key).GetAwaiter().GetResult();

    public async Task RemoveAsync(string key, CancellationToken token = default)
    {
        using var response = await _httpClient.DeleteAsync(BuildUri(key), token).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.NotFound)
        {
            return;
        }

        response.EnsureSuccessStatusCode();
    }

    private string BuildUri(string key)
    {
        var encodedKey = Uri.EscapeDataString(key);
        return string.IsNullOrEmpty(_tenantId)
            ? $"api/v1/cache/{encodedKey}"
            : $"api/v1/cache/{Uri.EscapeDataString(_tenantId)}/{encodedKey}";
    }

    private static void ApplyExpirationHeaders(HttpRequestMessage request, DistributedCacheEntryOptions options)
    {
        if (options.AbsoluteExpiration is { } absolute)
        {
            request.Headers.Add(AbsoluteExpirationHeader, absolute.ToString("O", CultureInfo.InvariantCulture));
        }
        else if (options.AbsoluteExpirationRelativeToNow is { } relative)
        {
            request.Headers.Add(AbsoluteExpirationRelativeSecondsHeader, relative.TotalSeconds.ToString(CultureInfo.InvariantCulture));
        }

        if (options.SlidingExpiration is { } sliding)
        {
            request.Headers.Add(SlidingExpirationSecondsHeader, sliding.TotalSeconds.ToString(CultureInfo.InvariantCulture));
        }
    }
}
