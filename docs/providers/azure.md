# Azure Provider

`RiftCache.Providers.Azure` implements RiftCache's cloud provider interfaces against Azure
services. This is the first provider package in the repo, so it's also the template
[CONTRIBUTING.md](../../CONTRIBUTING.md) points future AWS/GCP contributions at — same structure,
different SDKs.

| Interface | Implementation | Status |
|---|---|---|
| `ISecretProvider` | `AzureKeyVaultSecretProvider` | Available |
| `IPersistenceProvider` | `AzureBlobPersistenceProvider` | Not built yet — see [ROADMAP.md](../../ROADMAP.md) |

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
