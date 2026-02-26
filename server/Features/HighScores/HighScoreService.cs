using Azure;
using Azure.Data.Tables;
using Azure.Identity;

namespace PoBananaGame.Features.HighScores;

/// <summary>Azure Table Storage entity representing one logged race finish time.</summary>
public class HighScoreEntity : ITableEntity
{
    public string PartitionKey { get; set; } = "banana";

    /// <summary>Zero-padded TimeMs (15 digits) + unique suffix ensures lex order = ascending time.</summary>
    public string RowKey { get; set; } = "";

    public ETag ETag { get; set; } = ETag.All;
    public DateTimeOffset? Timestamp { get; set; }

    public long TimeMs { get; set; }
}

public record HighScoreEntry(int Rank, long TimeMs);

public interface IHighScoreService
{
    Task SaveScoreAsync(long timeMs);
    Task<List<HighScoreEntry>> GetTopScoresAsync();
}

public class HighScoreService : IHighScoreService
{
    private const string TableName = "highscores";
    private const string Partition = "banana";
    private const int MaxScores = 10;

    private readonly TableClient _table;
    private readonly ILogger<HighScoreService> _log;

    public HighScoreService(IConfiguration configuration, ILogger<HighScoreService> log)
    {
        _log = log;

        var tableEndpoint = configuration["AzureStorage:TableEndpoint"];
        var accountName   = configuration["AzureStorage:AccountName"];
        var accountKey    = configuration["AzureStorage:AccountKey"];

        TableServiceClient svc;

        if (!string.IsNullOrEmpty(accountKey) && !string.IsNullOrEmpty(accountName))
        {
            // Local development: use Azurite shared-key auth
            var credential = new TableSharedKeyCredential(accountName, accountKey);
            svc = new TableServiceClient(new Uri(tableEndpoint!), credential);
            _log.LogInformation("[HighScores] Using shared-key auth (local Azurite) → {endpoint}", tableEndpoint);
        }
        else
        {
            // Production: use Managed Identity via DefaultAzureCredential
            // The App Service system-assigned MI has Storage Table Data Contributor on the storage account
            svc = new TableServiceClient(new Uri(tableEndpoint!), new DefaultAzureCredential());
            _log.LogInformation("[HighScores] Using DefaultAzureCredential (Managed Identity) → {endpoint}", tableEndpoint);
        }

        _table = svc.GetTableClient(TableName);


        try
        {
            svc.CreateTableIfNotExists(TableName);
            _log.LogInformation("[HighScores] Table storage ready — {endpoint}", tableEndpoint);
        }
        catch (Exception ex)
        {
            _log.LogWarning("[HighScores] Could not connect to Table Storage on startup (scores will retry per-request): {msg}", ex.Message);
        }
    }

    public async Task SaveScoreAsync(long timeMs)
    {
        var rowKey = $"{timeMs:D15}_{Guid.NewGuid():N}";
        var entity = new HighScoreEntity { PartitionKey = Partition, RowKey = rowKey, TimeMs = timeMs };

        try
        {
            // Ensure table exists (idempotent — handles late Azurite startup)
            await _table.CreateIfNotExistsAsync();
            await _table.AddEntityAsync(entity);
        }
        catch (Exception ex)
        {
            _log.LogWarning("[HighScores] Could not save score: {msg}", ex.Message);
            return;
        }

        await TrimToTopAsync();
    }

    public async Task<List<HighScoreEntry>> GetTopScoresAsync()
    {
        var all = await FetchAllAsync();
        all.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
        return all.Take(MaxScores)
                  .Select((e, i) => new HighScoreEntry(i + 1, e.TimeMs))
                  .ToList();
    }

    private async Task TrimToTopAsync()
    {
        var all = await FetchAllAsync();
        if (all.Count <= MaxScores) return;

        all.Sort((a, b) => a.TimeMs.CompareTo(b.TimeMs));
        foreach (var del in all.Skip(MaxScores))
        {
            try { await _table.DeleteEntityAsync(del.PartitionKey, del.RowKey); }
            catch { /* ignore — concurrent delete is fine */ }
        }
    }

    private async Task<List<HighScoreEntity>> FetchAllAsync()
    {
        var results = new List<HighScoreEntity>();
        try
        {
            await foreach (var e in _table.QueryAsync<HighScoreEntity>(
                               filter: $"PartitionKey eq '{Partition}'"))
                results.Add(e);
        }
        catch (Exception ex)
        {
            _log.LogWarning("[HighScores] Could not fetch scores: {msg}", ex.Message);
        }
        return results;
    }
}
