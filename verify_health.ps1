param(
    [string]$appServiceUrl,
    [string]$swaUrl
)

Write-Host "Verifying App Service Health: $appServiceUrl"
try {
    $resp = Invoke-WebRequest -Uri $appServiceUrl -UseBasicParsing -TimeoutSec 10
    if ($resp.StatusCode -eq 200) {
        Write-Host "✅ App Service is Healthy"
    } else {
        Write-Host "❌ App Service returned status: $($resp.StatusCode)"
    }
} catch {
    Write-Host "❌ App Service Health Check Failed: $_"
}

Write-Host "Verifying Static Web App Health: $swaUrl"
try {
    $resp = Invoke-WebRequest -Uri $swaUrl -UseBasicParsing -TimeoutSec 10
    if ($resp.StatusCode -eq 200) {
        Write-Host "✅ Static Web App is Healthy"
    } else {
        Write-Host "❌ Static Web App returned status: $($resp.StatusCode)"
    }
} catch {
    Write-Host "❌ Static Web App Health Check Failed: $_"
}
