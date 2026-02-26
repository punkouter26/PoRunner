// ─────────────────────────────────────────────────────────────────────────────
// PoRunner — Subscription-scope infrastructure entry point
//
// Resources created:
//   PoRunner RG     → new resource group for all PoRunner-specific resources
//   porunner module → Storage Account + App Service (referencing PoShared plan)
//   kv-secrets mod  → Key Vault secret + RBAC in existing kv-poshared (PoShared)
//
// Shared resources consumed from PoShared:
//   asp-poshared-linux  – App Service Plan (F1 Linux, westus2)
//   kv-poshared         – Key Vault (eastus) for secrets
//   poappideinsights8f9c9a4e – Application Insights (eastus2)
// ─────────────────────────────────────────────────────────────────────────────
targetScope = 'subscription'

@description('Azure region for all PoRunner resources. Must match asp-poshared-linux (westus2).')
param location string = 'westus2'

@description('Globally unique App Service name. Change if wa-porunner is already taken.')
param webAppName string = 'wa-porunner'

@description('Application Insights connection string (from PoShared).')
@secure()
param appInsightsConnectionString string

// ── Resource Group ────────────────────────────────────────────────────────────
resource poRunnerRg 'Microsoft.Resources/resourceGroups@2024-03-01' = {
  name: 'PoRunner'
  location: location
  tags: {
    project: 'PoRunner'
    env: 'production'
    managedBy: 'bicep'
  }
}

// ── PoRunner resources (Storage + App Service) ────────────────────────────────
module poRunnerResources './modules/porunner.bicep' = {
  name: 'poRunnerResources'
  scope: poRunnerRg
  params: {
    location: location
    webAppName: webAppName
  }
}

// ── PoRunner Static Web App ───────────────────────────────────────────────────
module staticWebApp './modules/swa.bicep' = {
  name: 'staticWebApp'
  scope: poRunnerRg
  params: {
    location: location
    swaName: 'swa-porunner'
  }
}

// ── PoShared Key Vault: add secret + grant web app MI access ─────────────────
// kv-poshared lives in PoShared group (eastus) — deployed as a separate module scope
module kvSecrets './modules/kv-secrets.bicep' = {
  name: 'kvSecrets'
  scope: resourceGroup('PoShared')
  params: {
    appInsightsConnectionString: appInsightsConnectionString
    webAppPrincipalId: poRunnerResources.outputs.webAppPrincipalId
  }
}

// ── Outputs ──────────────────────────────────────────────────────────────────
output webAppUrl string = poRunnerResources.outputs.webAppUrl
output swaUrl string = 'https://${staticWebApp.outputs.swaDefaultHostname}'
output storageAccountName string = poRunnerResources.outputs.storageAccountName
output webAppPrincipalId string = poRunnerResources.outputs.webAppPrincipalId
