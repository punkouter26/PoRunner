using PoBananaGame.Features.GameSession;
using PoBananaGame.Features.GameSession.State;
using PoBananaGame.Features.HighScores;
using System.Text.Json;
using System.Text.Json.Serialization;

var builder = WebApplication.CreateBuilder(args);

// ── Application Insights (connection string via Key Vault reference in App Service) ──
builder.Services.AddApplicationInsightsTelemetry();

// Add services to the container.
builder.Services.AddControllers();

// Register the IGameSessionManager as a Singleton
builder.Services.AddSingleton<IGameSessionManager, GameSessionManager>();

// Register HighScoreService (Azure Table Storage via Azurite in dev)
builder.Services.AddSingleton<IHighScoreService, HighScoreService>();

// Register the BackgroundService implicitly by using the same Singleton instance
builder.Services.AddHostedService(provider => (GameSessionManager)provider.GetRequiredService<IGameSessionManager>());

builder.Services.AddSignalR(options =>
{
    options.EnableDetailedErrors = true;
}).AddJsonProtocol(options =>
{
    // Serialize Enums as Strings (e.g. "Waiting", "Idle") instead of Integers (0, 1) to remain compatible with main.js strings
    options.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});

// Configure CORS for Vite dev server and production origin
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowVite",
        builder =>
        {
            builder.WithOrigins(
                       "http://localhost:5173", "http://127.0.0.1:5173",
                       "http://localhost:5174", "http://127.0.0.1:5174",
                       "https://wa-porunner.azurewebsites.net",
                       "https://victorious-ocean-06635bc1e.6.azurestaticapps.net")
                   .AllowAnyHeader()
                   .AllowAnyMethod()
                   .AllowCredentials(); // Required for SignalR
        });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
app.UseCors("AllowVite");

app.UseStaticFiles();
app.UseRouting();

app.UseAuthorization();

app.MapControllers();
app.MapHub<GameHub>("/gamehub");
app.MapFallbackToFile("index.html");

app.Run();

// Required by WebApplicationFactory<Program> in integration tests
public partial class Program { }
