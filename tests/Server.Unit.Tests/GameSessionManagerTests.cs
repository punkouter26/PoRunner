using Microsoft.Extensions.DependencyInjection;
using Moq;
using PoBananaGame.Features.GameSession.Models;
using PoBananaGame.Features.GameSession.State;
using Xunit;

namespace Server.Unit.Tests;

/// <summary>
/// Unit tests for GameSessionManager — pure in-memory state logic, no I/O.
/// Uses a stubbed IServiceProvider so the timer callback never fires.
/// </summary>
public class GameSessionManagerTests : IDisposable
{
    // ────────────────────────────────────────────────────────────── helpers ──

    private readonly Mock<IServiceProvider> _spMock = new();
    private readonly Mock<IServiceScope> _scopeMock = new();
    private readonly Mock<IServiceScopeFactory> _factoryMock = new();

    private GameSessionManager CreateManager()
    {
        // Wire up a minimal IServiceProvider so the constructor does not throw.
        // StartAsync is NOT called, so the timer never fires during unit tests.
        _factoryMock.Setup(f => f.CreateScope()).Returns(_scopeMock.Object);
        _spMock.Setup(sp => sp.GetService(typeof(IServiceScopeFactory))).Returns(_factoryMock.Object);
        return new GameSessionManager(_spMock.Object);
    }

    public void Dispose() { /* nothing to clean up — all in-memory */ }

    // ═══════════════════════════════════════════════════════════════════════
    // AddPlayer
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void AddPlayer_NoOpenRooms_CreatesNewRoom()
    {
        var mgr = CreateManager();
        var (room, isNew) = mgr.AddPlayer("p1");

        Assert.NotNull(room);
        Assert.True(isNew);
    }

    [Fact]
    public void AddPlayer_OpenRoomExists_JoinsExistingRoom()
    {
        var mgr = CreateManager();
        var (room1, _) = mgr.AddPlayer("p1");
        var (room2, isNew) = mgr.AddPlayer("p2");

        Assert.Same(room1, room2);
        Assert.False(isNew);
    }

    [Fact]
    public void AddPlayer_SecondPlayer_SetsReadyCheckStatus()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        var (room, _) = mgr.AddPlayer("p2");

