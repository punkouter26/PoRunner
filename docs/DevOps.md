# DevOps & Deployment Pipeline

## Overview
Banana Game uses **GitOps CI/CD** on Azure with separate environments for **Development**, **Staging**, and **Production**.

---

## Architecture

### **Azure Services Used**
- **App Service** — Hosts .NET backend
- **SignalR Service** — Managed WebSocket relay (scales to 1000s of concurrent connections)
- **Blob Storage** — Character sprites and animations (CDN origin)
- **Azure CDN** — Global sprite delivery
- **Application Insights** — Telemetry, logging, performance monitoring
- **KeyVault** — Secrets management (connection strings, API keys)

### **Local Development**
- **.NET 10 SDK** — Backend
- **Vite dev server** — Frontend (http://localhost:5173)
- **Docker Compose** — Optional container stack for isolation

---

## Environment Configuration

### **Development (localhost)**
```
ASPNETCORE_ENVIRONMENT=Development
SignalR URLs: http://localhost:5173, http://127.0.0.1:5173
CORS: AllowVite policy
Session persistence: In-memory
Azure Services: None (local fallback)
```

### **Staging (Azure)**
```
ASPNETCORE_ENVIRONMENT=Staging
SignalR URLs: https://staging.api.bananagame.dev
AppService Tier: Standard S1 (burst-capable)
SignalR Tier: Standard (1 unit, ~100 concurrent)
Session persistence: In-memory (restart clears state — OK for testing)
Monitoring: Full Application Insights telemetry
```

### **Production (Azure)**
```
ASPNETCORE_ENVIRONMENT=Production
SignalR URLs: https://api.bananagame.dev
AppService Tier: Premium P2V3 (auto-scale 2-10 instances)
SignalR Tier: Premium (5 units, 10,000+ concurrent)
Session persistence: Distributed (future: Redis/ServiceBus)
Monitoring: Full Application Insights + custom alerts
Backup: Geo-redundant storage blobs
CDN: Premium tier with DDoS protection
```

---

## CI/CD Pipeline

### **GitHub Actions Workflow**
```yaml
name: Deploy to Azure
on:
  push:
    branches: [main, staging]
  pull_request:
    branches: [main]

jobs:
  build:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v3
      
      # Build backend
      - uses: actions/setup-dotnet@v3
        with:
          dotnet-version: '10.0.x'
      - run: dotnet build Server.csproj
      - run: dotnet test --no-build
      
      # Build frontend
      - uses: actions/setup-node@v3
        with:
          node-version: 18
      - run: cd client && npm install && npm run build
      
      # Package
      - run: dotnet publish -c Release -o publish
      
      # Deploy
      - uses: azure/login@v1
        with:
          creds: ${{ secrets.AZURE_CREDENTIALS }}
      
      - uses: azure/webapps-deploy@v2
        with:
          app-name: BananaGame-${{ github.ref_name }}
          package: publish
```

### **Branch Strategy**
| Branch | Deploys To | Trigger |
|--------|-----------|---------|
| `main` | Production | Merge to main (manual approval gate) |
| `staging` | Staging | Push to staging (auto-deploy) |
| `feature/*` | None | PR to main (runs tests only) |

---

## Secrets & Configuration

### **Azure KeyVault**
All secrets stored in KeyVault and injected via **Managed Identity** on App Service.

| Secret | Value | Rotation |
|--------|-------|----------|
| `SignalR--ConnectionString` | Full SignalR Service connection string | 90 days |
| `Storage--ConnectionString` | Blob storage connection (read-only key) | 90 days |
| `AppInsights--InstrumentationKey` | Application Insights key | 365 days |
| `CORS--AllowedOrigins` | Semicolon-separated list of allowed domains | As needed |

### **Environment Variables**
```bash
# appsettings.json (committed)
{
  "Logging": {
    "LogLevel": { "Default": "Information" }
  },
  "AllowedHosts": "*"
}

# appsettings.Development.json (local override, NOT committed)
{
  "Logging": {
    "LogLevel": { "Default": "Debug" }
  }
}

# appsettings.Production.json (deployed via CI/CD secrets)
{
  "Logging": {
    "LogLevel": { "Default": "Warning" }
  },
  "ApplicationInsights": {
    "InstrumentationKey": "$$APPINSIGHTS_KEY$$" // Injected by pipeline
  }
}
```

---

## Health Checks & Monitoring

### **Application Insights Alerts**
- **Error Rate >1%** → Page author + Slack
- **P95 Latency >200ms** → Engineering team
- **Availability <99%** → Escalate to SRE
- **SignalR connection drop >5%** → Investigation

### **Custom Metrics**
```csharp
// In GameHub/GameSessionManager
using Microsoft.ApplicationInsights;
using Microsoft.ApplicationInsights.DataContracts;

private TelemetryClient _telemetry;

_telemetry.TrackEvent("PlayerConnected", new() { 
    { "roomId", room.RoomId }, 
    { "totalSessions", activeRooms.Count } 
});

_telemetry.TrackDependency("SignalRBroadcast", "SendAsync", stopwatch, success);
```

### **Custom Dashboard**
- **Active Rooms** (gauge)
- **Players Online** (gauge)
- **Avg Game Duration** (chart)
- **Win Rate by Color** (table)
- **Error Rate** (time series)

---

## Rollback Strategy

### **Canary Deployment**
1. Deploy to 1 instance (out of 10)
2. Monitor metrics for 15 minutes
3. If error rate < 0.5%, gradually roll out to 100%
4. If error rate > 0.5%, rollback immediately

### **Manual Rollback**
```bash
az webapp deployment slot swap \
  --resource-group BananaGame-RG \
  --name BananaGame-Production \
  --slot staging
```

---

## Disaster Recovery

| Scenario | RTO | RPO | Procedure |
|----------|-----|-----|-----------|
| **App Service down** | 5 min | 0 | Failover to secondary region (manual) |
| **SignalR Service down** | 10 min | 0 | Switch to Long Polling fallback |
| **Blob Storage down** | 30 min | 0 | Use geo-redundant replica storage |
| **Data corruption** | 1 hour | 24 hr | Restore from daily backup |

---

## Performance Tuning

### **Backend (.NET)**
- **Connection pooling:** 100 max connections per SignalR instance
- **Message batch size:** Group broadcasts every 50ms instead of per-player
- **Compression:** Enable gzip on payloads >1KB

### **Frontend (Vite)**
- **Code splitting:** Lazy-load audio engine
- **Asset caching:** Service Worker caches sprites for offline preview
- **Network:** WebSocket stays open, auto-reconnect with exponential backoff

### **Database Query Optimization** (future)
- Index on `roomId, createdAtMs` for cleanup jobs
- Partition game sessions by date for efficient archival

---

## Deployment Checklist

Before each production deployment:
- [ ] All tests pass (unit + integration)
- [ ] Code review approved
- [ ] No breaking API changes
- [ ] Migration scripts tested in staging
- [ ] Rollback plan documented
- [ ] Stakeholders notified (Slack)
- [ ] Monitor dashboard ready
- [ ] Runbook linked in change ticket
