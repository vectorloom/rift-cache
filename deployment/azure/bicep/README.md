# Azure Container Apps Reference Deployment

Deploys the core RiftCache image behind Azure Container Apps — no code changes, the exact same
image `deployment/docker/Dockerfile` builds. Key Vault backs `RIFTCACHE_API_KEY` via Container
Apps' native Key-Vault-backed secret injection: the platform resolves the secret using the app's
managed identity before the container ever starts, so the app itself never needs
`AzureKeyVaultSecretProvider` code for this.

**Persistence is memory-only by default** (`NullPersistenceProvider`, the core service's default).
Set `enableBlobPersistence = true` in your `.bicepparam` file to additionally provision Blob
Storage and back the cache with `AzureBlobPersistenceProvider` instead — see
[docs/providers/azure.md](../../../docs/providers/azure.md) for what that provider does. Enabling
it requires deploying the `src/RiftCache.Azure` image (built from
`deployment/docker/Dockerfile.azure`) rather than the core image, since only that image references
the Azure persistence provider; the core image doesn't carry Azure SDK dependencies at all.

## What gets created

- Log Analytics Workspace (Container Apps Environment requires one for logging)
- Container Apps Environment (Consumption plan)
- A user-assigned Managed Identity
- Key Vault, with the identity granted **Key Vault Secrets User** (read-only)
- A Key Vault secret holding `RIFTCACHE_API_KEY`
- The Container App itself — external ingress on port 8080, 1–3 replicas by default
- **Only when `enableBlobPersistence = true`:** a Storage Account, a `riftcache-entries` Blob
  container, and a role assignment granting the identity **Storage Blob Data Contributor** on the
  storage account; the container app also gets `RIFTCACHE_BLOB_CONTAINER_URL` set to that
  container's URL

## Prerequisites

- An Azure subscription and a resource group to deploy into.
- The [Azure CLI](https://learn.microsoft.com/cli/azure/install-azure-cli), logged in
  (`az login`) with that subscription selected.
- **A container image already built and pushed somewhere you control.** Nothing is published
  anywhere yet (see the main [README](../../../README.md)), so this template takes the image
  reference as a parameter — build `deployment/docker/Dockerfile` (or `Dockerfile.azure` if you're
  setting `enableBlobPersistence = true`) and push it to your own registry (Azure Container
  Registry, Docker Hub, GHCR, etc.) first.

## Deploy

```bash
cp main.bicepparam main.local.bicepparam
# edit main.local.bicepparam: set containerImage and riftCacheApiKey at minimum

az deployment group create \
  --resource-group <your-resource-group> \
  --template-file main.bicep \
  --parameters main.local.bicepparam
```

## Verify

```bash
fqdn=$(az deployment group show \
  --resource-group <your-resource-group> \
  --name main \
  --query properties.outputs.containerAppFqdn.value -o tsv)

curl "https://$fqdn/healthz"
curl -H "X-RiftCache-Api-Key: <the key you set>" "https://$fqdn/api/v1/cache/hello"
```

## Cost

Consumption-plan Container Apps, Key Vault, and Log Analytics are all pay-per-use with no fixed
minimum, but this isn't free to leave running indefinitely — `minReplicas` defaults to `1` (a cache
with zero warm replicas defeats its own purpose, unlike a typical stateless web app where
scale-to-zero makes sense), so there's always at least one replica running. Delete the resource
group when you're done evaluating this template:

```bash
az group delete --name <your-resource-group>
```

## Local validation without deploying

```bash
az bicep build --file main.bicep
az bicep build-params --file main.bicepparam
```

Both check the templates compile and every resource type/API version/property is valid against
Azure's real schemas — no subscription or credentials needed, since this is pure local
compilation.
