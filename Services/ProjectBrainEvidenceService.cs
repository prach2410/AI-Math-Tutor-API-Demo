using System.Text.Json;
using backend.Data;
using backend.Models;
using Microsoft.EntityFrameworkCore;

namespace backend.Services;

public class ProjectBrainEvidenceService(AppDbContext db)
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public async Task SaveAsync(string sessionId, SaveEvidenceRequest req)
    {
        var items = req.Items;

        var summary = new EvidenceSummary(
            StrongEvidence:      items.Where(i => i.Confidence >= 0.85).Select(FormatItem).ToList(),
            PartialEvidence:     items.Where(i => i.Confidence is >= 0.6 and < 0.85).Select(FormatItem).ToList(),
            OpenQuestions:       items.Where(i => i.EvidenceType == "Question").Select(i => i.UserStatement).ToList(),
            PossibleMisalignment: []
        );

        var entity = new ProjectBrainEvidenceEntity
        {
            Id           = Guid.NewGuid().ToString(),
            SessionId    = sessionId,
            StudentId    = req.StudentId,
            Topic        = req.Topic,
            CreatedAt    = DateTime.UtcNow,
            EvidenceJson = JsonSerializer.Serialize(items, JsonOpts),
            SummaryJson  = JsonSerializer.Serialize(summary, JsonOpts),
        };

        db.ProjectBrainEvidence.Add(entity);
        await db.SaveChangesAsync();
    }

    public async Task<List<ProjectBrainEvidenceEntity>> GetRecentAsync(string studentId, int limit = 5)
    {
        return await db.ProjectBrainEvidence
            .Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    private static string FormatItem(EvidenceItem i) =>
        $"[{i.EvidenceType}] {i.UserStatement}";
}
