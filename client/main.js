import './style.css';
import * as signalR from '@microsoft/signalr';
import { playSound, audioCtx } from './audioEngine.js';

const canvas = document.getElementById('gameCanvas');
const ctx = canvas.getContext('2d');

canvas.width = window.innerWidth;
canvas.height = window.innerHeight;

canvas.width = window.innerWidth;
canvas.height = window.innerHeight;

let worldWidth = Math.max(1200, window.innerWidth);
const START_LINE_X = 150;
// Finish line is halfway along the full world width (50% shorter race)
let FINISH_LINE_X = 150 + Math.round((worldWidth - 300) * 0.5);

window.addEventListener('resize', () => {
    canvas.width = window.innerWidth;
    canvas.height = window.innerHeight;
    worldWidth = Math.max(1200, window.innerWidth);
    FINISH_LINE_X = 150 + Math.round((worldWidth - 300) * 0.5);
});

// UI Elements
const uiError = document.getElementById('ui-error');
const uiWaiting = document.getElementById('ui-waiting');
const uiReadyCheck = document.getElementById('ui-readycheck');
const uiCountdown = document.getElementById('ui-countdown');
const uiPlaying = document.getElementById('ui-playing');
const uiGameOver = document.getElementById('ui-gameover');

const errorMessage = document.getElementById('error-message');
const readyStatusText = document.getElementById('ready-status-text');
const btnReady = document.getElementById('btn-ready');
const btnRestart = document.getElementById('btn-restart');
const btnReconnect = document.getElementById('btn-reconnect');
const countdownText = document.getElementById('countdown-text');
const hudTimer = document.getElementById('hud-timer');
const winnerText = document.getElementById('winner-text');
const finalTimeText = document.getElementById('final-time-text');
const comboKeys = [
    document.getElementById('combo-T'),
    document.getElementById('combo-Y'),
    document.getElementById('combo-G'),
    document.getElementById('combo-H')
];

// TYGH combo mechanic
const COMBO = ['t', 'y', 'g', 'h'];
let comboIndex = 0;

function hideAllScreens() {
    uiError.classList.add('hidden');
    uiWaiting.classList.add('hidden');
    uiReadyCheck.classList.add('hidden');
    uiCountdown.classList.add('hidden');
    uiPlaying.classList.add('hidden');
    uiGameOver.classList.add('hidden');
}

// Multiplayer Networking
// Use relative URL for local dev, full URL for production
const isProduction = window.location.hostname !== 'localhost' && !window.location.hostname.includes('127.0.0.1');
const hubUrl = isProduction 
    ? "https://wa-porunner.azurewebsites.net/gamehub" 
    : "/gamehub";

const connection = new signalR.HubConnectionBuilder()
    .withUrl(hubUrl)
    .configureLogging(signalR.LogLevel.Information)
    .build();

let serverPlayers = {};
let gameStatus = 'waiting';
let countdownStartTimeMs = 0;
let raceStartTimeMs = 0;
let finishedPlayerId = '';
let lastRaceTimeMs = 0;
let connectionError = '';
let topScores = [];  // [{rank, timeMs}]

let cameraX = 0;

connection.on('gameState', (data) => {
    // Signal to e2e tests that the server connection is live
    window.__serverConnected = true;
    // Expose live game status for test assertions (avoids DOM-class polling)
    window.__gameStatus = data.status;

    if (data.status === 'playing' && gameStatus !== 'playing') {
        comboIndex = 0; // reset combo sequence on each new race
        localSprite.action = 'idle';
        localSprite.frame = 0;
        localSprite.frameTimer = 0;
    }
    if (data.status === 'readycheck' || data.status === 'waiting') {
        localSprite.action = 'idle';
        localSprite.frame = 0;
        localSprite.frameTimer = 0;
    }
    serverPlayers = data.players;
    gameStatus = data.status;
    countdownStartTimeMs = data.countdownStartTimeMs || 0;
    raceStartTimeMs = data.raceStartTimeMs || 0;
    finishedPlayerId = data.finishedPlayerId || '';
});

connection.on('gameOver', (data) => {
    gameStatus = 'gameover';
    window.__gameStatus = 'gameover';
    finishedPlayerId = data.winnerId;
    serverPlayers = data.players;
    lastRaceTimeMs = data.timeMs;
    playSound('crowd');
});

connection.on('highScores', (scores) => {
    topScores = scores || [];
    renderLeaderboard();
});

