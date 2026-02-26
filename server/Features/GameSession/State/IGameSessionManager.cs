using PoBananaGame.Features.GameSession.Models;

namespace PoBananaGame.Features.GameSession.State;

public interface IGameSessionManager
{
    GameRoom? GetRoomForPlayer(string connectionId);
    (GameRoom Room, bool IsNewRoom) AddPlayer(string connectionId);
    GameRoom? RemovePlayer(string connectionId);
    void UpdatePlayerState(string connectionId, ClientState clientState);
    bool SetPlayerColor(string connectionId, PlayerColor color);
    bool SetPlayerReady(string connectionId);
    void FinishRace(string connectionId);
    bool AllPlayersReady(string roomId);
    void StartCountdown(string roomId);
    bool RequestRestart(string connectionId);
}
