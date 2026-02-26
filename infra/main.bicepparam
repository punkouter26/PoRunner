// ─────────────────────────────────────────────────────────────────────────────
// PoRunner Bicep parameters
// Subscription: Punkouter26 (bbb8dfbe-9169-432f-9b7a-fbf861b51037)
// ─────────────────────────────────────────────────────────────────────────────
using './main.bicep'

// Location matches the existing asp-poshared-linux App Service Plan
param location = 'westus2'

// App Service name — must be globally unique across all of Azure.
// If deployment fails with a name-conflict error, change this.
param webAppName = 'wa-porunner'

// Application Insights connection string from the shared poappideinsights8f9c9a4e component.
// This is written to kv-poshared as secret "PoRunner-AppInsights-ConnectionString".
param appInsightsConnectionString = 'InstrumentationKey=85672f16-e8e5-4f0f-882f-1ca7eff6b93f;IngestionEndpoint=https://eastus2-3.in.applicationinsights.azure.com/;LiveEndpoint=https://eastus2.livediagnostics.monitor.azure.com/;ApplicationId=274cd1bb-b5db-4e99-a352-ac2deb893eda'
