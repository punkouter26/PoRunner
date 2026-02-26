namespace PoBananaGame.Features.GameSession.Models;

/// <summary>
/// Broadcast to all clients in a room whenever the game state changes.
/// </summary>
public record GameStateDto(
    string Status,
    Dictionary<string, PlayerState> Players,
    long CountdownStartTimeMs,
    long RaceStartTimeMs,
    string FinishedPlayerId
);

/// <summary>
/// Broadcast to all clients in a room when a player crosses the finish line.
/// </summary>
public record GameOverDto(
    string WinnerId,
    Dictionary<string, PlayerState> Players,
    long TimeMs
);
