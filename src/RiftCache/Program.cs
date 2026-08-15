using Microsoft.Extensions.Options;
using RiftCache;
using RiftCache.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRiftCacheCore(builder.Configuration);

var app = builder.Build();

var serverOptions = app.Services.GetRequiredService<IOptions<RiftCacheServerOptions>>().Value;
app.MapRiftCacheCore(serverOptions);

app.Run();

// Exposed so WebApplicationFactory<Program> can bootstrap this app in integration tests.
public partial class Program;
