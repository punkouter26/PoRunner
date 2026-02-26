# 🍌 Banana Game

A multiplayer **2-player competitive racing game** where players in a banana suit race to the finish line in real-time using WebSocket synchronization.

## Quick Links

- **Product Specification:** [docs/ProductSpec.md](docs/ProductSpec.md) — Business goals & success metrics
- **DevOps & Deployment:** [docs/DevOps.md](docs/DevOps.md) — CI/CD, Azure infrastructure, monitoring
- **Local Setup Guide:** [docs/LocalSetup.md](docs/LocalSetup.md) — Day 1 onboarding & Docker Compose
- **Architecture Diagrams:** [docs/Architecture.mmd](docs/Architecture.mmd) — System context, components, data flow
- **Application Flow:** [docs/ApplicationFlow.mmd](docs/ApplicationFlow.mmd) — User journeys & game state transitions
- **Data Model:** [docs/DataModel.mmd](docs/DataModel.mmd) — Entity relationships & schema
- **Component Map:** [docs/ComponentMap.mmd](docs/ComponentMap.mmd) — Frontend & backend architecture
- **Data Pipeline:** [docs/DataPipeline.mmd](docs/DataPipeline.mmd) — CRUD operations & state sync
- **UI Screenshots:** [docs/screenshot-*.html](docs/) — Interactive mockups of all game screens

---

## Overview

### Game Loop
```
Player Opens App 
  ↓
Connect to Server (SignalR)
  ↓
Wait for Opponent
  ↓
Ready Check + Color Selection
  ↓
3-Second Countdown
  ↓
RACE! (Real-time sync via WebSocket)
  ↓
First to Finish Line Wins
  ↓
Display Winner + Time
  ↓
Restart or Reconnect
```

### Key Features

| Feature | Tech | Status |
|---------|------|--------|
| **Real-time Multiplayer** | SignalR WebSocket | ✅ Live |
| **Animated Sprites** | 4-directional walk/jump | ✅ Live |
| **Game State Sync** | Server-authoritative, 100ms ticks | ✅ Live |
| **Anti-cheat Validation** | Velocity caps, rate limiting | ✅ Live |
| **Responsive UI** | HTML5 Canvas + Glassmorphism CSS | ✅ Live |
| **Audio Engine** | Web Audio API | ✅ Live |
| **Cloud Deployment** | Azure App Service + SignalR Service | ✅ Live |

---

## Architecture at a Glance

```
┌─────────────────────────────────────────────────────────────┐
│                     BANANA GAME PLATFORM                    │
├─────────────────────────────────────────────────────────────┤
│                                                              │
│  ┌──────────────────────┐      ┌─────────────────────────┐ │
│  │   FRONTEND (Vite)    │◄────►│  BACKEND (.NET + C#)    │ │
│  │   ─────────────────  │      │  ─────────────────────  │ │
│  │  • Canvas Rendering  │      │  • SignalR Hub          │ │
│  │  • Event Handlers    │      │  • Session Manager      │ │
│  │  • Audio Engine      │      │  • Room Matchmaking     │ │
│  │  • State Machine     │  WS  │  • Game Logic           │ │
│  │  • 60fps Render Loop │◄────►│  • Anti-cheat           │ │
│  │                      │      │  • 100ms Game Ticks     │ │
│  └──────────────────────┘      └─────────────────────────┘ │
│           │                              │                  │
│           ▼                              ▼                  │
│  ┌─────────────────┐          ┌──────────────────────┐     │
│  │  Sprite Assets  │          │  In-Memory Sessions  │     │
│  │  (PNG frames)   │          │  (GameRoom, Player)  │     │
│  └─────────────────┘          └──────────────────────┘     │
│                                                              │
└─────────────────────────────────────────────────────────────┘
                             │
                    ┌────────▼────────┐
                    │  Azure Services │
                    ├─────────────────┤
                    │ • App Service   │
                    │ • SignalR Svc   │
                    │ • App Insights  │
                    │ • KeyVault      │
                    │ • Blob Storage  │
                    │ • CDN           │
                    └─────────────────┘
```

---

## Technology Stack

