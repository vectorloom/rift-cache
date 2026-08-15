using 'main.bicep'

// Copy this file to main.local.bicepparam (already covered by .gitignore, so a filled-in copy
// with real values never gets committed by accident) and fill in the values below before
// deploying. See README.md in this directory for the full walkthrough.

param namePrefix = 'riftcache'

// Build and push your own image from deployment/docker/Dockerfile first -- nothing is published
// anywhere yet. e.g.: 'myregistry.azurecr.io/riftcache:latest'
param containerImage = ''

// The value callers must send back via the X-RiftCache-Api-Key header. Pick something real --
// this becomes a Key Vault secret, not a plaintext app setting, but the value you put here is
// still visible in your shell history / this file, so don't reuse a key from anywhere else.
param riftCacheApiKey = ''

param minReplicas = 1
param maxReplicas = 3

// Set to true to provision Blob Storage and back the cache with AzureBlobPersistenceProvider
// instead of memory-only NullPersistenceProvider. If true, containerImage above must point at an
// image built from deployment/docker/Dockerfile.azure, not the core Dockerfile.
param enableBlobPersistence = false
