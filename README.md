# RiftCache

**Distributed caching, cloud-native by design.**

RiftCache is an open-source, Redis-inspired distributed cache built for containerized .NET applications. It ships as a drop-in `IDistributedCache` implementation with a simple REST API, runs anywhere containers run, and is designed to scale from a single self-hosted instance to a multi-tenant enterprise deployment.

- **Self-hosters**: one container, one API key, no cloud lock-in required.
- **Enterprise teams**: multi-tenancy, pluggable secrets/persistence providers, OpenTelemetry (traces + metrics), and Azure reference providers (Key Vault, Blob Storage — see [docs/providers/azure.md](docs/providers/azure.md)) today. AWS and GCP implementations of the same interfaces are designed for (see [ARCHITECTURE.md](ARCHITECTURE.md)) but not built yet — contributions welcome, see [CONTRIBUTING.md](CONTRIBUTING.md).

> Reference deployments today: **Docker / Podman** — see [Building From Source](#building-from-source-docker--podman) below — and **Azure Container Apps** (Bicep) — see [deployment/azure/bicep](deployment/azure/bicep). AWS (Fargate/ECS) and GCP (Cloud Run) reference deployments are planned, not built yet. Contributions welcome — see [CONTRIBUTING.md](CONTRIBUTING.md).

---

## Why RiftCache?

RiftCache is a distributed cache built to slot into infrastructure you already run, rather than adding a new managed service to your bill and your ops surface.

- **A faster local dev loop.** Run the exact same cache your production app uses, locally, in one container — no separate Redis install, no "works differently in dev vs. prod" drift. Since RiftCache implements `IDistributedCache` directly, your code never branches on environment.
- **No vendor lock-in.** Open source, MIT-licensed, runs on any container platform. Your caching layer isn't tied to a specific cloud's managed offering.
- **Full visibility and control.** It's your code running in your environment — inspect it, extend it, fix it, without waiting on a vendor's support queue or feature roadmap.
- **Free when it rides on capacity you already have.** If you're running a Dedicated Azure Container Apps workload profile (or equivalent reserved compute) with headroom, RiftCache can share that profile with internal-only ingress — no new billing line, no public request charges, near-zero marginal cost. (This isn't true of every deployment — see below.)

### When RiftCache makes sense
- Local and CI/CD environments, as a drop-in Redis-like cache with zero setup friction
- Teams with spare capacity on an existing Dedicated Container Apps profile (or equivalent reserved compute elsewhere)
- Teams that specifically need persistence but don't want to pay for Redis Premium's clustering/VNet bundle just to get it
- Teams that want a caching layer they fully own and can audit/extend

### When it might not
- Deployed **standalone** with dedicated new compute, it's not reliably cheaper than a managed Redis Standard tier — compute is compute either way.
- If you need Redis's full feature set (pub/sub, sorted sets, Lua scripting, modules), RiftCache's simpler data model won't cover you.
- If your team would rather not own cache infrastructure operations at all, a managed service's SLA and support may be worth the premium.

---

## Quick Start (Self-Hosted)

```bash
docker run --name riftcache -p 8080:8080 -e RIFTCACHE_API_KEY=dev-key riftcache/riftcache:latest
```

