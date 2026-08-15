# Azure Provider

`RiftCache.Providers.Azure` implements RiftCache's cloud provider interfaces against Azure
services. This is the first provider package in the repo, so it's also the template
[CONTRIBUTING.md](../../CONTRIBUTING.md) points future AWS/GCP contributions at — same structure,
different SDKs.

| Interface | Implementation | Status |
|---|---|---|
| `ISecretProvider` | `AzureKeyVaultSecretProvider` | Available |
| `IPersistenceProvider` | `AzureBlobPersistenceProvider` | Available |

Not published anywhere yet (not on nuget.org, not on GitHub Packages) — build from source for now:
reference `src/RiftCache.Providers.Azure/RiftCache.Providers.Azure.csproj` directly, or `dotnet pack`
it yourself.

## AzureKeyVaultSecretProvider

### Prerequisites

- An Azure Key Vault.
- A way to authenticate: [Managed Identity](https://learn.microsoft.com/azure/active-directory/managed-identities-azure-resources/overview)
  is the recommended path for anything running in Azure (Container Apps, App Service, VMs); for
  local development, being logged in via the Azure CLI (`az login`) or a service principal's
  environment variables both work too — `AddAzureKeyVaultSecretProvider` uses
  `Azure.Identity`'s `DefaultAzureCredential` by default, which tries these (and a few other
  standard sources) in order automatically. No RiftCache-specific credential configuration exists
  or is needed.
- The identity needs the **Key Vault Secrets User** role (or equivalent) on the vault — read access
  only, RiftCache never writes secrets.

### Usage

```csharp
using RiftCache.Providers.Azure;

services.AddAzureKeyVaultSecretProvider(new Uri("https://your-vault-name.vault.azure.net/"));

// Or with an explicit credential instead of the DefaultAzureCredential fallback chain:
services.AddAzureKeyVaultSecretProvider(
    new Uri("https://your-vault-name.vault.azure.net/"),
    new ClientSecretCredential(tenantId, clientId, clientSecret));
```

This registers `ISecretProvider` as `AzureKeyVaultSecretProvider`, replacing the default
`EnvironmentSecretProvider`. It does **not** modify `src/RiftCache/Program.cs` — the core service
never references Azure SDK types (see [ARCHITECTURE.md](../../ARCHITECTURE.md)), so swapping this
in means composing your own entry point that references both `RiftCache` and
`RiftCache.Providers.Azure` and calls this instead of (or alongside) the default registration.

### Secret naming

This is the one place `AzureKeyVaultSecretProvider`'s behavior isn't a drop-in match for
`EnvironmentSecretProvider`. Key Vault secret names only allow letters, digits, and hyphens — no
underscores — but every key RiftCache looks up follows `EnvironmentSecretProvider`'s
underscore convention. `AzureKeyVaultSecretProvider` translates automatically:

| RiftCache looks up | Key Vault secret name |
|---|---|
| `RIFTCACHE_API_KEY` | `RIFTCACHE-API-KEY` |
| `RIFTCACHE_API_KEY__TEAM-A` (multi-tenant) | `RIFTCACHE-API-KEY--TEAM-A` |

`_` becomes `-`, and `__` (the multi-tenant nesting marker — see `ApiKeyAuthFilter`) becomes `--`
so it stays visually distinct. Name your Key Vault secrets accordingly.

### Error handling

A missing secret resolves to `null`, same contract as every other `ISecretProvider`. Any other
failure — wrong permissions, network issues, vault not found — propagates as the underlying
`Azure.RequestFailedException` rather than being swallowed; RiftCache's auth layer treats that the
same as a missing/invalid key (request rejected), but the real exception is still visible in logs
for diagnosing *why*.

## AzureBlobPersistenceProvider

### Prerequisites

- An Azure Storage account with a **blob container already created** — `AzureBlobPersistenceProvider`
  reads and writes blobs but never provisions the container itself (same stance as
  `AzureKeyVaultSecretProvider` not creating the vault). Provisioning is the Bicep reference
  deployment's job.
- The identity needs the **Storage Blob Data Contributor** role (or equivalent) on the container —
  it both reads and writes, unlike the read-only Key Vault provider.
- Same `DefaultAzureCredential` story as Key Vault: Managed Identity in Azure, `az login` or a
  service principal locally, no RiftCache-specific credential config.

### Usage

```csharp
using RiftCache.Providers.Azure;

services.AddAzureBlobPersistenceProvider(
    new Uri("https://your-account.blob.core.windows.net/your-container"));

// Or with an explicit credential:
services.AddAzureBlobPersistenceProvider(
    new Uri("https://your-account.blob.core.windows.net/your-container"),
    new ClientSecretCredential(tenantId, clientId, clientSecret));
```

Registers `IPersistenceProvider` as `AzureBlobPersistenceProvider`, replacing the default
`NullPersistenceProvider`. Same composition note as Key Vault: this doesn't touch
`src/RiftCache/Program.cs`, since the core service can't reference Azure SDK types at all.

### How entries are stored

One blob per cache key (the key itself, URL-encoded, as the blob name — the same encoding already
used for cache keys in the REST API and client). The blob's **content is the cached value's raw
bytes directly** — inspectable, no envelope format. `AbsoluteExpiration` and `SlidingExpiration`
are stored as blob **metadata** (`absoluteExpiration`, `slidingExpiration`) rather than embedded in
the content, since Blob Storage supports small string key/value metadata on every blob natively.

### Error handling

Same shape as `AzureKeyVaultSecretProvider`: a missing blob resolves to `null` from `LoadAsync`;
`RemoveAsync` is a no-op on an already-missing blob (`DeleteIfExistsAsync`); any other failure
propagates as `Azure.RequestFailedException` rather than being swallowed.