connection.on('error', (msg) => {
    connectionError = msg;
});

async function start() {
    try {
        await connection.start();
        console.log("SignalR Connected.");
        connectionError = '';
    } catch (err) {
        connectionError = "Failed to connect to server. Ensure Backend is running.";
        console.error(err);
    }
}
start();

// ── Test hooks (used by Playwright e2e tests) ──────────────────────────────
// Sets window.__serverConnected = true  when first gameState arrives (see above).
// Sets window.__gameStatus to the current game phase string on every state change.
// Exposes __testPlayerReady() so tests can invoke PlayerReady without a button click.
window.__testPlayerReady = () => { void connection.invoke('PlayerReady'); };
window.__testRequestRestart = () => { void connection.invoke('RequestRestart'); };
// ──────────────────────────────────────────────────────────────────────────────

btnReconnect.addEventListener('click', () => {
    connectionError = '';
    start();
});

const leaderboardBody = document.getElementById('leaderboard-body');

function renderLeaderboard() {
    if (!leaderboardBody) return;
    if (!topScores.length) {
        leaderboardBody.innerHTML = '<tr><td colspan="2" style="opacity:0.5;padding:0.5rem 0;">No scores yet</td></tr>';
        return;
    }
    leaderboardBody.innerHTML = topScores.map(s => {
        const medal = s.rank === 1 ? '🥇' : s.rank === 2 ? '🥈' : s.rank === 3 ? '🥉' : `#${s.rank}`;
        const time = (s.timeMs / 1000).toFixed(3) + 's';
        const isNew = lastRaceTimeMs > 0 && s.timeMs === lastRaceTimeMs;
        return `<tr class="${isNew ? 'leaderboard-new' : ''}">
            <td class="leaderboard-rank">${medal}</td>
            <td class="leaderboard-time">${time}${isNew ? ' ← you!' : ''}</td>
        </tr>`;
    }).join('');
}

let lastBeepSec = -1;
let startedFlag = false;

window.addEventListener('keydown', (e) => {
    if (audioCtx.state === 'suspended') audioCtx.resume();
    if (gameStatus !== 'playing') return;

    if (e.repeat) return; // Prevent holding down
    const k = e.key.toLowerCase();

    // Wrong key — flash red, buzz, reset combo
    if (k !== COMBO[comboIndex]) {
        if (/^[a-z]$/.test(k)) {
            comboIndex = 0;
            playSound('wrong');
            document.getElementById('game-container').classList.remove('flash-red');
            void document.getElementById('game-container').offsetWidth;
            document.getElementById('game-container').classList.add('flash-red');
        }
        return;
    }

    comboIndex++;

    // Not yet a full TYGH — just update the indicator, no movement
    if (comboIndex < COMBO.length) return;

    // Full TYGH completed — reset, move, animate
    comboIndex = 0;

    // Kick off the walk animation locally
    localSprite.action = 'walk';
    localSprite.frame = 0;
    localSprite.frameTimer = 0;

    var myPlayer = serverPlayers[connection.connectionId];
    if (!myPlayer) return;

    myPlayer.x += 60;

    connection.invoke('PlayerUpdate', {
        x: myPlayer.x,
        y: myPlayer.y,
        direction: myPlayer.direction,
        action: 'Walk',
        currentFrame: 0
    }).catch(err => console.error(err));

    if (myPlayer.x >= FINISH_LINE_X) {
        myPlayer.x = FINISH_LINE_X;
        connection.invoke('PlayerFinished');
    }
});

btnReady.addEventListener('click', () => {
    if (audioCtx.state === 'suspended') audioCtx.resume();
    if (gameStatus === 'readycheck' || gameStatus === 'waiting') {
        connection.invoke('PlayerReady');
    }
});

const btnReadySolo = document.getElementById('btn-ready-solo');
if (btnReadySolo) {
    btnReadySolo.addEventListener('click', () => {
        if (audioCtx.state === 'suspended') audioCtx.resume();
        if (gameStatus === 'waiting') {
            connection.invoke('PlayerReady');
        }
    });
}

btnRestart.addEventListener('click', () => {
    if (gameStatus === 'gameover') {
        connection.invoke('RequestRestart');
    }
});



// Asset loading via Promise.all
const assets = {
    sky: new Image(),
    ground: new Image(),
    idle: { south: new Image(), west: new Image(), east: new Image(), north: new Image() },
    walk: { south: [], west: [], east: [], north: [] },
    jump: { south: [], west: [], east: [], north: [] },
};

