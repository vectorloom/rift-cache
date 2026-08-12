# Architecture Notes — Addendum

This addendum captures the changes needed to move the original design (internal Service Fabric replacement, Azure-only, single-org) toward a public, multi-cloud, self-host-friendly open-source project. Treat this as the working brief for implementation — bring it into Claude Code alongside the original `01-05` design docs.

---

## 1. Renaming

The original docs used two inconsistent placeholder names:
- `SoCreate.Extensions.Caching.ContainerApps` (docs 01-03) — this matches the namespace of an existing real open-source project by SoCreate. **Do not carry this namespace, branding, or file structure into the public repo without checking their license/attribution requirements.** Treat it as reference inspiration only, not a starting point to copy from directly.
- `Quantum.Caching` (IMPLEMENTATION_GUIDE.md) — leftover from an earlier naming idea.

**New naming convention:**

| Old | New |
|---|---|
| `SoCreate.Extensions.Caching.ContainerApps` / `Quantum.Caching` (service) | `RiftCache` |
| `SoCreate.Extensions.Caching.ContainerApps.NuGet` (client) | `RiftCache.Client` |
| `AddContainerAppsDistributedCache(...)` | `AddRiftCache(...)` |
| `ContainerAppsCacheClient` | `RiftCacheClient` |
| `ContainerAppsCacheOptions` | `RiftCacheOptions` |
| Docker image `distcache:latest` | `riftcache/riftcache:latest` |
| Repo/solution name | `RiftCache` |

Apply this rename across all five numbered design docs and the implementation guide before generating code from them.

---

## 2. Provider Abstraction Pattern

The original design called Azure services (Key Vault, Managed Identity, Blob Storage, Application Insights) directly from core service code. To support self-hosters and future AWS/GCP contributions, the core must depend only on interfaces — cloud-specific code lives in separate, optional provider packages.

### 2.1 Secrets / Auth — `ISecretProvider`

```csharp
public interface ISecretProvider
{
    Task<string?> GetSecretAsync(string key, CancellationToken token = default);
}
```

Implementations:
- `EnvironmentSecretProvider` — **default**, reads from environment variables / mounted files. Zero cloud dependency. This is what self-hosters use.
- `AzureKeyVaultSecretProvider` — optional package, uses Managed Identity.
- `AwsSecretsManagerProvider` — community-contributed.
- `GcpSecretManagerProvider` — community-contributed.

This replaces the original design's direct Key Vault calls in `AuthenticationMiddleware` and `ApiKeyProvider`.

### 2.2 Persistence — `IPersistenceProvider`

Already interface-based in the original design (`03_IMPLEMENTATION.md`) — good foundation, keep it. Just formalize that:
- `NullPersistenceProvider` — **default**, in-memory only, no persistence. Fine for local dev and low-stakes caching.
- `AzureBlobPersistenceProvider` — the existing Azure design, becomes optional.
- `S3PersistenceProvider` — community-contributed.
- `GcsPersistenceProvider` — community-contributed.

### 2.3 Observability — `ITelemetryProvider` (or adopt OpenTelemetry directly)

Replace direct Application Insights SDK usage with **OpenTelemetry** for metrics, tracing, and logging. OpenTelemetry is vendor-neutral and exports to App Insights, CloudWatch, Cloud Monitoring, Prometheus, or anything else via standard exporters — no custom abstraction needed here, just don't couple core code to `Microsoft.ApplicationInsights` directly.

---

## 3. Deployment Layout

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

Core container image ships with **no cloud SDK dependencies**. Cloud-specific provider packages (`RiftCache.Providers.Azure`, `RiftCache.Providers.Aws`, `RiftCache.Providers.Gcp`) are referenced only if that provider is configured — keeps the base image small and keeps AWS/GCP users from pulling in Azure SDKs they'll never use.

---

## 4. Multi-Tenancy: Optional, Not Default

The original design assumed multi-tenancy (one instance per team, Key Vault-backed per-tenant API keys) as the baseline. Keep multi-tenancy as a **feature**, not the default:

- **Single-tenant mode (default)**: one API key, no `tenantId` required in URLs, minimal config. This is what a self-hoster running one instance for their own app uses.
- **Multi-tenant mode (opt-in)**: the original tenant-scoped URL structure (`/api/v1/cache/{tenantId}/...`) and per-tenant secret isolation remain available for enterprise deployments running a shared instance across teams.

This should be a config flag, not two separate codebases.

---

## 5. What Moves Out of Public Docs

`05_MIGRATION_ROLLOUT.md` and most of the original `README.md` (12-week timeline, budget, FTE allocation, leadership approval, per-team rollout waves) describe an internal Service Fabric migration plan. Since that migration isn't happening at your org, this content doesn't belong in the public repo. Either drop it entirely or keep it privately if useful as a personal reference for how an enterprise adopter *might* plan a rollout — but it shouldn't ship as the project's main README.
