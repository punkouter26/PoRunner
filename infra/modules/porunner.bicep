// ─────────────────────────────────────────────────────────────────────────────
// PoRunner resources module — deployed into the PoRunner resource group
//
// Creates:
//   stporunner{unique}   – Storage Account (Standard LRS, Table Storage enabled)
//   wa-porunner          – App Service (Linux, .NET 10, uses shared plan in PoShared)
//   Role assignment      – Storage Table Data Contributor → web app MI on storage
// ─────────────────────────────────────────────────────────────────────────────

@description('Location. Must match asp-poshared-linux location (westus2).')
param location string = 'westus2'

@description('App Service name — must be globally unique across Azure.')
param webAppName string = 'wa-porunner'

// Storage account names: lowercase, 3-24 chars, globally unique
var uniqueSuffix = substring(uniqueString(resourceGroup().id), 0, 8)
var storageAccountName = 'stporunner${uniqueSuffix}'

// ── Storage Account ───────────────────────────────────────────────────────────
resource storageAccount 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: storageAccountName
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: {
    accessTier: 'Hot'
    allowBlobPublicAccess: false
    minimumTlsVersion: 'TLS1_2'
    supportsHttpsTrafficOnly: true
    // Disable shared-key (account-key) access — enforce Managed Identity / RBAC only
    allowSharedKeyAccess: false
  }
  tags: {
    project: 'PoRunner'
    purpose: 'highscores'
  }
}

// Table service declaration (required to enable Table storage on the account)
resource tableService 'Microsoft.Storage/storageAccounts/tableServices@2023-05-01' = {
  parent: storageAccount
  name: 'default'
}

// ── App Service ───────────────────────────────────────────────────────────────
resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: webAppName
  location: location
  identity: {
    // System-assigned MI — used for Key Vault references and Storage RBAC
    type: 'SystemAssigned'
  }
  properties: {
    // Cross-RG reference to the existing shared Linux App Service Plan in PoShared
    serverFarmId: resourceId('PoShared', 'Microsoft.Web/serverfarms', 'asp-poshared-linux')
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      webSocketsEnabled: true  // Required for SignalR
      alwaysOn: false           // Not available on Free (F1) tier
      appSettings: [
        {
          // Key Vault reference — resolved at runtime using the web app's MI
          // The MI must have Key Vault Secrets User on kv-poshared (granted in kv-secrets module)
          name: 'APPLICATIONINSIGHTS_CONNECTION_STRING'
          value: '@Microsoft.KeyVault(VaultName=kv-poshared;SecretName=PoRunner-AppInsights-ConnectionString)'
        }
        {
          // Table endpoint URL is not a secret — stored directly in app settings
          // ASP.NET Core maps AzureStorage__TableEndpoint → AzureStorage:TableEndpoint
          // Use environment().suffixes.storage to avoid hardcoded cloud URLs
          name: 'AzureStorage__TableEndpoint'
          value: 'https://${storageAccountName}.table.${environment().suffixes.storage}'
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
        {
          // Tell SignalR to allow KV reference resolution before hub connections
          name: 'WEBSITE_SKIP_ASSET_SYNC'
          value: 'false'
        }
      ]
    }
  }
  tags: {
    project: 'PoRunner'
  }
}

// ── RBAC: Storage Table Data Contributor ─────────────────────────────────────
// Grants the web app's Managed Identity read/write access to Table Storage
// Role ID: Storage Table Data Contributor = 0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3
var storageTableDataContributorRoleId = '0a9a7e1f-b9d0-4cc4-a60d-0319b160aaa3'

resource storageRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  // Deterministic GUID scoped to this storage + web app + role
  name: guid(storageAccount.id, webApp.id, storageTableDataContributorRoleId)
  scope: storageAccount
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      storageTableDataContributorRoleId
    )
    principalId: webApp.identity.principalId
    principalType: 'ServicePrincipal'
  }
}

// ── Outputs ──────────────────────────────────────────────────────────────────
output storageAccountName string = storageAccountName
output storageTableEndpoint string = 'https://${storageAccountName}.table.${environment().suffixes.storage}'
output webAppPrincipalId string = webApp.identity.principalId
output webAppName string = webAppName
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'