### Frontend
- **Framework:** Vite + Vanilla JavaScript
- **Rendering:** HTML5 Canvas (2D context, pixelated art style)
- **Networking:** SignalR Client (@microsoft/signalr)
- **Styling:** CSS3 (Glassmorphism, animations, responsive)
- **Audio:** Web Audio API (sound effects)

### Backend
- **Framework:** .NET 10 / ASP.NET Core
- **Networking:** SignalR (WebSocket with Long Polling fallback)
- **Language:** C#
- **Architecture:** Feature-based folder structure (GameHub, GameSessionManager)
- **Concurrency:** ConcurrentDictionary for thread-safe room/player state

### Infrastructure
- **Cloud:** Microsoft Azure
- **Deployment:** GitHub Actions CI/CD
- **Database:** In-memory (future: Redis for distributed sessions)
- **Monitoring:** Application Insights
- **Hosting:** Azure App Service (auto-scaling)
- **Real-time API:** Azure SignalR Service (managed WebSocket)

---

## Getting Started

### Prerequisites
- Node.js 18+
- .NET 10 SDK
- Git
- Docker (optional)

### Quick Start (5 minutes)

```bash
# 1. Clone
git clone https://github.com/YourOrg/BananaGame.git
cd BananaGame

# 2. Backend
cd server
dotnet restore
dotnet run
# Listens on http://localhost:5000

# 3. Frontend (new terminal)
cd client
npm install
npm run dev
# Vite dev server on http://localhost:5173

# 4. Open browser
# http://localhost:5173
```

### Using Docker Compose
```bash
docker-compose up
# Backend on :5000, Frontend on :5173
```

See [Local Setup Guide](docs/LocalSetup.md) for detailed instructions.

---

## Project Structure

```
BananaGame/
├── client/                          # Frontend (Vite + JavaScript)
│   ├── main.js                      # Game loop, input handling, canvas rendering
│   ├── audioEngine.js               # Sound effects and Web Audio API
│   ├── index.html                   # HTML entry point
│   ├── style.css                    # Glassmorphism UI, animations
│   ├── vite.config.js               # Vite configuration & SignalR proxy
│   ├── package.json                 # Node dependencies
│   └── public/
│       ├── man_dressed_in_banana_suit/
│       │   ├── rotations/           # Idle sprites (4 directions)
│       │   ├── animations/
│       │   │   ├── walk/            # 6-frame walk animation per direction
│       │   │   └── jumping-1/       # 9-frame jump animation per direction
│       │   └── metadata.json        # Animation definitions
│       └── ...
│
├── server/                          # Backend (.NET 10 / C#)
│   ├── Program.cs                   # Startup, services registration, middleware
│   ├── Server.csproj                # Project file & dependencies
│   ├── Features/
│   │   ├── GameSession/
│   │   │   ├── GameHub.cs           # SignalR Hub (connection, messages)
│   │   │   ├── Models/
│   │   │   │   └── GameModels.cs    # GameRoom, Player, GameStatus enums
│   │   │   └── State/
│   │   │       ├── GameSessionManager.cs      # Room mgmt, matchmaking, game loop
│   │   │       └── IGameSessionManager.cs     # Interface for DI
│   └── Properties/
│       └── launchSettings.json      # Debug profiles
│
├── docs/                            # Documentation
│   ├── Architecture.mmd              # C4 System Context diagram
│   ├── ApplicationFlow.mmd           # User journey flowchart
│   ├── DataModel.mmd                 # ER diagram (entities & state)
│   ├── ComponentMap.mmd              # Component hierarchy
│   ├── DataPipeline.mmd              # CRUD & state sync workflow
│   ├── ProductSpec.md                # PRD, business goals, success metrics
│   ├── DevOps.md                     # CI/CD, Azure deployment, monitoring
│   ├── LocalSetup.md                 # Day 1 guide, Docker Compose, debugging
│   ├── ImprovementSuggestions.md     # Top 5 recommendations
│   └── screenshot-*.html             # UI mockups (all game screens)
│
├── README.md                        # This file
├── BananaGame.slnx                  # Solution file
└── Directory.Packages.props          # NuGet package management

```

---

## Key Concepts