        Assert.Equal(GameStatus.ReadyCheck, room.Status);
    }

    [Fact]
    public void AddPlayer_ThirdPlayer_CreatesNewRoom()
    {
        var mgr = CreateManager();
        var (room1, _) = mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        var (room3, isNew) = mgr.AddPlayer("p3");

        Assert.NotSame(room1, room3);
        Assert.True(isNew);
    }

    [Fact]
    public void AddPlayer_FirstPlayer_IsNewRoomTrue()
    {
        var mgr = CreateManager();
        var (_, isNew) = mgr.AddPlayer("p1");
        Assert.True(isNew);
    }

    [Fact]
    public void AddPlayer_SecondPlayer_IsNewRoomFalse()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        var (_, isNew) = mgr.AddPlayer("p2");
        Assert.False(isNew);
    }

    [Fact]
    public void AddPlayer_SecondPlayer_AssignsBlueColor()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");

        var room = mgr.GetRoomForPlayer("p2");
        Assert.NotNull(room);
        Assert.Equal(PlayerColor.Blue, room!.Players["p2"].ColorTint);
    }

    [Fact]
    public void AddPlayer_FirstPlayer_PositionY_IsZero()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");

        var room = mgr.GetRoomForPlayer("p1");
        Assert.Equal(0, room!.Players["p1"].Y);
    }

    [Fact]
    public void AddPlayer_SecondPlayer_PositionY_IsOne()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");

        var room = mgr.GetRoomForPlayer("p2");
        Assert.Equal(1, room!.Players["p2"].Y);
    }

    [Fact]
    public void AddPlayer_PlayersStartAt_X100()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        var room = mgr.GetRoomForPlayer("p1");
        Assert.Equal(100, room!.Players["p1"].X);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RemovePlayer
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void RemovePlayer_UnknownPlayer_ReturnsNull()
    {
        var mgr = CreateManager();
        var result = mgr.RemovePlayer("nobody");
        Assert.Null(result);
    }

    [Fact]
    public void RemovePlayer_LastPlayer_RoomIsGone()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.RemovePlayer("p1");

        // After removing the only player the room should be pruned;
        // adding a new player must therefore create a brand-new room.
        var (_, isNew) = mgr.AddPlayer("p2");
        Assert.True(isNew);
    }

    [Fact]
    public void RemovePlayer_WithRemainingPlayer_ResetsIsReady()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        mgr.SetPlayerReady("p2");

        mgr.RemovePlayer("p1");

        var room = mgr.GetRoomForPlayer("p2");
        Assert.False(room!.Players["p2"].IsReady);
    }

    [Fact]
    public void RemovePlayer_WithRemainingPlayer_SetsWaitingStatus()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2"); // -> ReadyCheck

        mgr.RemovePlayer("p1");

        var room = mgr.GetRoomForPlayer("p2");
        Assert.Equal(GameStatus.Waiting, room!.Status);
    }

    [Fact]
    public void RemovePlayer_GameOverRoom_DoesNotResetStatus()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        // force game over
        mgr.SetPlayerReady("p1");
        mgr.SetPlayerReady("p2");
        var room = mgr.GetRoomForPlayer("p1")!;
        mgr.StartCountdown(room.RoomId);
        // Manually advance status to Playing then GameOver
        room.Status = GameStatus.Playing;
        mgr.FinishRace("p1");

        mgr.RemovePlayer("p1");

        // p2 is still in the old room (Game Over)
        var remaining = mgr.GetRoomForPlayer("p2");
        Assert.Equal(GameStatus.GameOver, remaining!.Status);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GetRoomForPlayer
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void GetRoomForPlayer_UnknownPlayer_ReturnsNull()
    {
        var mgr = CreateManager();
        Assert.Null(mgr.GetRoomForPlayer("ghost"));
    }

    [Fact]
    public void GetRoomForPlayer_KnownPlayer_ReturnsCorrectRoom()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        var room = mgr.GetRoomForPlayer("p1");
        Assert.NotNull(room);
        Assert.True(room!.Players.ContainsKey("p1"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SetPlayerReady
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SetPlayerReady_WhenStatusIsWaiting_ReturnsTrue()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1"); // solo -> Waiting
        // Solo players can ready up from Waiting to start a solo race
        Assert.True(mgr.SetPlayerReady("p1"));
    }

    [Fact]
    public void SetPlayerReady_WhenReadyCheck_SetsIsReadyTrue()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2"); // -> ReadyCheck
        mgr.SetPlayerReady("p1");

        var room = mgr.GetRoomForPlayer("p1");
        Assert.True(room!.Players["p1"].IsReady);
    }

    [Fact]
    public void SetPlayerReady_UnknownPlayer_ReturnsFalse()
    {
        var mgr = CreateManager();
        Assert.False(mgr.SetPlayerReady("nobody"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // AllPlayersReady
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void AllPlayersReady_OnePlayerReady_ReturnsFalse()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        mgr.SetPlayerReady("p1");

        var room = mgr.GetRoomForPlayer("p1")!;
        Assert.False(mgr.AllPlayersReady(room.RoomId));
    }

    [Fact]
    public void AllPlayersReady_BothPlayersReady_ReturnsTrue()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        mgr.SetPlayerReady("p1");
        mgr.SetPlayerReady("p2");

        var room = mgr.GetRoomForPlayer("p1")!;
        Assert.True(mgr.AllPlayersReady(room.RoomId));
    }

    [Fact]
    public void AllPlayersReady_UnknownRoom_ReturnsFalse()
    {
        var mgr = CreateManager();
        Assert.False(mgr.AllPlayersReady("Room_9999"));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // StartCountdown
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void StartCountdown_SetsCountdownStatus()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        var room = mgr.GetRoomForPlayer("p1")!;

        mgr.StartCountdown(room.RoomId);

        Assert.Equal(GameStatus.Countdown, room.Status);
    }

    [Fact]
    public void StartCountdown_RaceStartTime_IsThreeSecondsAfterCountdown()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        var room = mgr.GetRoomForPlayer("p1")!;

        var before = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        mgr.StartCountdown(room.RoomId);
        var after = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        Assert.InRange(room.CountdownStartTimeMs, before, after + 50);
        Assert.Equal(room.CountdownStartTimeMs + 3000, room.RaceStartTimeMs);
    }

    [Fact]
    public void StartCountdown_ResetsPlayerPositions()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        var room = mgr.GetRoomForPlayer("p1")!;
        // Move p1 forward manually
        room.Players["p1"].X = 800;
        mgr.StartCountdown(room.RoomId);

        Assert.Equal(100, room.Players["p1"].X);
    }

    [Fact]
    public void StartCountdown_ClearsFinishedPlayerId()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        var room = mgr.GetRoomForPlayer("p1")!;
        room.FinishedPlayerId = "p1";

        mgr.StartCountdown(room.RoomId);

        Assert.Equal("", room.FinishedPlayerId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // UpdatePlayerState (anti-cheat)
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void UpdatePlayerState_NotPlayingStatus_IgnoresUpdate()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        // Status is ReadyCheck — update must be a no-op
        mgr.UpdatePlayerState("p1", new ClientState(500, 0, "east", PlayerAction.Walk, 1));

        var room = mgr.GetRoomForPlayer("p1")!;
        Assert.Equal(100, room.Players["p1"].X); // unchanged
    }

    [Fact]
    public void UpdatePlayerState_SmallMove_AppliesPosition()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        var room = mgr.GetRoomForPlayer("p1")!;
        room.Status = GameStatus.Playing;

        mgr.UpdatePlayerState("p1", new ClientState(110, 0, "east", PlayerAction.Walk, 2));

        Assert.Equal(110f, room.Players["p1"].X);
    }

    [Fact]
    public void UpdatePlayerState_LargeMove_ClampsPosition()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        var room = mgr.GetRoomForPlayer("p1")!;
        room.Status = GameStatus.Playing;

        // Current X=100, request X=500 (delta=400 > 200) → clamp to 100+20=120
        mgr.UpdatePlayerState("p1", new ClientState(500, 0, "east", PlayerAction.Walk, 3));

        Assert.Equal(120f, room.Players["p1"].X);
    }

    [Fact]
    public void UpdatePlayerState_UpdatesDirectionAndAction()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        var room = mgr.GetRoomForPlayer("p1")!;
        room.Status = GameStatus.Playing;

        mgr.UpdatePlayerState("p1", new ClientState(110, 0, "west", PlayerAction.Walk, 4));

        Assert.Equal("west", room.Players["p1"].Direction);
        Assert.Equal(PlayerAction.Walk, room.Players["p1"].Action);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SetPlayerColor
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void SetPlayerColor_WaitingStatus_SetsColor()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1"); // solo -> Waiting
        var result = mgr.SetPlayerColor("p1", PlayerColor.Yellow);

        Assert.True(result);
        var room = mgr.GetRoomForPlayer("p1")!;
        Assert.Equal(PlayerColor.Yellow, room.Players["p1"].ColorTint);
    }

    [Fact]
    public void SetPlayerColor_ReadyCheckStatus_SetsColor()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        var result = mgr.SetPlayerColor("p2", PlayerColor.Yellow);

        Assert.True(result);
    }

    [Fact]
    public void SetPlayerColor_PlayingStatus_ReturnsFalse()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        var room = mgr.GetRoomForPlayer("p1")!;
        room.Status = GameStatus.Playing;

        var result = mgr.SetPlayerColor("p1", PlayerColor.Yellow);

        Assert.False(result);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FinishRace
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void FinishRace_SetsGameOverStatus()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        var room = mgr.GetRoomForPlayer("p1")!;
        room.Status = GameStatus.Playing;

        mgr.FinishRace("p1");

        Assert.Equal(GameStatus.GameOver, room.Status);
    }

    [Fact]
    public void FinishRace_SetsFinishedPlayerId()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        var room = mgr.GetRoomForPlayer("p1")!;
        room.Status = GameStatus.Playing;

        mgr.FinishRace("p1");

        Assert.Equal("p1", room.FinishedPlayerId);
    }

    [Fact]
    public void FinishRace_AlreadyGameOver_DoesNotOverwrite()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        var room = mgr.GetRoomForPlayer("p1")!;
        room.Status = GameStatus.Playing;
        mgr.FinishRace("p1");

        // p2 tries to also finish — should be ignored
        mgr.FinishRace("p2");

        Assert.Equal("p1", room.FinishedPlayerId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // RequestRestart
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void RequestRestart_NotGameOver_ReturnsFalse()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");

        Assert.False(mgr.RequestRestart("p1"));
    }

    [Fact]
    public void RequestRestart_GameOver_SetsReadyCheckStatus()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        var room = mgr.GetRoomForPlayer("p1")!;
        room.Status = GameStatus.Playing;
        mgr.FinishRace("p1");

        var result = mgr.RequestRestart("p1");

        Assert.True(result);
        Assert.Equal(GameStatus.ReadyCheck, room.Status);
    }

    [Fact]
    public void RequestRestart_GameOver_ResetsPlayerReadiness()
    {
        var mgr = CreateManager();
        mgr.AddPlayer("p1");
        mgr.AddPlayer("p2");
        var room = mgr.GetRoomForPlayer("p1")!;
        room.Status = GameStatus.Playing;
        mgr.FinishRace("p1");
        // Manually mark ready
        room.Players["p1"].IsReady = true;
        room.Players["p2"].IsReady = true;

        mgr.RequestRestart("p1");

        Assert.All(room.Players.Values, p => Assert.False(p.IsReady));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GameModels defaults
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void PlayerState_Defaults_AreCorrect()
    {
        var ps = new PlayerState { Id = "test" };
        Assert.Equal(PlayerAction.Idle, ps.Action);
        Assert.Equal(PlayerColor.None, ps.ColorTint);
        Assert.Equal("east", ps.Direction);
        Assert.False(ps.IsReady);
    }

    [Fact]
    public void GameRoom_DefaultStatus_IsWaiting()
    {
        var gr = new GameRoom { RoomId = "r1" };
        Assert.Equal(GameStatus.Waiting, gr.Status);
        Assert.Empty(gr.Players);
    }

    [Fact]
    public void ClientState_Construction_StoresValues()
    {
        var cs = new ClientState(42.5f, 10f, "north", PlayerAction.Walk, 7);
        Assert.Equal(42.5f, cs.X);
        Assert.Equal(10f, cs.Y);
        Assert.Equal("north", cs.Direction);
        Assert.Equal(PlayerAction.Walk, cs.Action);
        Assert.Equal(7, cs.CurrentFrame);
    }
}
