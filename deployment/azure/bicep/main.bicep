// Azure Container Apps reference deployment for RiftCache.
//
// Deploys the core RiftCache image (Azure-SDK-free, no code changes) behind Azure Container
// Apps. Key Vault backs RIFTCACHE_API_KEY via Container Apps' native Key-Vault-backed secret
// injection -- the app itself never needs AzureKeyVaultSecretProvider code for that, since the
// platform resolves the secret using the app's managed identity before the container starts.
//
// Persistence is memory-only by default (NullPersistenceProvider, the core service's default).
// Set enableBlobPersistence=true to additionally provision Blob Storage and wire
// AzureBlobPersistenceProvider in via RIFTCACHE_BLOB_CONTAINER_URL -- see the parameter
// description below. That requires deploying the src/RiftCache.Azure image (built from
// deployment/docker/Dockerfile.azure) instead of the core image, since only that image
// references the Azure persistence provider.
//
// Deploy with (see README.md in this directory for the full walkthrough):
//   az deployment group create --resource-group <rg> --template-file main.bicep --parameters main.bicepparam

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Name prefix applied to all resources.')
param namePrefix string = 'riftcache'

@description('Container image to deploy, e.g. myregistry.azurecr.io/riftcache:latest. No image is published anywhere yet -- build and push your own from deployment/docker/Dockerfile (or Dockerfile.azure when enableBlobPersistence is true).')
param containerImage string

@description('The API key clients must send via the X-RiftCache-Api-Key header. Stored in Key Vault, never in a plain app setting.')
@secure()
param riftCacheApiKey string

@description('Minimum number of replicas. A cache with zero warm replicas defeats its own purpose, so this defaults to 1 rather than allowing scale-to-zero.')
param minReplicas int = 1

@description('Maximum number of replicas.')
param maxReplicas int = 3

@description('Provision Blob Storage and wire RIFTCACHE_BLOB_CONTAINER_URL into the container app, so AzureBlobPersistenceProvider backs the cache instead of memory-only NullPersistenceProvider. Requires containerImage to point at an image built from deployment/docker/Dockerfile.azure, not the core Dockerfile -- only that image references the Azure persistence provider.')
param enableBlobPersistence bool = false

var logAnalyticsName = '${namePrefix}-logs'
var containerAppsEnvironmentName = '${namePrefix}-env'
var identityName = '${namePrefix}-identity'
var keyVaultName = '${namePrefix}-kv-${uniqueString(resourceGroup().id)}'
var containerAppName = '${namePrefix}-app'
var apiKeySecretName = 'RIFTCACHE-API-KEY' // Key Vault secret names don't allow underscores.
var keyVaultSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'
var storageAccountName = toLower(replace('${namePrefix}st${uniqueString(resourceGroup().id)}', '-', ''))
var blobContainerName = 'riftcache-entries'
var storageBlobDataContributorRoleId = 'ba92f5b4-2d11-453d-a403-e96b0029c9fe'

resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: logAnalyticsName
  location: location
  properties: {
    sku: {
      name: 'PerGB2018'
    }
    retentionInDays: 30
  }
}

resource containerAppsEnvironment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: containerAppsEnvironmentName
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

resource identity 'Microsoft.ManagedIdentity/userAssignedIdentities@2023-01-31' = {
  name: identityName
  location: location
}

resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: keyVaultName
  location: location
  properties: {
    sku: {
      family: 'A'
      name: 'standard'
    }
    tenantId: subscription().tenantId
    enableRbacAuthorization: true
  }
}

resource apiKeySecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: apiKeySecretName
  properties: {
    value: riftCacheApiKey
  }
}

resource keyVaultSecretsUserRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, identity.id, keyVaultSecretsUserRoleId)
  scope: keyVault
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', keyVaultSecretsUserRoleId)
  }
}

resource storageAccount 'Microsoft.Storage/storageAccounts@2023-01-01' = if (enableBlobPersistence) {
  name: storageAccountName
  location: location
  kind: 'StorageV2'
  sku: {
    name: 'Standard_LRS'
  }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-01-01' = if (enableBlobPersistence) {
  parent: storageAccount
  name: 'default'
}

resource blobContainer 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-01-01' = if (enableBlobPersistence) {
  parent: blobService
  name: blobContainerName
}

resource storageBlobDataContributorRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = if (enableBlobPersistence) {
  name: guid(storageAccount.id, identity.id, storageBlobDataContributorRoleId)
  scope: storageAccount
  properties: {
    principalId: identity.properties.principalId
    principalType: 'ServicePrincipal'
    roleDefinitionId: subscriptionResourceId('Microsoft.Authorization/roleDefinitions', storageBlobDataContributorRoleId)
  }
}

var baseEnv = [
  {
    name: 'RIFTCACHE_API_KEY'
    secretRef: 'riftcache-api-key'
  }
]

var blobEnv = enableBlobPersistence ? [
  {
    name: 'RIFTCACHE_BLOB_CONTAINER_URL'
    // storageAccount is only null when enableBlobPersistence is false, which is exactly the
    // branch this ternary never evaluates -- safe despite the static warning below.
    #disable-next-line BCP318
    value: '${storageAccount.properties.primaryEndpoints.blob}${blobContainerName}'
  }
] : []

resource containerApp 'Microsoft.App/containerApps@2024-03-01' = {
  name: containerAppName
  location: location
  identity: {
    type: 'UserAssigned'
    userAssignedIdentities: {
      '${identity.id}': {}
    }
  }
  properties: {
    managedEnvironmentId: containerAppsEnvironment.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
      }
      secrets: [
        {
          name: 'riftcache-api-key'
          keyVaultUrl: apiKeySecret.properties.secretUri
          identity: identity.id
        }
      ]
    }
    template: {
      containers: [
        {
          name: 'riftcache'
          image: containerImage
          resources: {
            cpu: json('0.25')
            memory: '0.5Gi'
          }
          env: concat(baseEnv, blobEnv)
        }
      ]
      scale: {
        minReplicas: minReplicas
        maxReplicas: maxReplicas
      }
    }
  }
  dependsOn: [
    keyVaultSecretsUserRoleAssignment
    storageBlobDataContributorRoleAssignment
  ]
}

@description('Public URL of the deployed RiftCache Container App.')
output containerAppFqdn string = containerApp.properties.configuration.ingress.fqdn

@description('Key Vault name, for adding/rotating RIFTCACHE_API_KEY or configuring AzureKeyVaultSecretProvider later.')
output keyVaultName string = keyVault.name