let assetsLoaded = false;

const loadImage = (img, src) => new Promise(resolve => {
    img.src = src;
    img.onload = resolve;
});

async function loadAssets() {
    const promises = [];
    // Vite serves public folder files at root path, not /public/
    promises.push(loadImage(assets.sky, '/sky.png'));
    promises.push(loadImage(assets.ground, '/ground.png'));

    const dirs = ['south', 'west', 'east', 'north'];
    for (const dir of dirs) {
        promises.push(loadImage(assets.idle[dir], `/man_dressed_in_banana_suit/rotations/${dir}.png`));

        for (let i = 0; i < 6; i++) {
            const img = new Image();
            promises.push(loadImage(img, `/man_dressed_in_banana_suit/animations/walk/${dir}/frame_00${i}.png`));
            assets.walk[dir].push(img);
        }
        for (let i = 0; i < 9; i++) {
            const img = new Image();
            promises.push(loadImage(img, `/man_dressed_in_banana_suit/animations/jumping-1/${dir}/frame_00${i}.png`));
            assets.jump[dir].push(img);
        }
    }
    await Promise.all(promises);
    assetsLoaded = true;
}
loadAssets();

let lastTime = performance.now();
const WALK_FPS = 12;
let frameTimer = 0;

// Local-only sprite state so server gameState updates never interrupt the animation
const localSprite = { action: 'idle', frame: 0, frameTimer: 0 };

function update(dt) {
    if (gameStatus === 'playing') {
        if (localSprite.action === 'walk') {
            localSprite.frameTimer += dt;
            if (localSprite.frameTimer > 1 / WALK_FPS) {
                localSprite.frame++;
                if (localSprite.frame >= 6) {
                    localSprite.frame = 0;
                    localSprite.action = 'idle';
                }
                localSprite.frameTimer = 0;
            }
        }
    } else {
        localSprite.action = 'idle';
        localSprite.frame = 0;
        localSprite.frameTimer = 0;
    }
}

let skyPattern = null;
let groundPattern = null;

