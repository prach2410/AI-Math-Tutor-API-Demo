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
    string CreatedAt,
    string DownloadedAt = "",
    string Reflection = "",
    string VisionModel = "",
    string AnalysisStartedAt = "",
    string AnalysisEndedAt = ""
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

    public async Task<(string Id, string Date)?> ExistsByHashAsync(string hash)
    {
        if (string.IsNullOrWhiteSpace(hash)) return null;
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT Id, Date FROM LearningRecords WHERE ImageHash = $hash LIMIT 1";
        cmd.Parameters.AddWithValue("$hash", hash);
        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;
        return (reader.GetString(0), reader.GetString(1));
    }

    public async Task<string> SaveAsync(LearningJournalAnalysis analysis, string imageHash = "")
    {
        var id        = Guid.NewGuid().ToString();
        var date      = DateTime.UtcNow.ToString("yyyy-MM-dd");
        var createdAt = DateTime.UtcNow.ToString("O");
        var kw        = JsonSerializer.Serialize(analysis.Keywords);
        var hl        = JsonSerializer.Serialize(analysis.Highlights);

        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            INSERT INTO LearningRecords
                (Id, Date, DocumentType, Topic, Summary, HighlightsJson, KeywordsJson, CreatedAt, ImageHash,
                 VisionModel, AnalysisStartedAt, AnalysisEndedAt)
            VALUES ($id, $date, $docType, $topic, $summary, $hl, $kw, $createdAt, $hash,
                    $visionModel, $startedAt, $endedAt)
            """;
        cmd.Parameters.AddWithValue("$id",          id);
        cmd.Parameters.AddWithValue("$date",        date);
        cmd.Parameters.AddWithValue("$docType",     analysis.DocumentType);
        cmd.Parameters.AddWithValue("$topic",       analysis.Topic);
        cmd.Parameters.AddWithValue("$summary",     analysis.Summary);
        cmd.Parameters.AddWithValue("$hl",          hl);
        cmd.Parameters.AddWithValue("$kw",          kw);
        cmd.Parameters.AddWithValue("$createdAt",   createdAt);
        cmd.Parameters.AddWithValue("$hash",        imageHash);
        cmd.Parameters.AddWithValue("$visionModel", analysis.VisionModel);
        cmd.Parameters.AddWithValue("$startedAt",   analysis.StartedAt);
        cmd.Parameters.AddWithValue("$endedAt",     analysis.EndedAt);
        await cmd.ExecuteNonQueryAsync();
        return id;
    }

    public async Task SetReflectionAsync(string id, string reflection)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = "UPDATE LearningRecords SET Reflection = $reflection WHERE Id = $id";
        cmd.Parameters.AddWithValue("$reflection", reflection);
        cmd.Parameters.AddWithValue("$id", id);
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

    public async Task<List<LearningRecordEntry>> GetByDateRangeAsync(string start, string end)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Date, DocumentType, Topic, Summary, KeywordsJson, CreatedAt, DownloadedAt, Reflection,
                   VisionModel, AnalysisStartedAt, AnalysisEndedAt
            FROM   LearningRecords
            WHERE  Date >= $start AND Date <= $end
            ORDER  BY Date ASC, CreatedAt ASC
            """;
        cmd.Parameters.AddWithValue("$start", start);
        cmd.Parameters.AddWithValue("$end",   end);

        var entries = new List<LearningRecordEntry>();
        using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            List<string> kw;
            try { kw = JsonSerializer.Deserialize<List<string>>(reader.GetString(5)) ?? []; }
            catch { kw = []; }

            entries.Add(new LearningRecordEntry(
                Id:               reader.GetString(0),
                Date:             reader.GetString(1),
                DocumentType:     reader.GetString(2),
                Topic:            reader.GetString(3),
                Summary:          reader.GetString(4),
                Keywords:         kw,
                CreatedAt:        reader.GetString(6),
                DownloadedAt:     reader.IsDBNull(7)  ? "" : reader.GetString(7),
                Reflection:       reader.IsDBNull(8)  ? "" : reader.GetString(8),
                VisionModel:      reader.IsDBNull(9)  ? "" : reader.GetString(9),
                AnalysisStartedAt:reader.IsDBNull(10) ? "" : reader.GetString(10),
                AnalysisEndedAt:  reader.IsDBNull(11) ? "" : reader.GetString(11)
            ));
        }
        return entries;
    }

    public async Task<List<LearningRecordEntry>> GetByDateAsync(string date)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Id, Date, DocumentType, Topic, Summary, KeywordsJson, CreatedAt
            FROM   LearningRecords
            WHERE  Date = $date
            ORDER  BY CreatedAt DESC
            """;
        cmd.Parameters.AddWithValue("$date", date);

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
        return entries;
    }

    public async Task<(string Markdown, string Filename)?> ExportMarkdownAsync(string id)
    {
        using var conn = new SqliteConnection($"Data Source={_dbPath}");
        await conn.OpenAsync();
        using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT Date, DocumentType, Topic, Summary, HighlightsJson, KeywordsJson, Reflection,
                   VisionModel, AnalysisStartedAt, AnalysisEndedAt
            FROM   LearningRecords
            WHERE  Id = $id
            """;
        cmd.Parameters.AddWithValue("$id", id);

        using var reader = await cmd.ExecuteReaderAsync();
        if (!await reader.ReadAsync()) return null;

        var date        = reader.GetString(0);
        var docType     = reader.GetString(1);
        var topic       = reader.GetString(2);
        var summary     = reader.GetString(3);
        var reflection  = reader.IsDBNull(6) ? "" : reader.GetString(6);
        var visionModel = reader.IsDBNull(7) ? "" : reader.GetString(7);
        var startedAt   = reader.IsDBNull(8) ? "" : reader.GetString(8);
        var endedAt     = reader.IsDBNull(9) ? "" : reader.GetString(9);

        List<string> highlights, keywords;
        try { highlights = JsonSerializer.Deserialize<List<string>>(reader.GetString(4)) ?? []; } catch { highlights = []; }
        try { keywords   = JsonSerializer.Deserialize<List<string>>(reader.GetString(5)) ?? []; } catch { keywords   = []; }

        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {date}");
        sb.AppendLine();
        sb.AppendLine("## ประเภทเอกสาร");
        sb.AppendLine(docType);
        sb.AppendLine();
        sb.AppendLine("## หัวข้อ");
        sb.AppendLine(topic);
        sb.AppendLine();
        sb.AppendLine("## สรุปเนื้อหา");
        sb.AppendLine(summary);
        if (highlights.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## สิ่งสำคัญ");
            foreach (var h in highlights) sb.AppendLine($"- {h}");
        }
        if (keywords.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## คำสำคัญ");
            foreach (var k in keywords) sb.AppendLine($"- {k}");
        }
        if (!string.IsNullOrWhiteSpace(reflection))
        {
            var reflectionLabel = reflection switch
            {
                "Understood"           => "🟢 อ๋อ เข้าใจแล้ว",
                "StartingToUnderstand" => "🟡 เริ่มเข้าใจแล้ว",
                "StillConfused"        => "🟠 ยังงงอยู่",
                "NotUnderstand"        => "🔴 ไม่เข้าใจเลย",
                _ => reflection,
            };
            sb.AppendLine();
            sb.AppendLine("## ความรู้สึกหลังเรียน");
            sb.AppendLine(reflectionLabel);
        }

        if (!string.IsNullOrWhiteSpace(visionModel))
        {
            var durationStr = "";
            if (DateTime.TryParse(startedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var s) &&
                DateTime.TryParse(endedAt,   null, System.Globalization.DateTimeStyles.RoundtripKind, out var e))
                durationStr = $" · ใช้เวลา: {(e - s).TotalSeconds:F1}s";
            sb.AppendLine();
            sb.AppendLine($"---");
            sb.AppendLine($"วิเคราะห์ด้วย: {visionModel}{durationStr}");
        }

        var safeTopic = string.Concat(topic.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var filename  = $"{date}_{safeTopic}.md";

        // Mark as downloaded
        using var updateConn = new SqliteConnection($"Data Source={_dbPath}");
        await updateConn.OpenAsync();
        using var updateCmd = updateConn.CreateCommand();
        updateCmd.CommandText = "UPDATE LearningRecords SET DownloadedAt = $now WHERE Id = $id";
        updateCmd.Parameters.AddWithValue("$now", DateTime.UtcNow.ToString("O"));
        updateCmd.Parameters.AddWithValue("$id",  id);
        await updateCmd.ExecuteNonQueryAsync();

        return (sb.ToString(), filename);
    }
}
