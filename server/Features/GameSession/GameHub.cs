using Microsoft.AspNetCore.SignalR;
using PoBananaGame.Features.GameSession.Models;
using PoBananaGame.Features.GameSession.State;
using PoBananaGame.Features.HighScores;

namespace PoBananaGame.Features.GameSession;

public class GameHub : Hub
{
    private readonly IGameSessionManager _sessionManager;
    private readonly IHighScoreService _highScores;

    public GameHub(IGameSessionManager sessionManager, IHighScoreService highScores)
    {
        _sessionManager = sessionManager;
        _highScores = highScores;
    }

    public override async Task OnConnectedAsync()
    {
        Console.WriteLine($"[Socket] User connected: {Context.ConnectionId}");

        var (room, isNewRoom) = _sessionManager.AddPlayer(Context.ConnectionId);

        // Add the connection to the specific SignalR group for the room
        await Groups.AddToGroupAsync(Context.ConnectionId, room.RoomId);

        await Clients.Group(room.RoomId).SendAsync("gameState", new
        {
            players = room.Players,
            status = room.Status.ToString().ToLower(),
            countdownStartTimeMs = room.CountdownStartTimeMs,
            raceStartTimeMs = room.RaceStartTimeMs,
            finishedPlayerId = room.FinishedPlayerId
        });

        // Send current leaderboard to the newly connected client only
        var scores = await _highScores.GetTopScoresAsync();
        await Clients.Caller.SendAsync("highScores", scores);

        await base.OnConnectedAsync();
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        Console.WriteLine($"[Socket] User disconnected: {Context.ConnectionId}");

        var roomToNotify = _sessionManager.RemovePlayer(Context.ConnectionId);

        if (roomToNotify != null)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, roomToNotify.RoomId);

            await Clients.Group(roomToNotify.RoomId).SendAsync("gameState", new
            {
                players = roomToNotify.Players,
                status = roomToNotify.Status.ToString().ToLower(),
                countdownStartTimeMs = roomToNotify.CountdownStartTimeMs,
                raceStartTimeMs = roomToNotify.RaceStartTimeMs,
                finishedPlayerId = roomToNotify.FinishedPlayerId
            });
        }

        await base.OnDisconnectedAsync(exception);
    }

    public async Task PlayerReady()
    {
        var room = _sessionManager.GetRoomForPlayer(Context.ConnectionId);
        if (room == null) return;

        if (_sessionManager.SetPlayerReady(Context.ConnectionId))
        {
            if (_sessionManager.AllPlayersReady(room.RoomId))
            {
                _sessionManager.StartCountdown(room.RoomId);
            }

            // Sync state
            await Clients.Group(room.RoomId).SendAsync("gameState", new
            {
                players = room.Players,
                status = room.Status.ToString().ToLower(),
                countdownStartTimeMs = room.CountdownStartTimeMs,
                raceStartTimeMs = room.RaceStartTimeMs,
                finishedPlayerId = room.FinishedPlayerId
            });
        }
    }

    public async Task SelectColor(PlayerColor color)
    {
        var room = _sessionManager.GetRoomForPlayer(Context.ConnectionId);
        if (room == null) return;

        if (_sessionManager.SetPlayerColor(Context.ConnectionId, color))
        {
            await Clients.Group(room.RoomId).SendAsync("gameState", new
            {
                players = room.Players,
                status = room.Status.ToString().ToLower(),
                countdownStartTimeMs = room.CountdownStartTimeMs,
                raceStartTimeMs = room.RaceStartTimeMs,
                finishedPlayerId = room.FinishedPlayerId
            });
        }
    }

    public async Task PlayerUpdate(ClientState clientState)
    {
        _sessionManager.UpdatePlayerState(Context.ConnectionId, clientState);
        // The separate BackgroundService tick handles broadcasting the general state every 100ms
        // But for smooth player movement, we can broadcast immediately here as well
        var room = _sessionManager.GetRoomForPlayer(Context.ConnectionId);
        if (room != null)
        {
            await Clients.Group(room.RoomId).SendAsync("gameState", new
            {
                players = room.Players,
                status = room.Status.ToString().ToLower(),
                countdownStartTimeMs = room.CountdownStartTimeMs,
                raceStartTimeMs = room.RaceStartTimeMs,
                finishedPlayerId = room.FinishedPlayerId
            });
        }
    }

    public async Task PlayerFinished()
    {
        var room = _sessionManager.GetRoomForPlayer(Context.ConnectionId);
        if (room == null || room.Status == GameStatus.GameOver) return;

        _sessionManager.FinishRace(Context.ConnectionId);

        await Clients.Group(room.RoomId).SendAsync("gameOver", new
        {
            winnerId = room.FinishedPlayerId,
            timeMs = room.FinishTimeMs,
            players = room.Players
        });

        Console.WriteLine($"[Socket] Player {room.FinishedPlayerId} finished in {room.FinishTimeMs}ms");

        // Save the finish time and broadcast updated leaderboard to all clients in the room
        await _highScores.SaveScoreAsync(room.FinishTimeMs);
        var scores = await _highScores.GetTopScoresAsync();
        await Clients.Group(room.RoomId).SendAsync("highScores", scores);
    }

    public async Task GetHighScores()
    {
        var scores = await _highScores.GetTopScoresAsync();
        await Clients.Caller.SendAsync("highScores", scores);
    }

    public async Task RequestRestart()
    {
        if (_sessionManager.RequestRestart(Context.ConnectionId))
        {
             var room = _sessionManager.GetRoomForPlayer(Context.ConnectionId);
             if (room != null)
             {
                 await Clients.Group(room.RoomId).SendAsync("gameState", new
                 {
                     players = room.Players,
                     status = room.Status.ToString().ToLower(),
                     countdownStartTimeMs = room.CountdownStartTimeMs,
                     raceStartTimeMs = room.RaceStartTimeMs,
                     finishedPlayerId = room.FinishedPlayerId
                 });
             }
        }
    }
}
