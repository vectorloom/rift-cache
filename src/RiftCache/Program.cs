using Microsoft.Extensions.Options;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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
builder.Services.AddSingleton<CacheMetrics>();
builder.Services.AddSingleton<ICacheStore>(sp =>
    new MeteredCacheStore(sp.GetRequiredService<InMemoryCacheStore>(), sp.GetRequiredService<CacheMetrics>()));
builder.Services.AddHostedService<CacheEvictionService>();

// Only export via OTLP when an endpoint is actually configured -- otherwise a self-hoster who set
// nothing gets zero telemetry noise (no attempted connections, no connection-refused warnings).
// Read through IConfiguration, not Environment.GetEnvironmentVariable, for the same reason
// EnvironmentSecretProvider does: picks up env vars, user secrets, and config files uniformly.
var otlpEndpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"];

builder.Services.AddOpenTelemetry()
    .ConfigureResource(resource => resource.AddService("RiftCache"))
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation();

        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            tracing.AddOtlpExporter();
        }
    })
    .WithMetrics(metrics =>
    {
        metrics.AddAspNetCoreInstrumentation();
        metrics.AddMeter(CacheMetrics.MeterName);

        if (!string.IsNullOrEmpty(otlpEndpoint))
        {
            metrics.AddOtlpExporter();
        }
    });

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
