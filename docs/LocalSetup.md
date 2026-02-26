# Local Setup & Day 1 Guide

## Prerequisites

### **Required Software**
- **Node.js 18+** — [https://nodejs.org/](https://nodejs.org/)
- **.NET 10 SDK** — [https://dotnet.microsoft.com/download](https://dotnet.microsoft.com/download)
- **Docker** (optional) — [https://www.docker.com/](https://www.docker.com/)
- **Git** — [https://git-scm.com/](https://git-scm.com/)

### **Verify Installation**
```bash
node --version        # v18.x or higher
dotnet --version      # 10.0.x
docker --version      # 24.0.x (optional)
git --version         # 2.40.x
```

---

## Quick Start (5 minutes)

### **Step 1: Clone Repository**
```bash
git clone https://github.com/YourOrg/BananaGame.git
cd BananaGame
```

### **Step 2: Start Backend**
```bash
cd server
dotnet restore
dotnet run
# Listens on http://localhost:5000 (HTTP) and https://localhost:5001 (HTTPS)
```

### **Step 3: Start Frontend (new terminal)**
```bash
cd client
npm install
npm run dev
# Vite dev server on http://localhost:5173
```

### **Step 4: Open in Browser**
Navigate to **http://localhost:5173** and play!

---

## Detailed Setup

### **Backend Setup**

#### **1. Restore Dependencies**
```bash
cd server
dotnet restore
```

#### **2. Configure Local Secrets**
Create `appsettings.Development.json` in the `server/` folder (NOT committed to Git):
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Debug",
      "Microsoft": "Warning"
    }
  },
  "AllowedHosts": "*",
  "ApplicationInsights": {
    "InstrumentationKey": "" // Empty for local dev (no telemetry)
  }
}
```

#### **3. Run Database Migrations** (if applicable)
```bash
dotnet ef migrations add InitialCreate
dotnet ef database update
```

#### **4. Start Development Server**
```bash
dotnet run
```

**Expected output:**
```
info: Microsoft.AspNetCore.Hosting.Diagnostics[1]
      Request starting HTTP/1.1 POST http://localhost:5000/gamehub
info: PoBananaGame.Features.GameSession.GameHub[0]
      [Socket] User connected: abc123def
```

### **Frontend Setup**

#### **1. Install Dependencies**
```bash
cd client
npm install
```

#### **2. Configure Vite Dev Server**
Edit `vite.config.js`:
```javascript
export default {
  server: {
    port: 5173,
    proxy: {
      '/gamehub': {
        target: 'http://localhost:5000',
        ws: true // WebSocket support for SignalR
      }
    }
  }
}
```

#### **3. Start Dev Server**
```bash
npm run dev
```

**Expected output:**
```
  VITE v4.4.0  ready in 123 ms

  ➜  Local:   http://localhost:5173/
  ➜  press h to show help
```

#### **4. Build for Production**
```bash
npm run build   # Outputs to dist/
npm run preview # Test production build locally
```

---

## Docker Compose (Optional Isolation)

### **Why Use Docker?**
- Avoid conflicts with system Node/dotnet versions
- Isolated databases and services
- Easy teardown and cleanup
- Mimics production environment

### **docker-compose.yml**
```yaml
version: '3.8'

services:
  backend:
    build:
      context: ./server
      dockerfile: Dockerfile
    ports:
      - "5000:5000"
      - "5001:5001"
    environment:
      ASPNETCORE_ENVIRONMENT: Development
      ASPNETCORE_URLS: "http://+:5000;https://+:5001"
    volumes:
      - ./server:/app
    networks:
      - banana-net

  frontend:
    build:
      context: ./client
      dockerfile: Dockerfile
    ports:
      - "5173:5173"
    environment:
      VITE_API_URL: "http://localhost:5000"
    volumes:
      - ./client:/app
    depends_on:
      - backend
    networks:
      - banana-net

networks:
  banana-net:
    driver: bridge
```

### **Run with Docker Compose**
```bash
docker-compose up

# In another terminal, stop services
docker-compose down

# View logs
docker-compose logs -f backend
docker-compose logs -f frontend
```

---

## Development Workflow

### **1. Create a Feature Branch**
```bash
git checkout -b feature/my-awesome-feature
```

### **2. Make Changes**
Edit code in `client/` or `server/` folders. Vite and dotnet watch will auto-reload.

### **3. Run Tests**
```bash
# Backend
cd server
dotnet test

# Frontend
cd client
npm run test
```

### **4. Commit & Push**
```bash
git add .
git commit -m "feat: add player color selection"
git push origin feature/my-awesome-feature
```

### **5. Create Pull Request**
Push to GitHub and open a PR. CI will run tests and lint checks.

---

## Troubleshooting

### **Issue: "Failed to connect to server"**
- **Cause:** Backend not running or CORS misconfigured
- **Fix:**
  ```bash
  # Check backend is running on port 5000
  netstat -an | grep 5000
  
  # Restart backend
  cd server && dotnet run
  ```

### **Issue: "SignalR connection refused"**
- **Cause:** WebSocket proxy not configured
- **Fix:** Verify `vite.config.js` includes:
  ```javascript
  proxy: { '/gamehub': { ws: true } }
  ```

### **Issue: "Canvas not rendering"**
- **Cause:** Sprite assets missing or incorrect path
- **Fix:**
  ```bash
  cd client
  ls public/man_dressed_in_banana_suit/rotations/
  # Should show: east.png, north.png, south.png, west.png
  ```

### **Issue: "Port already in use"**
- **Cause:** Another process using port 5000 or 5173
- **Fix:**
  ```bash
  # Windows
  netstat -ano | findstr :5000
  taskkill /PID <PID> /F
  
  # macOS/Linux
  lsof -i :5000
  kill -9 <PID>
  ```

### **Issue: "npm modules not found"**
- **Cause:** Stale node_modules
- **Fix:**
  ```bash
  cd client
  rm -rf node_modules package-lock.json
  npm install
  ```

---

## Debugging Tips

### **Frontend Debugging (Browser DevTools)**
1. Open Chrome/Edge DevTools (`F12`)
2. **Console tab:** Check for JavaScript errors
3. **Network tab:** View HTTP and WebSocket traffic
4. **Application tab:** Inspect LocalStorage/SessionStorage
5. **Performance tab:** Profile frame rate during gameplay

### **Backend Debugging (Visual Studio Code)**
Create `.vscode/launch.json`:
```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": ".NET Launch",
      "type": "coreclr",
      "request": "launch",
      "program": "${workspaceFolder}/server/bin/Debug/net10.0/Server.dll",
      "args": [],
      "cwd": "${workspaceFolder}/server",
      "preLaunchTask": "build"
    }
  ]
}
```

Then press `F5` to start with breakpoints.

### **SignalR Diagnostic Logging**
Enable verbose logging:
```csharp
// In main.js
const connection = new signalR.HubConnectionBuilder()
    .withUrl("/gamehub")
    .configureLogging(signalR.LogLevel.Debug) // ← Change to Debug
    .build();
