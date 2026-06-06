using System.Text.Json;
using System.Text.Json.Serialization;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class LearningSessionService(AppDbContext db)
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    public async Task CreateAsync(CreateSessionRequest req)
    {
        if (await db.LearningSessions.AnyAsync(s => s.SessionId == req.SessionId))
            return;

        var doc = new SessionDocument
        {
            SessionId = req.SessionId,
            StudentAlias = req.StudentAlias,
            StudentId = req.StudentId,
            DeviceId = req.DeviceId,
            DisplayName = req.DisplayName,
            Topic = req.Topic,
            StartedAt = req.StartedAt,
            Completed = false
        };

        db.LearningSessions.Add(new LearningSessionEntity
        {
            SessionId = req.SessionId,
            Topic = req.Topic,
            StudentAlias = req.StudentAlias,
            StudentId = req.StudentId,
            DeviceId = req.DeviceId,
            CreatedAt = req.StartedAt,
            Completed = false,
            SessionJson = JsonSerializer.Serialize(doc, _opts)
        });
        await db.SaveChangesAsync();
    }

    public async Task<bool> CompleteAsync(string sessionId, CompleteSessionRequest req)
    {
        var entity = await db.LearningSessions.FindAsync(sessionId);
        if (entity is null) return false;

        var doc = JsonSerializer.Deserialize<SessionDocument>(entity.SessionJson, _opts) ?? new SessionDocument();
        doc.CompletedAt = req.CompletedAt;
        doc.Completed = true;
        doc.Messages = req.Messages;
        doc.Events = req.Events;
        doc.Summary = req.Summary;

        entity.Completed = true;
        entity.CompletedAt = req.CompletedAt;
        entity.SessionJson = JsonSerializer.Serialize(doc, _opts);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateParentFeedbackAsync(string sessionId, ParentFeedbackRequest req)
    {
        var entity = await db.LearningSessions.FindAsync(sessionId);
        if (entity is null) return false;

        var doc = JsonSerializer.Deserialize<SessionDocument>(entity.SessionJson, _opts) ?? new SessionDocument();
        doc.ParentFeedback = new ParentFeedbackData
        {
            SummaryOpened = true,
            FeedbackSubmitted = true,
            FeedbackSubmittedAt = DateTime.UtcNow.AddHours(7),
            UnderstandingLevel = req.UnderstandingLevel,
            MostValuableSection = req.MostValuableSection,
            Comment = req.Comment
        };

        entity.SessionJson = JsonSerializer.Serialize(doc, _opts);
        await db.SaveChangesAsync();
        return true;
    }

    public async Task<int> DeleteAllAsync()
    {
        var count = await db.LearningSessions.CountAsync();
        db.LearningSessions.RemoveRange(db.LearningSessions);
        await db.SaveChangesAsync();
        return count;
    }

    public async Task<ExportResponse> ExportAsync()
    {
        var sessions = await db.LearningSessions
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        return new ExportResponse(
            ExportedAt: DateTime.UtcNow.AddHours(7),
            Version: "Demo V1.10",
            TotalSessions: sessions.Count,
            Sessions: sessions.Select(s => JsonSerializer.Deserialize<JsonElement>(s.SessionJson)).ToList()
        );
    }
}

public class SessionDocument
{
    public string SessionId { get; set; } = string.Empty;
    public string StudentAlias { get; set; } = "Student-001";
    public string? StudentId { get; set; }
    public string? DeviceId { get; set; }
    public string? DisplayName { get; set; }
    public string Topic { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public bool Completed { get; set; }
    public List<SessionMessage>? Messages { get; set; }
    public List<SessionEvent>? Events { get; set; }
    public SessionSummary? Summary { get; set; }
    public ParentFeedbackData? ParentFeedback { get; set; }
}

public class ParentFeedbackData
{
    public bool SummaryOpened { get; set; }
    public bool FeedbackSubmitted { get; set; }
    public DateTime? FeedbackSubmittedAt { get; set; }
    public string UnderstandingLevel { get; set; } = string.Empty;
    public string? MostValuableSection { get; set; }
    public string? Comment { get; set; }
}

public record ExportResponse(
    DateTime ExportedAt,
    string Version,
    int TotalSessions,
    List<JsonElement> Sessions
);
