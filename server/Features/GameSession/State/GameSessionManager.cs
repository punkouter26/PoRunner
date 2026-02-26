using System.Collections.Concurrent;
using Microsoft.AspNetCore.SignalR;
using PoBananaGame.Features.GameSession.Models;

namespace PoBananaGame.Features.GameSession.State;

public class GameSessionManager : IHostedService, IGameSessionManager, IDisposable
{
    private readonly ConcurrentDictionary<string, GameRoom> _rooms = new();
    private readonly ConcurrentDictionary<string, string> _playerToRoomMap = new();
    private readonly IServiceProvider _serviceProvider;
    private Timer? _gameLoopTimer;
    private long _roomCounter = 0;

    // Movement validation constants
    private const float MaxVelocityPixelsPerSecond = 500f; // Prevent teleporting
    private const float MinTimeBetweenUpdatesSeconds = 0.05f; // 50ms tick

    public GameSessionManager(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        // 100ms server tick rate
        _gameLoopTimer = new Timer(ServerTick, null, TimeSpan.Zero, TimeSpan.FromMilliseconds(100));
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _gameLoopTimer?.Change(Timeout.Infinite, 0);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _gameLoopTimer?.Dispose();
    }

    private async void ServerTick(object? state)
    {
        try
        {
            using var scope = _serviceProvider.CreateScope();
            var hubContext = scope.ServiceProvider.GetRequiredService<IHubContext<GameHub>>();
            var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            foreach (var room in _rooms.Values)
            {
                bool stateChanged = false;

                if (room.Status == GameStatus.Countdown)
                {
                    if (nowMs >= room.RaceStartTimeMs)
                    {
                        room.Status = GameStatus.Playing;
                        stateChanged = true;
                    }
                }

                if (stateChanged)
                {
                    await BroadcastRoomState(hubContext, room);
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameSessionManager] Error in server tick: {ex.Message}");
        }
    }

    private async Task BroadcastRoomState(IHubContext<GameHub> hubContext, GameRoom room)
    {
        await hubContext.Clients.Group(room.RoomId).SendAsync("gameState", new
        {
            players = room.Players,
            status = room.Status.ToString().ToLower(), // Lowercase to match JS expected string
            countdownStartTimeMs = room.CountdownStartTimeMs,
            raceStartTimeMs = room.RaceStartTimeMs,
            finishedPlayerId = room.FinishedPlayerId
        });
    }

    public GameRoom? GetRoomForPlayer(string connectionId)
    {
        if (_playerToRoomMap.TryGetValue(connectionId, out var roomId) && _rooms.TryGetValue(roomId, out var room))
        {
            return room;
        }
        return null;
    }

    public (GameRoom Room, bool IsNewRoom) AddPlayer(string connectionId)
    {
        // Find an open room (less than 2 players) that is still waiting for players or in readycheck
        var openRoom = _rooms.Values.FirstOrDefault(r => r.Players.Count < 2 &&
            (r.Status == GameStatus.Waiting || r.Status == GameStatus.ReadyCheck));
        bool isNewRoom = false;

        if (openRoom == null)
        {
            isNewRoom = true;
            var newRoomId = $"Room_{Interlocked.Increment(ref _roomCounter)}";
            openRoom = new GameRoom { RoomId = newRoomId };
            _rooms.TryAdd(newRoomId, openRoom);
        }

        bool isPlayer1 = openRoom.Players.IsEmpty;

        var newPlayer = new PlayerState
        {
            Id = connectionId,
            ColorTint = isPlayer1 ? PlayerColor.Yellow : PlayerColor.Blue,
            Direction = "east",
            X = 100,
            Y = isPlayer1 ? 0 : 1
        };

        openRoom.Players.TryAdd(connectionId, newPlayer);
        _playerToRoomMap.TryAdd(connectionId, openRoom.RoomId);

        // Transition to ReadyCheck only when a second player joins
        if (openRoom.Players.Count >= 2 && openRoom.Status == GameStatus.Waiting)
        {
            openRoom.Status = GameStatus.ReadyCheck;
        }

        return (openRoom, isNewRoom);
    }

    public GameRoom? RemovePlayer(string connectionId)
    {
        if (_playerToRoomMap.TryRemove(connectionId, out var roomId) && _rooms.TryGetValue(roomId, out var room))
        {
            room.Players.TryRemove(connectionId, out _);

            if (room.Players.IsEmpty)
            {
                _rooms.TryRemove(roomId, out _); // Clean up empty room
            }
            else
            {
                // Unready remaining player
                foreach (var p in room.Players.Values) { p.IsReady = false; }
                
                // If game is not over, return to Waiting so solo player waits for a new opponent
                if (room.Status != GameStatus.GameOver)
                {
                    room.Status = GameStatus.Waiting;
                }
            }
            return room;
        }
        return null;
    }

    public void UpdatePlayerState(string connectionId, ClientState clientState)
    {
        var room = GetRoomForPlayer(connectionId);
        if (room == null || room.Status != GameStatus.Playing) return;

        if (room.Players.TryGetValue(connectionId, out var player))
        {
            // Anti-Cheat: Simple Velocity Check
            // A genuine client only moves 10px per tick.
            // If delta > 50, clamp it.
            float targetX = clientState.X;
            float deltaX = targetX - player.X;
            
            if (deltaX > 200) // Huge jump
            {
                 Console.WriteLine($"[Anti-Cheat] Player {connectionId} attempted to move {deltaX}px in one tick. Clamping.");
                 targetX = player.X + 20; // Clamp
            }

            player.X = targetX;
            player.Y = clientState.Y;
            player.Direction = clientState.Direction;
            player.Action = clientState.Action;
            player.CurrentFrame = clientState.CurrentFrame;
        }
    }

    public bool SetPlayerReady(string connectionId)
    {
        var room = GetRoomForPlayer(connectionId);
        // Solo players can ready up from Waiting; 2-player rooms use ReadyCheck
        if (room == null || (room.Status != GameStatus.ReadyCheck && room.Status != GameStatus.Waiting)) return false;

        if (room.Players.TryGetValue(connectionId, out var player))
        {
            player.IsReady = true;
            return true;
        }
        return false;
    }

    public bool SetPlayerColor(string connectionId, PlayerColor color)
    {
        var room = GetRoomForPlayer(connectionId);
        if (room == null || (room.Status != GameStatus.ReadyCheck && room.Status != GameStatus.Waiting)) return false;

        if (room.Players.TryGetValue(connectionId, out var player))
        {
            player.ColorTint = color;
            return true;
        }
        return false;
    }

    public bool AllPlayersReady(string roomId)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            return room.Players.Count >= 1 && room.Players.Values.All(p => p.IsReady);
        }
        return false;
    }

    public void StartCountdown(string roomId)
    {
        if (_rooms.TryGetValue(roomId, out var room))
        {
            room.Status = GameStatus.Countdown;
            room.CountdownStartTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            room.RaceStartTimeMs = room.CountdownStartTimeMs + 3000;
            room.FinishedPlayerId = "";
            room.FinishTimeMs = 0;
            
            // Reset Positions
            var pIds = room.Players.Keys.ToList();
            if (pIds.Count >= 1 && room.Players.TryGetValue(pIds[0], out var p1))
            {
                p1.X = 100;
                p1.Y = 0;
                p1.Direction = "east";
                p1.IsReady = false; 
            }
            if (pIds.Count >= 2 && room.Players.TryGetValue(pIds[1], out var p2))
            {
                p2.X = 100;
                p2.Y = 1;
                p2.Direction = "east";
                p2.IsReady = false;
            }
        }
    }

    public void FinishRace(string connectionId)
    {
        var room = GetRoomForPlayer(connectionId);
        if (room == null || room.Status == GameStatus.GameOver) return;

        room.Status = GameStatus.GameOver;
        room.FinishedPlayerId = connectionId;
        room.FinishTimeMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - room.RaceStartTimeMs;
    }

    public bool RequestRestart(string connectionId)
    {
        var room = GetRoomForPlayer(connectionId);
        if (room == null || room.Status != GameStatus.GameOver) return false;

        // Reset readiness for all players
        foreach (var p in room.Players.Values) { p.IsReady = false; }

        // Solo: go back to Waiting so the player sees the solo-start screen
        // Multi: go to ReadyCheck so both players must agree to rematch
        room.Status = room.Players.Count >= 2 ? GameStatus.ReadyCheck : GameStatus.Waiting;
        return true;
    }
}
