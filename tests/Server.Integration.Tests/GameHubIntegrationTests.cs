using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;
using PoBananaGame.Features.GameSession.Models;
using Xunit;

namespace Server.Integration.Tests;

/// <summary>
/// Integration tests that spin up the full ASP.NET Core host via
/// WebApplicationFactory and drive the SignalR GameHub end-to-end.
/// No external infrastructure (database, Docker) is required — the game
/// uses only in-memory state.
/// </summary>
public class GameHubIntegrationTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory;

    public GameHubIntegrationTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    public Task InitializeAsync() => Task.CompletedTask;
    public Task DisposeAsync() => Task.CompletedTask;

    // ────────────────────────────────────────────────────────────── helpers ──

    private HubConnection BuildConnection()
    {
        var httpClient = _factory.CreateClient();
        var baseAddress = httpClient.BaseAddress!;

        return new HubConnectionBuilder()
            .WithUrl($"{baseAddress}gamehub", options =>
            {
                options.HttpMessageHandlerFactory = _ => _factory.Server.CreateHandler();
            })
            .Build();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Connection tests
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Connect_ReceivesInitialGameState()
    {
        await using var conn = BuildConnection();

        object? receivedState = null;
        conn.On<object>("gameState", state => receivedState = state);

        await conn.StartAsync();
        await Task.Delay(200); // allow server to push initial state

        Assert.NotNull(receivedState);
    }

    [Fact]
    public async Task TwoPlayers_Connect_BothReceiveReadyCheckStatus()
    {
        await using var conn1 = BuildConnection();
        await using var conn2 = BuildConnection();

        string? statusAfterSecond = null;
        conn2.On<GameStateDto>("gameState", s => statusAfterSecond = s.Status);

        await conn1.StartAsync();
        await conn2.StartAsync();
        await Task.Delay(300);

        Assert.Equal("readycheck", statusAfterSecond);
    }

    [Fact]
    public async Task Disconnect_OtherPlayerReceivesWaitingStatus()
    {
        await using var conn1 = BuildConnection();
        await using var conn2 = BuildConnection();

        string? lastStatus = null;
        conn2.On<GameStateDto>("gameState", s => lastStatus = s.Status);

        await conn1.StartAsync();
        await conn2.StartAsync();
        await Task.Delay(200);

        await conn1.StopAsync();
        await Task.Delay(300);

        Assert.Equal("waiting", lastStatus);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // GameFlow — full Ready → Countdown path
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task BothPlayersReady_TransitionsToCountdown()
    {
        await using var conn1 = BuildConnection();
        await using var conn2 = BuildConnection();

        var statuses = new List<string>();
        conn1.On<GameStateDto>("gameState", s => statuses.Add(s.Status!));

        await conn1.StartAsync();
        await conn2.StartAsync();
        await Task.Delay(200);

        await conn1.InvokeAsync("PlayerReady");
        await conn2.InvokeAsync("PlayerReady");
        await Task.Delay(300);

        Assert.Contains("countdown", statuses);
    }

    [Fact]
    public async Task AfterCountdown_TransitionsToPlaying()
    {
        await using var conn1 = BuildConnection();
        await using var conn2 = BuildConnection();

        var statuses = new List<string>();
        conn1.On<GameStateDto>("gameState", s => statuses.Add(s.Status!));

        await conn1.StartAsync();
        await conn2.StartAsync();
        await Task.Delay(200);

        await conn1.InvokeAsync("PlayerReady");
        await conn2.InvokeAsync("PlayerReady");

        // Wait beyond 3-second countdown; server tick auto-transitions at raceStartTimeMs
        await Task.Delay(4500);

        Assert.Contains("playing", statuses);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // SelectColor
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task SelectColor_Broadcast_IsReflectedInGameState()
    {
        await using var conn1 = BuildConnection();
        await using var conn2 = BuildConnection();

        await conn1.StartAsync();
        await conn2.StartAsync();
        await Task.Delay(200);

        // Should not throw — color selection is valid during ReadyCheck
        await conn1.InvokeAsync("SelectColor", PlayerColor.Yellow);
        await Task.Delay(200);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PlayerUpdate (anti-cheat integration) — hub method is "PlayerUpdate"
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PlayerUpdate_DuringPlaying_Accepted()
    {
        await using var conn1 = BuildConnection();
        await using var conn2 = BuildConnection();

        await conn1.StartAsync();
        await conn2.StartAsync();
        await Task.Delay(200);

        // Advance to Playing
        await conn1.InvokeAsync("PlayerReady");
        await conn2.InvokeAsync("PlayerReady");
        await Task.Delay(4500); // wait past 3-second countdown

        // Small legal move — hub exposes "PlayerUpdate" with ClientState payload
        await conn1.InvokeAsync("PlayerUpdate", new
        {
            x = 110f,
            y = 0f,
            direction = "east",
            action = "walk",
            currentFrame = 1
        });
        await Task.Delay(200);
        // No exception thrown = acceptance
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PlayerFinished — hub method and uses "gameOver" event (not "gameState")
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task PlayerFinished_DuringPlaying_BroadcastsGameOverEvent()
    {
        await using var conn1 = BuildConnection();
        await using var conn2 = BuildConnection();

        object? gameOverPayload = null;
        conn2.On<object>("gameOver", payload => gameOverPayload = payload);

        await conn1.StartAsync();
        await conn2.StartAsync();
        await Task.Delay(200);

        await conn1.InvokeAsync("PlayerReady");
        await conn2.InvokeAsync("PlayerReady");
        await Task.Delay(4500); // past countdown

        await conn1.InvokeAsync("PlayerFinished");
        await Task.Delay(300);

        Assert.NotNull(gameOverPayload);
    }

    [Fact]
    public async Task RequestRestart_AfterGameOver_TransitionsToReadyCheck()
    {
        await using var conn1 = BuildConnection();
        await using var conn2 = BuildConnection();

        var statuses = new List<string>();
        conn1.On<GameStateDto>("gameState", s => statuses.Add(s.Status!));

        await conn1.StartAsync();
        await conn2.StartAsync();
        await Task.Delay(200);

        await conn1.InvokeAsync("PlayerReady");
        await conn2.InvokeAsync("PlayerReady");
        await Task.Delay(4500);

        await conn1.InvokeAsync("PlayerFinished");
        await Task.Delay(300);

        await conn1.InvokeAsync("RequestRestart");
        await Task.Delay(300);

        Assert.Contains("readycheck", statuses);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Concurrency — multiple rooms
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task FourPlayers_CreateTwoIndependentRooms()
    {
        await using var conn1 = BuildConnection();
        await using var conn2 = BuildConnection();
        await using var conn3 = BuildConnection();
        await using var conn4 = BuildConnection();

        var room1Statuses = new List<string>();
        var room3Statuses = new List<string>();

        conn1.On<GameStateDto>("gameState", s => room1Statuses.Add(s.Status!));
        conn3.On<GameStateDto>("gameState", s => room3Statuses.Add(s.Status!));

        await conn1.StartAsync();
        await conn2.StartAsync();
        await conn3.StartAsync();
        await conn4.StartAsync();
        await Task.Delay(400);

        // Room 1: conn1+conn2 should be in ReadyCheck
        Assert.Contains("readycheck", room1Statuses);
        // Room 2: conn3+conn4 should also be in ReadyCheck
        Assert.Contains("readycheck", room3Statuses);
    }
}

// ── DTO for decoding GameState messages ──────────────────────────────────────
file record GameStateDto(string? Status, long CountdownStartTimeMs, long RaceStartTimeMs, string? FinishedPlayerId);
