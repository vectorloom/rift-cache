using Microsoft.Extensions.Options;
using RiftCache;
using RiftCache.Options;
using RiftCache.Providers.Azure;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRiftCacheCore(builder.Configuration);

var blobContainerUrl = builder.Configuration["RIFTCACHE_BLOB_CONTAINER_URL"];
if (string.IsNullOrWhiteSpace(blobContainerUrl))
{
    throw new InvalidOperationException(
        "RIFTCACHE_BLOB_CONTAINER_URL is required by RiftCache.Azure (e.g. " +
        "https://<account>.blob.core.windows.net/<container>). Use the core RiftCache image " +
        "instead if you only need memory-only caching.");
}

builder.Services.AddAzureBlobPersistenceProvider(new Uri(blobContainerUrl));

var app = builder.Build();

var serverOptions = app.Services.GetRequiredService<IOptions<RiftCacheServerOptions>>().Value;
app.MapRiftCacheCore(serverOptions);

app.Run();

// Exposed so WebApplicationFactory<Program> can bootstrap this app in integration tests.
public partial class Program;
