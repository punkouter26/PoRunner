// ─────────────────────────────────────────────────────────────────────────────
// SWA module — deployed into the PoRunner resource group
// ─────────────────────────────────────────────────────────────────────────────

@description('Location for the Static Web App. Global service, but metadata stored in a region.')
param location string = 'westus2'

@description('SWA name.')
param swaName string = 'swa-porunner'

resource staticWebApp 'Microsoft.Web/staticSites@2024-04-01' = {
  name: swaName
  location: location
  sku: {
    name: 'Free'
    tier: 'Free'
  }
  properties: {
    // SWA configuration can be added here
    provider: 'GitHub'
  }
}

output swaDefaultHostname string = staticWebApp.properties.defaultHostname
output swaApiKey string = listSecrets(staticWebApp.id, staticWebApp.apiVersion).properties.apiKey