function render(time) {
    ctx.clearRect(0, 0, canvas.width, canvas.height);

    if (!assetsLoaded) {
        // We'll just skip rendering until loaded. The HTML handles UI.
        return;
    }

    const myPlayer = serverPlayers[connection.connectionId];
    let targetCameraX = 0;
    if (myPlayer) {
        targetCameraX = Math.max(0, myPlayer.x - (canvas.width / 3));
    }
    const maxPanX = Math.max(0, FINISH_LINE_X - canvas.width + 300);
    targetCameraX = Math.min(targetCameraX, maxPanX);

    // Snap camera if distance is too large (high latency mitigation)
    if (Math.abs(targetCameraX - cameraX) > 200) {
        cameraX = targetCameraX;
    } else {
        cameraX += (targetCameraX - cameraX) * 0.1;
    }

    ctx.save();
    ctx.translate(-cameraX, 0);

    if (assets.sky.width > 0 && assets.ground.width > 0) {
        if (!skyPattern) skyPattern = ctx.createPattern(assets.sky, 'repeat');
        if (!groundPattern) groundPattern = ctx.createPattern(assets.ground, 'repeat');

        ctx.save();
        ctx.fillStyle = skyPattern;
        ctx.fillRect(cameraX * 0.5, 0, canvas.width + cameraX, canvas.height - 150);

        ctx.translate(0, canvas.height - 150);
        ctx.fillStyle = groundPattern;
        ctx.fillRect(cameraX, 0, canvas.width + cameraX, 150);
        ctx.restore();

        // Start / Finish lines
        ctx.fillStyle = 'rgba(255, 0, 0, 0.7)';
        ctx.fillRect(START_LINE_X, canvas.height - 150, 10, 150);
        ctx.fillStyle = 'rgba(0, 255, 0, 0.7)';
        ctx.fillRect(FINISH_LINE_X, canvas.height - 150, 10, 150);

        ctx.fillStyle = 'white';
        ctx.font = 'bold 24px Inter, sans-serif';
        // Add text shadows using canvas context settings
        ctx.shadowColor = "rgba(0,0,0,0.8)";
        ctx.shadowBlur = 4;
        ctx.shadowOffsetX = 2;
        ctx.shadowOffsetY = 2;
        ctx.fillText('START', START_LINE_X + 20, canvas.height - 80);
        ctx.fillText('FINISH', FINISH_LINE_X + 20, canvas.height - 80);
        ctx.shadowBlur = 0;
        ctx.shadowOffsetX = 0;
        ctx.shadowOffsetY = 0;
    }

    // --- DOM UI UPDATES ---
    hideAllScreens();

    if (connectionError) {
        uiError.classList.remove('hidden');
        errorMessage.innerText = connectionError;
    } else if (gameStatus === 'waiting') {
        uiWaiting.classList.remove('hidden');
    } else if (gameStatus === 'readycheck') {
        uiReadyCheck.classList.remove('hidden');
        const p = serverPlayers[connection.connectionId];

        if (p && p.isReady) {
            btnReady.classList.add('hidden');
            readyStatusText.classList.remove('hidden');
        } else {
            btnReady.classList.remove('hidden');
            readyStatusText.classList.add('hidden');
        }
    } else if (gameStatus === 'countdown') {
        uiCountdown.classList.remove('hidden');
        const timeLeftMs = raceStartTimeMs - Date.now();
        if (timeLeftMs > 0) {
            const secs = Math.ceil(timeLeftMs / 1000);
            countdownText.innerText = secs.toString();
            if (secs !== lastBeepSec) {
                lastBeepSec = secs;
                if (secs <= 3) {
                    playSound('beep');
                    countdownText.style.transform = 'scale(1.5)';
                    setTimeout(() => countdownText.style.transform = 'scale(1)', 100);
                }
            }
            // Draw dark overlay on canvas behind countdown text
            ctx.fillStyle = 'rgba(0,0,0,0.4)';
            ctx.fillRect(cameraX, 0, canvas.width, canvas.height);
        } else {
            if (!startedFlag) {
                startedFlag = true;
                lastBeepSec = -1;
                playSound('gun');
            }
            countdownText.innerText = 'GO!';
        }
    } else if (gameStatus === 'gameover') {
        uiGameOver.classList.remove('hidden');
        let wName = "PLAYER";
        if (serverPlayers[finishedPlayerId]) {
            wName = serverPlayers[finishedPlayerId].colorTint.toLowerCase() === 'blue' ? "BLUE" : "YELLOW";
        }
        winnerText.innerText = `${wName} WINS!`;
        finalTimeText.innerText = `Final Time: ${(lastRaceTimeMs / 1000).toFixed(3)}s`;

        // Draw dark overlay
        ctx.fillStyle = 'rgba(0,0,0,0.6)';
        ctx.fillRect(cameraX, 0, canvas.width, canvas.height);

    } else if (gameStatus === 'playing') {
        uiPlaying.classList.remove('hidden');
        const elapsedMs = Math.max(0, Date.now() - raceStartTimeMs);
        hudTimer.innerText = `${(elapsedMs / 1000).toFixed(3)}s`;

        // Highlight the next key in the TYGH combo
        comboKeys.forEach((el, i) => {
            if (!el) return;
            el.classList.toggle('next', i === comboIndex);
        });
    }

    if (gameStatus !== 'countdown') {
        startedFlag = false;
        lastBeepSec = -1;
    }

    const playersToDraw = Object.values(serverPlayers).sort((a, b) => a.y - b.y);

    playersToDraw.forEach(p => {
        const isLocalPlayer = p.id === connection.connectionId;
        const action = isLocalPlayer ? localSprite.action : (p.action || 'idle');
        const frame  = isLocalPlayer ? localSprite.frame  : (p.currentFrame || 0);
        const dir = p.direction || 'east';

        let img;
        if (action === 'walk') img = assets.walk[dir][frame];
        else img = assets.idle[dir];

        if (img && img.width > 0) {
            const scale = 3;
            const w = img.width * scale;
            const h = img.height * scale;

            ctx.save();
            if (p.colorTint.toLowerCase() === 'blue') {
                ctx.filter = 'hue-rotate(240deg) saturate(200%)';
            } else if (p.colorTint.toLowerCase() === 'none') {
                // natural color
            }
            const BASE_Y = canvas.height - 100;
            const renderY = BASE_Y + (p.y * 60);

            ctx.drawImage(img, p.x - w / 2, renderY - h, w, h);
            ctx.restore();
        }
    });

    ctx.restore();
}

function loop(time) {
    const dt = (time - lastTime) / 1000;
    lastTime = time;

    if (dt < 0.1) {
        update(dt);
    }
    render(time);

    requestAnimationFrame(loop);
}

requestAnimationFrame(loop);
