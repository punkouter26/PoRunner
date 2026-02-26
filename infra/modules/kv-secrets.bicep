// ─────────────────────────────────────────────────────────────────────────────
// kv-secrets module — deployed into the PoShared resource group
//
// Operates on the existing kv-poshared Key Vault:
//   1. Writes PoRunner-specific secrets (App Insights connection string)
//   2. Grants the PoRunner web app's MI "Key Vault Secrets User" on the vault
//      so that the App Service Key Vault reference syntax resolves at runtime
// ─────────────────────────────────────────────────────────────────────────────

@description('App Insights connection string to store as a secret.')
@secure()
param appInsightsConnectionString string

@description('System-assigned Managed Identity principal ID of the PoRunner App Service.')
param webAppPrincipalId string

// ── Reference existing Key Vault ─────────────────────────────────────────────
resource keyVault 'Microsoft.KeyVault/vaults@2023-07-01' existing = {
  name: 'kv-poshared'
}

// ── Secret: Application Insights connection string ───────────────────────────
resource appInsightsSecret 'Microsoft.KeyVault/vaults/secrets@2023-07-01' = {
  parent: keyVault
  name: 'PoRunner-AppInsights-ConnectionString'
  properties: {
    value: appInsightsConnectionString
    attributes: {
      enabled: true
    }
    contentType: 'text/plain'
  }
}

// ── RBAC: Key Vault Secrets User ─────────────────────────────────────────────
// Grants the web app's MI permission to READ secrets — enables KV references in app settings
// Role ID: Key Vault Secrets User = 4633458b-17de-408a-b874-0445c86b69e6
var kvSecretsUserRoleId = '4633458b-17de-408a-b874-0445c86b69e6'

resource kvRoleAssignment 'Microsoft.Authorization/roleAssignments@2022-04-01' = {
  name: guid(keyVault.id, webAppPrincipalId, kvSecretsUserRoleId)
  scope: keyVault
  properties: {
    roleDefinitionId: subscriptionResourceId(
      'Microsoft.Authorization/roleDefinitions',
      kvSecretsUserRoleId
    )
    principalId: webAppPrincipalId
    principalType: 'ServicePrincipal'
  }
}
