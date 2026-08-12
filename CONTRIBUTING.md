# Contributing to RiftCache

Thanks for considering a contribution — RiftCache is built to grow beyond a single maintainer and a single cloud. This guide covers the most common contribution: **adding support for a new cloud provider**.

## Adding a Cloud Provider

RiftCache's core has no hard dependency on any cloud SDK. All cloud integration happens through three interfaces, implemented in separate, optional packages:

| Interface | Purpose | Example implementations |
|---|---|---|
| `ISecretProvider` | Read API keys / secrets | `EnvironmentSecretProvider` (default), `AzureKeyVaultSecretProvider` |
| `IPersistenceProvider` | Durable storage for cache snapshots | `NullPersistenceProvider` (default), `AzureBlobPersistenceProvider` |
| OpenTelemetry exporter | Metrics/tracing/logs | Standard OTel exporters — no custom interface needed |

### Steps to add, e.g., an AWS provider

1. Create `RiftCache.Providers.Aws` as its own project under `src/`.
2. Implement `ISecretProvider` against AWS Secrets Manager and `IPersistenceProvider` against S3.
3. Add a reference deployment under `deployment/aws/terraform/` (Fargate or ECS, matching the shape of `deployment/azure/bicep/`).
4. Add integration tests under `tests/RiftCache.Providers.Aws.Tests/` — these should run against LocalStack or an equivalent local emulator, not real AWS resources, so CI can run them without cloud credentials.
5. Document configuration in `docs/providers/aws.md` following the structure of the Azure provider doc.
6. Do **not** add AWS SDK references to the core `RiftCache` project — provider packages stay isolated so users on other clouds don't pull in dependencies they don't need.

The same pattern applies for GCP (`RiftCache.Providers.Gcp`, Secret Manager, GCS, Cloud Run).

## General Guidelines

- Core service and client library changes: open an issue to discuss before large PRs — the provider interfaces are still stabilizing pre-v1.0.
- Bug fixes, docs improvements, tests: PRs welcome directly.
- Follow existing code style (see `.editorconfig`).
- All new provider packages ship under the same MIT license as the core project.

## Questions

Open a GitHub issue or discussion — design questions about provider boundaries are especially welcome given the project is still early.
