# Product Specification & Success Metrics

## Executive Summary
**Banana Game** is a multiplayer **2-player competitive racing game** where players in a banana suit race from the start line to the finish line. Players connect via WebSocket/SignalR, coordinate game readiness, select colors, and race in real-time with synchronized animations and scoring.

**Core Loop:**
1. Connect to matchmaking server
2. Wait for second player
3. Ready check + color selection
4. 3-second countdown
5. Race to finish line
6. Display winner and elapsed time

---

## Business Goals

| Goal | Metric | Target |
|------|--------|--------|
| **User Acquisition** | New players per day | 50+ |
| **Engagement** | Session length | 5+ minutes avg |
| **Retention** | DAU/Monthly repeat players | 30%+ |
| **Social** | Referral rate | 20% of new users from existing players |
| **Performance** | Page load time | <2 seconds |
| **Reliability** | Uptime | 99.5%+ monthly |

---

## Functional Requirements

### 1. **Matchmaking & Lobbies**
- **Automatic pairing:** Single player enters -> Server creates/joins room automatically
- **Room states:** `Waiting` (for partner), `Countdown` (3s), `Playing`, `GameOver`
- **Max 2 players per room** (1v1 racing format)
- **Room lifecycle:** Timeout after 10 minutes of inactivity

### 2. **Player State Management**
- **Connection:** Store player `connectionId`, `roomId`, color preference
- **Ready check:** Players must explicitly click "Ready"
- **Color selection:** 4 options (Red, Blue, Green, Yellow)
- **Positions:** X/Y coordinates updated per 100ms server tick
- **Animations:** Play `walk` or `jumping` based on physics

### 3. **Game Physics**
- **Start position:** X = 150px (start line)
- **Finish position:** X = worldWidth - 150px
- **Movement:** Keyboard arrow/WASD keys increase velocity (+500px/s max)
- **Friction:** Velocity decays per frame (smooth deceleration)
- **Validation:** Server anti-cheat checks max velocity and time between updates (50ms minimum)

### 4. **Real-Time Synchronization**
- **Tech:** SignalR WebSocket with fallback to Long Polling
- **Tick rate:** Server broadcasts `gameState` at 100ms intervals
- **Payload:** `{players: [...], status, countdownStartTimeMs, raceStartTimeMs, finishedPlayerId}`
- **Client rendering:** Interpolate position between ticks for smooth animation

### 5. **Win Detection & Scoring**
- **End condition:** First player to reach X = finishLineX
- **Score:** Time elapsed (milliseconds from race start to finish)
- **Broadcast:** `gameOver` event with winner ID + elapsed time
- **Display:** 5-second win screen before offering Restart/Reconnect

---

## Non-Functional Requirements

| Aspect | Requirement | Rationale |
|--------|-------------|-----------|
| **Latency** | <100ms RTT (SignalR) | Real-time gameplay |
| **Throughput** | 50 concurrent connections | Scalable to 100s of rooms |
| **Availability** | 99.5% uptime SLO | Enterprise-grade reliability |
| **Security** | No PII stored; HTTPS only | Privacy compliance |
| **Accessibility** | WCAG 2.1 AA compliant | Inclusive design |

---

## Success Criteria

### ✅ **Immediate (v1.0 Launch)**
- [x] 2-player matchmaking works end-to-end
- [x] Animations play smoothly at 60fps  
- [x] Winning player determined correctly
- [x] All UI screens display and transition properly
- [x] No connection drops during 10-minute gameplay session

### ✅ **Short-term (Weeks 1-4)**
- [ ] Achieve 50+ concurrent players
- [ ] Average session length 5+ minutes
- [ ] Error rate <0.1%
- [ ] Mobile-responsive design tested

### ✅ **Medium-term (Months 1-3)**
- [ ] 4-player rooms (tournament mode)
- [ ] Leaderboards with persistent stats
- [ ] Custom skins/cosmetics
- [ ] 1000+ DAU

---

## Data Model

### **GameRoom** Entity
```
{
  roomId: uuid,
  status: "waiting" | "countdown" | "playing" | "gameover",
  players: Player[],
  countdownStartTimeMs: long,
  raceStartTimeMs: long,
  finishedPlayerId: string,
  createdAtMs: long
}
```

### **Player** Entity
```
{
  connectionId: string,
  roomId: string,
  name: string,
  color: "red" | "blue" | "green" | "yellow",
  positionX: float,
  positionY: float,
  velocityX: float,
  velocityY: float,
  isReady: boolean,
  isFinished: boolean,
  finishTimeMs: long
}
```

---

## Success Metrics & KPIs

| Metric | Definition | Current | Target |
|--------|-----------|---------|--------|
| **Connection Success Rate** | Successful SignalR handshakes / total attempts | 98% | 99.9%+ |
| **Average Session Duration** | Mean time from connect to disconnect | — | 5+ min |
| **Match Completion Rate** | Completed races / started races | — | 95%+ |
| **Player Satisfaction** | NPS score (post-game survey) | — | 40+ |
| **Error Rate** | Request errors / total requests | <0.5% | <0.1% |
| **P95 Latency** | 95th percentile RTT to server | 80ms | <50ms |

---

## Future Roadmap (Out of Scope v1.0)

- **Persistent accounts** with authentication
- **Cosmetics shop** (skins, particle effects)
- **Seasonal challenges** and tournaments
- **Mobile app** (native iOS/Android)
- **Spectator mode** for streaming
- **Replay system** (replay stored clips)