```

---

## Performance Profiling

### **Measure Frame Rate**
```javascript
// In main.js
let lastTime = performance.now();
let frameCount = 0;

function gameLoop() {
    const now = performance.now();
    const deltaMs = now - lastTime;
    frameCount++;
    
    if (frameCount % 60 === 0) {
        console.log(`FPS: ${(1000 / deltaMs).toFixed(1)}`);
    }
    
    lastTime = now;
    requestAnimationFrame(gameLoop);
}
```

### **Profile Backend Latency**
```csharp
var sw = Stopwatch.StartNew();
// ... game logic ...
sw.Stop();
Console.WriteLine($"[Perf] ServerTick took {sw.ElapsedMilliseconds}ms");
```

---

## Next Steps

1. **Read Architecture:** See [Architecture.mmd](Architecture.mmd)
2. **Explore Code:** Start with `client/main.js` and `server/Program.cs`
3. **Run Tests:** `npm test` and `dotnet test`
4. **Check PR Template:** `.github/pull_request_template.md`
5. **Join Slack:** #banana-game-dev channel for questions

---

## Useful Links

| Resource | URL |
|----------|-----|
| **GitHub Repo** | https://github.com/YourOrg/BananaGame |
| **Issue Tracker** | https://github.com/YourOrg/BananaGame/issues |
| **Project Board** | https://github.com/orgs/YourOrg/projects |
| **SignalR Docs** | https://learn.microsoft.com/en-us/aspnet/core/signalr/ |
| **Vite Docs** | https://vitejs.dev/ |
| **.NET Docs** | https://learn.microsoft.com/en-us/dotnet/ |
| **Mermaid Diagrams** | https://mermaid.js.org/ |