> **Two different names, easy to mix up when testing manually:** `RIFTCACHE_API_KEY` above is the
> **environment variable** the *server* reads at startup (via `EnvironmentSecretProvider`).
> Callers authenticate with the **HTTP header** `X-RiftCache-Api-Key` instead — see the curl
> example under [Building From Source](#building-from-source-docker--podman) below.

No published image yet — until there is, build it from source (below). The image is a
multi-stage build of just `src/RiftCache`, so it stays cloud-agnostic: no Azure/AWS/GCP
SDKs baked in, per [ARCHITECTURE.md](ARCHITECTURE.md).

```csharp
services.AddRiftCache(options =>
{
    options.ServiceUrl = "http://localhost:8080";
    options.ApiKey = "dev-key";
});

var cache = serviceProvider.GetRequiredService<IDistributedCache>();
await cache.SetAsync("key", value, new DistributedCacheEntryOptions
{
    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(30)
});
```

No Key Vault, no managed identity, no tenant setup required to get started. Persistence defaults to in-memory only unless you configure a storage provider.

### Installing RiftCache.Client

Not on nuget.org yet — every merge to `main` publishes a prerelease build to GitHub Packages
instead:

```bash
dotnet nuget add source https://nuget.pkg.github.com/vectorloom/index.json \
  --name github-vectorloom \
  --username YOUR_GITHUB_USERNAME \
  --password YOUR_GITHUB_PAT \
  --store-password-in-clear-text

dotnet add package RiftCache.Client --prerelease
```

GitHub Packages requires a PAT with `read:packages` scope to *pull* NuGet packages, even from a
public repo — that's a GitHub limitation, not something RiftCache adds on top.

### Building From Source (Docker / Podman)

The Dockerfile ([deployment/docker/Dockerfile](deployment/docker/Dockerfile)) builds and publishes
`src/RiftCache` in one stage, then copies just the published output into a slim runtime image
that runs as a non-root user. Build context must be the repo root, since it needs
`Directory.Build.props` alongside the project.

**Docker:**

```bash
docker build -f deployment/docker/Dockerfile -t riftcache/riftcache:latest .
docker run --name riftcache -p 8080:8080 -e RIFTCACHE_API_KEY=dev-key riftcache/riftcache:latest

# or, via compose:
docker compose -f deployment/docker/docker-compose.yml up --build
```

**Podman** (drop-in — same Dockerfile, same flags):

```bash
podman build -f deployment/docker/Dockerfile -t riftcache/riftcache:latest .
podman run --name riftcache -p 8080:8080 -e RIFTCACHE_API_KEY=dev-key riftcache/riftcache:latest
```

**On Windows**, `podman compose -f deployment/docker/docker-compose.yml up --build` currently
fails with "no Containerfile or Dockerfile ... found", even though the compose file is correct —
confirmed as an upstream `podman-compose` bug (1.6.0, the latest release as of this writing), not
specific to this project or to a nested `dockerfile:` path. `podman-compose` resolves the build
context to an absolute path (`D:\...`) before checking whether it's a remote git URL, but that
check (`is_context_git_url`, via `urllib.parse.urlparse`) treats the drive letter before the `:`
as a URL scheme, so it misidentifies any local Windows path as remote and skips local Dockerfile
resolution entirely — silently building with no `-f` at all. This will misfire for *any*
`podman-compose` build on Windows, not just this one. Use the `podman build` / `podman run`
commands above instead until upstream fixes it.

Either way, `RIFTCACHE_API_KEY` is read from the container's environment at runtime by
`EnvironmentSecretProvider` — it's never baked into the image. [deployment/docker/docker-compose.yml](deployment/docker/docker-compose.yml)
defaults it to `dev-key` for local convenience; override it by exporting `RIFTCACHE_API_KEY`
before running compose, or with a `.env` file next to it.

**Verify it's running:**

```bash
curl http://localhost:8080/healthz                                              # no auth needed
curl -H "X-RiftCache-Api-Key: dev-key" http://localhost:8080/api/v1/cache/hello # 404 — nothing set yet, but the header is accepted
```

Note the header is `X-RiftCache-Api-Key`, **not** `RIFTCACHE_API_KEY` — that name is only the
server-side env var above. Sending `RIFTCACHE_API_KEY` as a header (an easy mix-up in Postman)
gets a `401`, since the server never sees a recognized `X-RiftCache-Api-Key` header at all.

---

## Architecture at a Glance

```
Application Code (IDistributedCache — no vendor-specific code)
        │
RiftCache.Client (NuGet)
        │  HTTP/JSON, retry + circuit breaker
        ▼
RiftCache Service (container — runs anywhere)
   ├─ In-memory store (ConcurrentDictionary, TTL + LRU)
   ├─ ISecretProvider      → env vars, user secrets, mounted files | Azure Key Vault (today)
   │                         AWS Secrets Manager | GCP Secret Manager (planned)
   ├─ IPersistenceProvider → none / memory-only | Azure Blob (today)
   │                         S3 | GCS (planned)
   └─ OpenTelemetry        → traces + metrics, exported via OTLP when
                              OTEL_EXPORTER_OTLP_ENDPOINT is set — silent otherwise
```

The service core has no direct dependency on any single cloud's SDK. Cloud-specific secret and
persistence integrations are designed to live behind provider interfaces — see
[ARCHITECTURE.md](ARCHITECTURE.md) for the interface definitions and how to implement a new
provider. Observability goes through OpenTelemetry directly, no custom abstraction needed — see
[ROADMAP.md](ROADMAP.md) for what's implemented today versus planned.

---

## Documentation

| Doc | Purpose |
|---|---|
| [ARCHITECTURE.md](ARCHITECTURE.md) | Provider abstraction pattern, deployment layout, multi-tenancy design |
| [docs/providers/azure.md](docs/providers/azure.md) | Configuring `AzureKeyVaultSecretProvider` and `AzureBlobPersistenceProvider` |
| [deployment/azure/bicep](deployment/azure/bicep) | Azure Container Apps reference deployment (Bicep) |
| [ENTERPRISE_DEPLOYMENT.md](ENTERPRISE_DEPLOYMENT.md) | Running RiftCache as shared, centrally-owned infrastructure for multiple teams |
| [ROADMAP.md](ROADMAP.md) | Phased plan — core, Azure reference, AWS/GCP providers, enterprise features |
| [CONTRIBUTING.md](CONTRIBUTING.md) | How to add a cloud provider, submit PRs, project conventions |

---

## Status

Early-stage, actively developed. API and provider interfaces may change before v1.0. Feedback and contributions welcome via GitHub issues and PRs.

## License

MIT