# Roadmap

## Phase 1 — Core + Azure Reference (current focus)
- [x] Core service: in-memory store, TTL/expiration
- [x] `RiftCache.Client` NuGet package (`IDistributedCache` implementation)
- [x] Provider interfaces: `ISecretProvider`, `IPersistenceProvider`, OpenTelemetry integration
- [x] Default providers: `EnvironmentSecretProvider`, `NullPersistenceProvider` (self-host path)
- [x] Azure reference provider: Key Vault (`ISecretProvider`) — see [docs/providers/azure.md](docs/providers/azure.md)
- [x] Azure reference provider: Blob Storage (`IPersistenceProvider`) — see [docs/providers/azure.md](docs/providers/azure.md)
- [x] Azure Container Apps reference deployment (Bicep) + Docker Compose for local dev — see [deployment/azure/bicep](deployment/azure/bicep); memory-only (`NullPersistenceProvider`) in this template
- [ ] Wire `AzureBlobPersistenceProvider` into the Azure Container Apps reference deployment — needs `Program.cs` extracted into reusable `AddRiftCacheCore()`/`MapRiftCacheCore()` extensions first, plus a second composed entry point + Dockerfile that layers the Azure providers on top of the default
- [x] Single-tenant mode as default; multi-tenant mode as opt-in config
- [x] v0.1.0 release

## Phase 2 — Community Cloud Providers
- [ ] AWS provider package (Secrets Manager, S3) + Fargate/ECS reference deployment
- [ ] GCP provider package (Secret Manager, GCS) + Cloud Run reference deployment
- [ ] Provider contribution guide finalized (see CONTRIBUTING.md)
- [ ] v0.2.0 release

## Phase 3 — Enterprise Features (opt-in modules)
- [ ] Multi-tenancy hardening (per-tenant rate limiting, audit logging)
- [ ] SSO/RBAC for admin API
- [ ] Batch operations, request signing (HMAC)
- [ ] Metrics/stats endpoint
- [ ] v1.0.0 release

## Phase 4 — Ecosystem
- [ ] Non-.NET clients (Go, Node, Python) if community demand exists
- [ ] Helm chart for Kubernetes deployments
- [ ] Clustering / gossip-based replication (currently single-instance-per-replica, no cross-replica cache coherence)

---

Priorities may shift based on community feedback and contributions. Open an issue to propose or discuss roadmap changes.
