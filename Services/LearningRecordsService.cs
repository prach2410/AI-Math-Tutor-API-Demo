using Microsoft.Data.Sqlite;
using System.Text.Json;

namespace backend.Services;

public record LearningRecordEntry(
    string Id,
    string Date,
    string DocumentType,
    string Topic,
    string Summary,
    List<string> Keywords,
    string CreatedAt
);

public record DailyLogGroup(
    string Date,
    List<LearningRecordEntry> Records
);

public class LearningRecordsService(IConfiguration config)
{
    private readonly string _dbPath =
        config.GetValue<string>("DatabasePath")
        ?? Environment.GetEnvironmentVariable("DatabasePath")
        ?? "learning_sessions.db";

    public async Task SaveAsync(LearningJournalAnalysis analysis)
    {
        var id          = Guid.NewGuid().ToString();
        var date        = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var createdAt   = DateTime.UtcNow.ToString("O");
        var kw          = JsonSerializer.Serialize(analysis.Keywords);
        var hl          = JsonSerializer.Serialize(analysis.Highlights);

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO LearningRecords
                (Id, Date, DocumentType, Topic, Summary, HighlightsJson, KeywordsJson, CreatedAt)
            VALUES ($id, $date, $docType, $topic, $summary, $hl, $kw, $createdAt)
            """;
        cmd.Parameters.AddWithValue("$id",        id);
        cmd.Parameters.AddWithValue("$date",      date);
        cmd.Parameters.AddWithValue("$docType",   analysis.DocumentType);
        cmd.Parameters.AddWithValue("$topic",     analysis.Topic);
        cmd.Parameters.AddWithValue("$summary",   analysis.Summary);
        cmd.Parameters.AddWithValue("$hl",        hl);
        cmd.Parameters.AddWithValue("$kw",        kw);
        cmd.Parameters.AddWithValue("$createdAt", createdAt);
        await cmd.ExecuteNonQueryAsync();
    }

    public async Task<List<DailyLogGroup>> GetTimelineAsync()
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Date, DocumentType, Topic, Summary, KeywordsJson, CreatedAt
            FROM   LearningRecords
            ORDER  BY CreatedAt DESC
            """;

        var entries = new List<LearningRecordEntry>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            List<string> kw;
            try { kw = JsonSerializer.Deserialize<List<string>>(reader.GetString(5)) ?? []; }
            catch { kw = []; }

            entries.Add(new LearningRecordEntry(
                Id:           reader.GetString(0),
                Date:         reader.GetString(1),
                DocumentType: reader.GetString(2),
                Topic:        reader.GetString(3),
                Summary:      reader.GetString(4),
                Keywords:     kw,
                CreatedAt:    reader.GetString(6)
            ));
        }

        return entries
            .GroupBy(e => e.Date)
            .OrderByDescending(g => g.Key)
            .Select(g => new DailyLogGroup(g.Key, g.ToList()))
            .ToList();
    }
}
