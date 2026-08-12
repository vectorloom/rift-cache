# Architecture

RiftCache's core has no hard dependency on any single cloud provider. Cloud integration happens entirely through pluggable provider interfaces, so the project can run self-hosted with zero cloud dependencies, or scale up to a full multi-cloud, multi-tenant deployment.

---

## Provider Abstraction Pattern

### Secrets / Auth — `ISecretProvider`

```csharp
public interface ISecretProvider
{
    Task<string?> GetSecretAsync(string key, CancellationToken token = default);
}
```

Implementations:
- `EnvironmentSecretProvider` — **default**, reads through `IConfiguration`: environment variables, user secrets, and mounted-file secrets (a `{KEY}_FILE` entry pointing at a file, the Docker/Kubernetes secrets-mount convention) all resolve the same way. Zero cloud dependency. This is what self-hosters use. Multi-tenant secret names nest with `__` (e.g. `RIFTCACHE_API_KEY__{TENANT}`), matching `IConfiguration`'s own environment-variable nesting convention.
- `AzureKeyVaultSecretProvider` — optional package, uses Managed Identity.
- `AwsSecretsManagerProvider` — community-contributed.
- `GcpSecretManagerProvider` — community-contributed.

### Persistence — `IPersistenceProvider`

```csharp
public interface IPersistenceProvider
{
    Task PersistAsync(string key, CacheEntry entry, CancellationToken token = default);
    Task<CacheEntry?> LoadAsync(string key, CancellationToken token = default);
    Task RemoveAsync(string key, CancellationToken token = default);
}
```

`CacheEntry` carries the value alongside its absolute/sliding expiration, so a real provider can restore TTLs across a restart or replica — not just raw bytes.

Implementations:
- `NullPersistenceProvider` — **default**, in-memory only, no persistence. Fine for local dev and low-stakes caching.
- `AzureBlobPersistenceProvider` — Azure reference implementation.
- `S3PersistenceProvider` — community-contributed.
- `GcsPersistenceProvider` — community-contributed.

### Observability — OpenTelemetry

Metrics, tracing, and logging go through **OpenTelemetry** rather than a custom abstraction. OpenTelemetry is vendor-neutral and exports to App Insights, CloudWatch, Cloud Monitoring, Prometheus, or anything else via standard exporters — core code stays uncoupled from any specific telemetry backend.

---

## Deployment Layout

```
deployment/
├── docker/
│   ├── Dockerfile              (cloud-agnostic — core image, no cloud SDKs baked in)
│   └── docker-compose.yml      (local dev: RiftCache + optional storage emulator)
├── azure/
│   └── bicep/                  (reference deployment — Container Apps, Key Vault, Blob Storage)
├── aws/
│   └── terraform/              (community — Fargate/ECS, Secrets Manager, S3)
└── gcp/
    └── terraform/              (community — Cloud Run, Secret Manager, GCS)
```

The core container image ships with **no cloud SDK dependencies**. Cloud-specific provider packages (`RiftCache.Providers.Azure`, `RiftCache.Providers.Aws`, `RiftCache.Providers.Gcp`) are referenced only if that provider is configured — this keeps the base image small and keeps AWS/GCP users from pulling in Azure SDKs they'll never use.

---

## Multi-Tenancy: Optional, Not Default

Multi-tenancy is a **feature**, not the default:

- **Single-tenant mode (default)**: one API key, no `tenantId` required in URLs, minimal config. This is what a self-hoster running one instance for their own app uses.
- **Multi-tenant mode (opt-in)**: tenant-scoped URLs (`/api/v1/cache/{tenantId}/...`) and per-tenant secret isolation, for larger or shared deployments. See [ENTERPRISE_DEPLOYMENT.md](ENTERPRISE_DEPLOYMENT.md) for what's involved in running this way.

This is a config flag, not two separate codebases.

---

## Contributing a New Provider

See [CONTRIBUTING.md](CONTRIBUTING.md) for the step-by-step process of adding a new `ISecretProvider` or `IPersistenceProvider` implementation (e.g., for a cloud not yet supported).