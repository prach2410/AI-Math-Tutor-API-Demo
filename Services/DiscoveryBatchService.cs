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
        var now = DateTime.UtcNow;

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
            exportedAt = DateTime.UtcNow,
            version = "Demo V1.10",
            batchId,
            sessionCount = sessions.Count,
            analysisPrompt = AnalysisPromptText,
            sessions = sessionDocs
        };
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
        var batchedIds = await GetAllBatchedSessionIdsAsync();
        var duplicates = uploadedIds.Count(id => batchedIds.Contains(id));

        string duplicateStatus = duplicates == 0 ? "NewBatch"
            : duplicates == uploadedIds.Count ? "AlreadyReviewed"
            : "PartiallyImported";

        // Create imported batch
        var batchNumber = (await db.DiscoveryBatches.CountAsync()) + 1;
        var batchId = $"batch-{batchNumber:D3}";
        var now = DateTime.UtcNow;

        db.DiscoveryBatches.Add(new DiscoveryBatchEntity
        {
            BatchId = batchId,
            CreatedAt = now,
            Status = "draft",
            BatchType = "Imported",
            Source = "Upload",
            SourceJson = rawJson,
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
            DuplicateCount = duplicates
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
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> MarkReviewedAsync(string batchId)
    {
        var entity = await db.DiscoveryBatches.FindAsync(batchId);
        if (entity is null) return false;

        entity.Status = "reviewed";
        entity.ReviewedAt = DateTime.UtcNow;
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
        return new BatchSummaryDto
        {
            BatchId = entity.BatchId,
            CreatedAt = entity.CreatedAt,
            ReviewedAt = entity.ReviewedAt,
            Status = entity.Status,
            BatchType = entity.BatchType,
            SessionCount = sessionIds.Count,
            Notes = notes
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
