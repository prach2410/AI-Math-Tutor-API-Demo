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
            analysisPrompt = AnalysisPrompt,
            sessions = sessionDocs
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
            SessionCount = sessionIds.Count,
            Notes = notes
        };
    }

    private const string AnalysisPrompt = """
        Analyze this AI Tutor Learning Session Batch.

        Please summarize:

        1. Where did students struggle?
        2. Which Teaching Actions helped most?
        3. Was Help Me Start useful?
        4. Was Worked Example useful?
        5. Did students complete the lessons?
        6. Did Parent Summary create value?
        7. What Product Discoveries are supported?
        8. What should be improved next?
        9. What should NOT be built yet?
        10. What should be added to MVP Scope?
        """;
}
