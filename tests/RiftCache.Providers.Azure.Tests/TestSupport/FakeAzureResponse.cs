using Azure;
using Azure.Core;

namespace RiftCache.Providers.Azure.Tests.TestSupport;

/// <summary>
/// Minimal Response stand-in so tests can build a Response&lt;KeyVaultSecret&gt; via
/// Response.FromValue(...) without a real HTTP round trip. AzureKeyVaultSecretProvider only ever
/// reads response.Value, never the raw response, so the members below just need to satisfy the
/// abstract contract, not behave realistically.
/// </summary>
internal sealed class FakeAzureResponse : Response
{
    public override int Status => 200;

    public override string ReasonPhrase => "OK";

    public override Stream? ContentStream { get; set; }

    public override string ClientRequestId { get; set; } = string.Empty;

    public override void Dispose()
    {
    }

    protected override bool TryGetHeader(string name, out string value)
    {
        value = string.Empty;
        return false;
    }

    protected override bool TryGetHeaderValues(string name, out IEnumerable<string> values)
    {
        values = [];
        return false;
    }

    protected override bool ContainsHeader(string name) => false;

    protected override IEnumerable<HttpHeader> EnumerateHeaders() => [];
}
