using System.Text.Json;
using System.Text.Json.Serialization;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class DiscoveryBatchService(AppDbContext db)
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task<UnreviewedCountDto> GetUnreviewedCountAsync()
    {
        var reviewedSessionIds = await GetAllBatchedSessionIdsAsync();
        var total = await db.LearningSessions.CountAsync();
        var reviewed = reviewedSessionIds.Count;
        return new UnreviewedCountDto { UnreviewedSessions = Math.Max(0, total - reviewed) };
    }

    public async Task<CreateBatchResponseDto> CreateBatchAsync(int maxSessions)
    {
        var batchedIds = await GetAllBatchedSessionIdsAsync();

        var sessions = await db.LearningSessions
            .Where(s => !batchedIds.Contains(s.SessionId))
            .OrderBy(s => s.CreatedAt)
            .Take(maxSessions)
            .Select(s => s.SessionId)
            .ToListAsync();

        var batchNumber = (await db.DiscoveryBatches.CountAsync()) + 1;
        var batchId = $"batch-{batchNumber:D3}";
        var now = DateTime.UtcNow.AddHours(7);

        db.DiscoveryBatches.Add(new DiscoveryBatchEntity
        {
            BatchId = batchId,
            CreatedAt = now,
            Status = "draft",
            SessionIdsJson = JsonSerializer.Serialize(sessions),
            NotesJson = JsonSerializer.Serialize(new DiscoveryNotes(), _opts)
        });
        await db.SaveChangesAsync();

        return new CreateBatchResponseDto
        {
            BatchId = batchId,
            SessionCount = sessions.Count,
            Status = "draft"
        };
    }

    public async Task<List<BatchSummaryDto>> ListBatchesAsync()
    {
        var batches = await db.DiscoveryBatches
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync();

        return batches.Select(ToSummaryDto).ToList();
    }

    public async Task<BatchDetailDto?> GetBatchAsync(string batchId)
    {
        var entity = await db.DiscoveryBatches.FindAsync(batchId);
        if (entity is null) return null;

        var dto = ToSummaryDto(entity);
        var sessionIds = JsonSerializer.Deserialize<List<string>>(entity.SessionIdsJson) ?? [];
        return new BatchDetailDto
        {
            BatchId = dto.BatchId,
            CreatedAt = dto.CreatedAt,
            ReviewedAt = dto.ReviewedAt,
            Status = dto.Status,
            SessionCount = dto.SessionCount,
            Notes = dto.Notes,
            SessionIds = sessionIds
        };
    }

    public async Task<object?> ExportBatchAsync(string batchId)
    {
        var entity = await db.DiscoveryBatches.FindAsync(batchId);
        if (entity is null) return null;

        // For imported batches, return the original uploaded JSON
        if (entity.BatchType == "Imported" && entity.SourceJson is not null)
            return JsonSerializer.Deserialize<JsonElement>(entity.SourceJson);

        var sessionIds = JsonSerializer.Deserialize<List<string>>(entity.SessionIdsJson) ?? [];

        var sessions = await db.LearningSessions
            .Where(s => sessionIds.Contains(s.SessionId))
            .ToListAsync();

        var sessionDocs = sessions
            .Select(s => JsonSerializer.Deserialize<JsonElement>(s.SessionJson))
            .ToList();

        return new
        {
            exportedAt = DateTime.UtcNow.AddHours(7),
            version = "Demo V1.10",
            batchId,
            sessionCount = sessions.Count,
            analysisPrompt = AnalysisPromptText,
            sessions = sessionDocs
        };
    }

    private static BatchMetrics CalculateSummary(JsonElement root)
    {
        var metrics = new BatchMetrics();
        if (!root.TryGetProperty("sessions", out var sessionsEl)) return metrics;

        var sessions = sessionsEl.EnumerateArray().ToList();
        metrics.TotalSessions = sessions.Count;

        var studentCounts = new Dictionary<string, int>();
        var deviceCounts  = new Dictionary<string, int>();
        var abandonedLessons = new Dictionary<string, int>();
        var abandonedSteps   = new Dictionary<string, int>();

        foreach (var s in sessions)
        {
            bool completed = false;
            if (s.TryGetProperty("completed", out var cEl)) completed = cEl.GetBoolean();
            if (completed) metrics.CompletedSessions++;

            if (s.TryGetProperty("summary", out var sum))
            {
                if (sum.TryGetProperty("hintUsed",        out var h))  metrics.HintCount        += h.GetInt32();
                if (sum.TryGetProperty("helpMeStartUsed", out var hms)) metrics.HelpMeStartCount += hms.GetInt32();
                if (sum.TryGetProperty("exampleUsed",     out var ex))  metrics.WorkedExampleCount += ex.GetInt32();
            }

            bool abandoned = false;
            int abandonedAtStep = 0;
            if (s.TryGetProperty("events", out var evtsEl))
            {
                var events = evtsEl.EnumerateArray().ToList();
                abandoned = events.Any(e =>
                    e.TryGetProperty("type", out var t) && t.GetString() == "session_abandoned");

                if (abandoned)
                {
                    // Count step_started events before session_abandoned (ordered by timestamp)
                    var orderedEvents = events
                        .Where(e => e.TryGetProperty("timestamp", out _))
                        .OrderBy(e => e.GetProperty("timestamp").GetString())
                        .ToList();

                    int stepCount = 0;
                    foreach (var ev in orderedEvents)
                    {
                        if (!ev.TryGetProperty("type", out var evType)) continue;
                        var evTypStr = evType.GetString() ?? "";
                        if (evTypStr == "session_abandoned") break;
                        if (evTypStr == "step_started") stepCount++;
                    }
                    abandonedAtStep = stepCount;
                }
            }

            if (abandoned)
            {
                metrics.AbandonedCount++;
                if (s.TryGetProperty("topic", out var topicEl))
                {
                    var topic = topicEl.GetString() ?? "";
                    abandonedLessons.TryGetValue(topic, out var cnt);
                    abandonedLessons[topic] = cnt + 1;
                }
                if (abandonedAtStep > 0)
                {
                    var stepKey = $"Step {abandonedAtStep}";
                    abandonedSteps.TryGetValue(stepKey, out var sc);
                    abandonedSteps[stepKey] = sc + 1;
                }
            }

            if (s.TryGetProperty("studentId", out var sidEl) && sidEl.ValueKind != JsonValueKind.Null)
            {
                var sid = sidEl.GetString() ?? "";
                if (!string.IsNullOrEmpty(sid))
                {
                    studentCounts.TryGetValue(sid, out var sc);
                    studentCounts[sid] = sc + 1;
                }
            }

            if (s.TryGetProperty("deviceId", out var didEl) && didEl.ValueKind != JsonValueKind.Null)
            {
                var did = didEl.GetString() ?? "";
                if (!string.IsNullOrEmpty(did))
                {
                    deviceCounts.TryGetValue(did, out var dc);
                    deviceCounts[did] = dc + 1;
                }
            }
        }

        int total = metrics.TotalSessions;
        metrics.IncompleteSessions = total - metrics.CompletedSessions;
        metrics.CompletionRate     = total > 0 ? Math.Round((double)metrics.CompletedSessions / total * 100, 1) : 0;
        metrics.AbandonmentRate    = total > 0 ? Math.Round((double)metrics.AbandonedCount    / total * 100, 1) : 0;
        metrics.HintRate           = total > 0 ? Math.Round((double)metrics.HintCount         / total * 100, 1) : 0;
        metrics.HelpMeStartRate    = total > 0 ? Math.Round((double)metrics.HelpMeStartCount  / total * 100, 1) : 0;
        metrics.WorkedExampleRate  = total > 0 ? Math.Round((double)metrics.WorkedExampleCount/ total * 100, 1) : 0;
        metrics.MostAbandonedLesson = abandonedLessons.Count > 0
            ? abandonedLessons.OrderByDescending(kv => kv.Value).First().Key : string.Empty;
        metrics.MostAbandonedStep = abandonedSteps.Count > 0
            ? abandonedSteps.OrderByDescending(kv => kv.Value).First().Key : string.Empty;

        metrics.UniqueStudents      = studentCounts.Count;
        metrics.ReturningStudents   = studentCounts.Count(kv => kv.Value > 1);
        metrics.AvgSessionsPerStudent = metrics.UniqueStudents > 0
            ? Math.Round((double)total / metrics.UniqueStudents, 1) : 0;

        metrics.UniqueDevices    = deviceCounts.Count;
        metrics.ReturningDevices = deviceCounts.Count(kv => kv.Value > 1);
        metrics.AvgSessionsPerDevice = metrics.UniqueDevices > 0
            ? Math.Round((double)total / metrics.UniqueDevices, 1) : 0;

        return metrics;
    }

    public async Task<ImportBatchResponse> ImportBatchAsync(string rawJson)
    {
        // Parse uploaded JSON — expect { sessions: [ { sessionId, studentId, startedAt, ... }, ... ] }
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;

        var uploadedIds = new List<string>();
        var uploadedStudentIds = new HashSet<string>();
        var uploadedDates = new HashSet<string>();

        if (root.TryGetProperty("sessions", out var sessionsEl))
        {
            foreach (var s in sessionsEl.EnumerateArray())
            {
                if (s.TryGetProperty("sessionId", out var idEl))
                    uploadedIds.Add(idEl.GetString() ?? "");
                if (s.TryGetProperty("studentId", out var sidEl) && sidEl.ValueKind != JsonValueKind.Null)
                    uploadedStudentIds.Add(sidEl.GetString() ?? "");
                if (s.TryGetProperty("startedAt", out var dateEl) && dateEl.ValueKind != JsonValueKind.Null)
                    uploadedDates.Add(dateEl.GetString()?.Substring(0, 10) ?? ""); // date only
            }
        }

        // Duplicate detection: check sessionId against existing batches
        var allBatches = await db.DiscoveryBatches.ToListAsync();
        var batchIdMap = new Dictionary<string, string>(); // sessionId → batchId
        foreach (var b in allBatches)
        {
            var ids = JsonSerializer.Deserialize<List<string>>(b.SessionIdsJson) ?? [];
            foreach (var id in ids) batchIdMap.TryAdd(id, b.BatchId);
        }

        var duplicates = uploadedIds.Count(id => batchIdMap.ContainsKey(id));
        string duplicateStatus = duplicates == 0 ? "NewBatch"
            : duplicates == uploadedIds.Count ? "AlreadyReviewed"
            : "PartiallyImported";

        // Find which batch most duplicates belong to
        var dupBatchRef = uploadedIds
            .Where(id => batchIdMap.ContainsKey(id))
            .Select(id => batchIdMap[id])
            .GroupBy(b => b)
            .OrderByDescending(g => g.Count())
            .FirstOrDefault()?.Key ?? string.Empty;

        // Calculate batch summary metrics
        var summary = CalculateSummary(root);
        var summaryJson = JsonSerializer.Serialize(summary, _opts);

        // Create imported batch
        var batchNumber = (await db.DiscoveryBatches.CountAsync()) + 1;
        var batchId = $"batch-{batchNumber:D3}";
        var now = DateTime.UtcNow.AddHours(7);

        db.DiscoveryBatches.Add(new DiscoveryBatchEntity
        {
            BatchId = batchId,
            CreatedAt = now,
            Status = "draft",
            BatchType = "Imported",
            Source = "Upload",
            AnalysisStatus = "not_analyzed",
            SourceJson = rawJson,
            BatchSummaryJson = summaryJson,
            SessionIdsJson = JsonSerializer.Serialize(uploadedIds),
            NotesJson = JsonSerializer.Serialize(new DiscoveryNotes(), _opts)
        });
        await db.SaveChangesAsync();

        return new ImportBatchResponse
        {
            BatchId = batchId,
            BatchType = "Imported",
            Source = "Upload",
            ImportedAt = now,
            SessionCount = uploadedIds.Count,
            DuplicateStatus = duplicateStatus,
            DuplicateCount = duplicates,
            DuplicateBatchRef = dupBatchRef,
            Summary = summary
        };
    }

    public async Task<bool> UpdateNotesAsync(string batchId, UpdateNotesRequest req)
    {
        var entity = await db.DiscoveryBatches.FindAsync(batchId);
        if (entity is null) return false;

        var notes = new DiscoveryNotes
        {
            KeyObservations = req.KeyObservations ?? string.Empty,
            ValidatedDiscoveries = req.ValidatedDiscoveries ?? string.Empty,
            UnconfirmedSignals = req.UnconfirmedSignals ?? string.Empty,
            ProductDecisions = req.ProductDecisions ?? string.Empty,
            NextQuestions = req.NextQuestions ?? string.Empty
        };

        entity.NotesJson = JsonSerializer.Serialize(notes, _opts);
        if (entity.AnalysisStatus == "not_analyzed")
            entity.AnalysisStatus = "analysis_generated";
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkReviewedAsync(string batchId)
    {
        var entity = await db.DiscoveryBatches.FindAsync(batchId);
        if (entity is null) return false;

        entity.Status = "reviewed";
        entity.AnalysisStatus = "reviewed";
        entity.ReviewedAt = DateTime.UtcNow.AddHours(7);
        await db.SaveChangesAsync();
        return true;
    }

    private async Task<HashSet<string>> GetAllBatchedSessionIdsAsync()
    {
        var allBatches = await db.DiscoveryBatches
            .Select(b => b.SessionIdsJson)
            .ToListAsync();

        var ids = new HashSet<string>();
        foreach (var json in allBatches)
        {
            var list = JsonSerializer.Deserialize<List<string>>(json) ?? [];
            foreach (var id in list) ids.Add(id);
        }
        return ids;
    }

    private static BatchSummaryDto ToSummaryDto(DiscoveryBatchEntity entity)
    {
        var sessionIds = JsonSerializer.Deserialize<List<string>>(entity.SessionIdsJson) ?? [];
        var notes = JsonSerializer.Deserialize<DiscoveryNotes>(entity.NotesJson) ?? new DiscoveryNotes();
        BatchMetrics? summary = null;
        if (entity.BatchSummaryJson is not null)
            summary = JsonSerializer.Deserialize<BatchMetrics>(entity.BatchSummaryJson, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });

        var notesHaveContent = !string.IsNullOrWhiteSpace(notes.KeyObservations)
            || !string.IsNullOrWhiteSpace(notes.ValidatedDiscoveries)
            || !string.IsNullOrWhiteSpace(notes.UnconfirmedSignals)
            || !string.IsNullOrWhiteSpace(notes.ProductDecisions)
            || !string.IsNullOrWhiteSpace(notes.NextQuestions);

        var discoveryStatus = entity.Status == "reviewed" ? "reviewed"
            : notesHaveContent ? "discovery_draft"
            : "not_analyzed";

        return new BatchSummaryDto
        {
            BatchId = entity.BatchId,
            CreatedAt = entity.CreatedAt,
            ReviewedAt = entity.ReviewedAt,
            Status = entity.Status,
            DiscoveryStatus = discoveryStatus,
            BatchType = entity.BatchType,
            AnalysisStatus = entity.AnalysisStatus,
            SessionCount = sessionIds.Count,
            Notes = notes,
            Summary = summary
        };
    }

    public const string AnalysisPromptText = """
        You are a product discovery analyst for an AI Math Tutor application.

        Analyze the learning session batch below and produce a structured discovery report.

        Follow this exact output structure:

        ---

        ## 1. Batch Summary (Quantitative Metrics)

        Calculate and report:

        Session Metrics:
        - Total Sessions
        - Completed Sessions
        - Incomplete Sessions
        - Completion Rate (%)

        Help Usage Metrics:
        - Hint Usage Count and Rate (%)
        - Help Me Start Usage Count and Rate (%)
        - Worked Example Usage Count and Rate (%)

        Learning Friction Metrics:
        - Session Abandoned Count and Rate (%)
        - Most Abandoned Lesson (if data exists)
        - Most Abandoned Step (if data exists)

        Student & Device Metrics:
        - Returning Students vs New Students (if studentId exists)
        - Average Sessions Per Student
        - Returning Devices vs New Devices (if deviceId exists)

        ---

        ## 2. Evidence

        List only facts observable from the logs.
        Format: "X out of Y sessions [did something]."
        Do NOT make interpretations yet.

        ---

        ## 3. Key Observations

        Based on evidence, describe what you observe.
        Keep observations descriptive. Avoid conclusions.

        ---

        ## 4. Unconfirmed Signals

        Generate hypotheses that the evidence suggests but does not yet prove.
        Label clearly as signals, not discoveries.

        ---

        ## 5. Validated Discoveries

        Only include findings that are:
        - Supported by multiple sessions
        - Observable from logs
        - Repeatable across sessions

        Each discovery must reference supporting evidence.
        Avoid strong conclusions from small samples (fewer than 5 sessions).

        ---

        ## 6. Product Decisions

        Suggest specific, MVP-focused actions.
        Each decision must be grounded in evidence.

        Do NOT recommend:
        - Multi-Agent Systems
        - Advanced Analytics
        - Gamification
        - RAG Expansion
        - Complex Personalization

        unless directly supported by evidence in this batch.

        ---

        ## 7. Next Questions

        List unanswered questions this batch raises.
        These will guide the next batch collection.

        ---

        Principle: Raw Logs → Evidence → Discovery → Product Decisions → MVP
        Never skip directly from ideas to features.
        """;

}
