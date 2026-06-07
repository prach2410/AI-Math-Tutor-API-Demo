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

    public async Task<object> ExportAsync()
    {
        var all = await db.ProjectBrainEvidence
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync();

        return new
        {
            exportedAt = DateTime.UtcNow,
            version = "ProjectBrain V1.0",
            totalSessions = all.Count,
            sessions = all.Select(e => new
            {
                e.Id,
                e.SessionId,
                e.StudentId,
                e.Topic,
                e.CreatedAt,
                evidence  = TryDeserialize(e.EvidenceJson),
                summary   = TryDeserialize(e.SummaryJson),
            })
        };
    }

    private static object? TryDeserialize(string json)
    {
        try { return JsonSerializer.Deserialize<object>(json, JsonOpts); }
        catch { return null; }
    }

    public async Task<List<ProjectBrainEvidenceEntity>> GetRecentAsync(string studentId, int limit = 5)
    {
        return await db.ProjectBrainEvidence
            .Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync();
    }

    public async Task<string?> GetRecentSummaryAsync(string studentId, int limit = 3)
    {
        var sessions = await db.ProjectBrainEvidence
            .Where(e => e.StudentId == studentId)
            .OrderByDescending(e => e.CreatedAt)
            .Take(limit)
            .ToListAsync();

        if (sessions.Count == 0) return null;

        var lines = new List<string>();

        foreach (var s in sessions)
        {
            try
            {
                var summary = JsonSerializer.Deserialize<EvidenceSummary>(s.SummaryJson, JsonOpts);
                if (summary is null) continue;

                if (summary.StrongEvidence.Count > 0)
                {
                    lines.Add("✓ Strong:");
                    lines.AddRange(summary.StrongEvidence.Take(3).Select(e => $"  • {TruncateStatement(e)}"));
                }

                if (summary.OpenQuestions.Count > 0)
                {
                    lines.Add("? Questions:");
                    lines.AddRange(summary.OpenQuestions.Take(2).Select(q => $"  • {TruncateStatement(q)}"));
                }
            }
            catch { /* skip malformed JSON */ }
        }

        return lines.Count > 0 ? string.Join("\n", lines) : null;
    }

    private static string TruncateStatement(string s) =>
        s.Length > 60 ? s[..60] + "…" : s;

    private static string FormatItem(EvidenceItem i) =>
        $"[{i.EvidenceType}] {i.UserStatement}";
}
