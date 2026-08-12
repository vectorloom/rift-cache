using Microsoft.Extensions.Options;
using RiftCache.Caching;
using RiftCache.Endpoints;
using RiftCache.Options;
using RiftCache.Providers.Persistence;
using RiftCache.Providers.Secrets;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<RiftCacheServerOptions>(builder.Configuration.GetSection(RiftCacheServerOptions.SectionName));

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ISecretProvider, EnvironmentSecretProvider>();
builder.Services.AddSingleton<IPersistenceProvider, NullPersistenceProvider>();
builder.Services.AddSingleton<InMemoryCacheStore>();
builder.Services.AddSingleton<ICacheStore>(sp => sp.GetRequiredService<InMemoryCacheStore>());
builder.Services.AddHostedService<CacheEvictionService>();

var app = builder.Build();

var serverOptions = app.Services.GetRequiredService<IOptions<RiftCacheServerOptions>>().Value;

app.MapGet("/", () => Results.Ok(new
{
    service = "RiftCache",
    version = typeof(Program).Assembly.GetName().Version?.ToString(3) ?? "0.0.0",
    mode = serverOptions.MultiTenant ? "multi-tenant" : "single-tenant",
    endpoints = new
    {
        health = "/healthz",
        cache = serverOptions.MultiTenant ? "/api/v1/cache/{tenantId}/{key}" : "/api/v1/cache/{key}",
    },
}));
app.MapGet("/healthz", () => Results.Ok(new { status = "healthy" }));
app.MapCacheEndpoints(serverOptions);

app.Run();

// Exposed so WebApplicationFactory<Program> can bootstrap this app in integration tests.
public partial class Program;