### Game State Machine
```
Waiting ──[AllReady]──> Countdown ──[CountdownEnd]──> Playing ──[PlayerFinished]──> GameOver
  │                                                      │
  └──────────────────[Disconnect]────────────────────────┘
```

### Player Data Flow
```
1. Client Input (KeyDown): X position += 10px, action = "Walk"
2. Client sends PlayerUpdate to Hub via SignalR
3. Server validates (max velocity, time between updates)
4. Server updates player state in GameRoom
5. Server broadcasts gameState to all players in room (100ms tick)
6. Client receives gameState event → updates local serverPlayers
7. Client render loop draws position + animation frame
8. Browser displays animated banana sprite moving
```

### Real-time Synchronization
- **Tech:** SignalR WebSocket double-way communication
- **Broadcast Rate:** 100ms server ticks via `ServerTick` timer
- **Tick Payload:** `{players: [...], status, countdownStartTimeMs, raceStartTimeMs, finishedPlayerId}`
- **Client Interpolation:** Position animated between server updates for smooth motion
- **Anti-cheat:** Server validates velocity caps & minimum 50ms between updates

---

## Documentation Suite

Each documentation file serves a specific purpose:

| File | Audience | Purpose |
|------|----------|---------|
| **ProductSpec.md** | PMs, stakeholders | Business goals, KPIs, success metrics |
| **DevOps.md** | DevOps engineers, backend team | Deployment pipeline, secrets, monitoring |
| **LocalSetup.md** | New developers, onboarding | Day 1 guide, troubleshooting, debugging |
| **Architecture.mmd** | Architects, tech leads | System context, Azure topology |
| **ApplicationFlow.mmd** | UX, frontend, QA | User journeys, state transitions |
| **DataModel.mmd** | Database designers, backend | Entity relationships, schema |
| **ComponentMap.mmd** | Frontend, backend teams | Component hierarchy, dependencies |
| **DataPipeline.mmd** | Backend, integration teams | CRUD operations, event flow |

---

## Development Workflow

### 1. Create Feature Branch
```bash
git checkout -b feature/my-cool-feature
```

### 2. Code Changes
Edit files in `client/` or `server/`. Vite & dotnet watch auto-reload.

### 3. Test Locally
```bash
# Backend
cd server && dotnet test

# Frontend
cd client && npm test
```

### 4. Commit
```bash
git add .
git commit -m "feat: add player color selection"
git push origin feature/my-cool-feature
```

### 5. Pull Request
Create PR on GitHub. CI runs tests automatically. Code review required for `main`.

### 6. Deploy
Merge to `main` → GitHub Actions deploys to Production Azure App Service.

---

## Monitoring & Observability

All systems log to **Application Insights** with custom metrics:

```csharp
// Example: Track when a player wins
_telemetry.TrackEvent("PlayerWon", new() { 
    { "roomId", room.RoomId },
    { "playerId", player.ConnectionId },
    { "timeMs", finishTimeMs },
    { "opponent", room.Players.First(p => p.ConnectionId != finishedPlayerId).Name }
});
```

**Dashboards:**
- Active rooms gauge
- Players online (real-time)
- Avg game duration
- Win rate by color
- Error rate & latency

---

## Support & Resources

| Resource | Link |
|----------|------|
| **GitHub Issues** | [Issues](https://github.com/YourOrg/BananaGame/issues) |
| **GitHub Discussions** | [Discussions](https://github.com/YourOrg/BananaGame/discussions) |
| **Design Docs** | [Mermaid Diagrams](docs/) |
| **SignalR Docs** | [learn.microsoft.com/signalr](https://learn.microsoft.com/en-us/aspnet/core/signalr/) |
| **Vite Docs** | [vitejs.dev](https://vitejs.dev/) |
| **Azure Docs** | [learn.microsoft.com](https://learn.microsoft.com) |

---

## License

Proprietary © 2026 Banana Game Studios. All rights reserved.

---

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for guidelines.

1. Fork the repo
2. Create feature branch (`git checkout -b feature/AmazingFeature`)
3. Commit changes (`git commit -m 'feat: Add AmazingFeature'`)
4. Push to branch (`git push origin feature/AmazingFeature`)
5. Open Pull Request

---

**Last updated:** February 23, 2026 | Built with ❤️ + 🍌
