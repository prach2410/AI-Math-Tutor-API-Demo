using backend.Data;
using backend.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text;
using System.Text.Json;

namespace backend.Controllers;

[ApiController]
[Route("api/admin")]
public class AdminController(AppDbContext db, LearningRecordsService learningRecords) : ControllerBase
{
    [HttpGet("sessions")]
    public async Task<IActionResult> GetSessions([FromQuery] string? date)
    {
        var targetDate = string.IsNullOrWhiteSpace(date)
            ? DateTime.UtcNow.ToString("yyyy-MM-dd")
            : date;

        var lrEntries = await learningRecords.GetByDateAsync(targetDate);
        var lrList = lrEntries.Select(r => new
        {
            id           = r.Id,
            documentType = r.DocumentType,
            topic        = r.Topic,
            summary      = r.Summary,
            keywords     = r.Keywords,
            createdAt    = r.CreatedAt,
        });

        var hwSessions = await db.TeachingSessions
            .Where(s => s.CreatedAt.StartsWith(targetDate))
            .OrderByDescending(s => s.CreatedAt)
            .ToListAsync();

        var hwList = hwSessions.Select(s => new
        {
            id          = s.Id,
            topic       = s.Topic,
            problemText = s.ProblemText,
            status      = s.Status,
            mode        = s.Mode,
            createdAt   = s.CreatedAt,
        });

        return Ok(new { learningRecords = lrList, homeworkSessions = hwList });
    }

    [HttpGet("export/homework/{id}")]
    public async Task<IActionResult> ExportHomework(string id)
    {
        var session = await db.TeachingSessions.FindAsync(id);
        if (session is null) return NotFound();

        var date = session.CreatedAt.Length >= 10
            ? session.CreatedAt[..10]
            : DateTime.UtcNow.ToString("yyyy-MM-dd");

        var opts = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        List<TeachingStep> steps;
        try { steps = JsonSerializer.Deserialize<List<TeachingStep>>(session.StepsJson, opts) ?? []; }
        catch { steps = []; }

        var sb = new StringBuilder();
        sb.AppendLine($"# การบ้าน — {date}");
        sb.AppendLine();
        sb.AppendLine("## หัวข้อ");
        sb.AppendLine(session.Topic);
        sb.AppendLine();
        sb.AppendLine("## โจทย์");
        sb.AppendLine(session.ProblemText);

        if (steps.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("## การสอน");
            foreach (var s in steps)
            {
                sb.AppendLine($"### ขั้นที่ {s.Step}");
                sb.AppendLine(s.GuidingQuestion);
                if (!string.IsNullOrWhiteSpace(s.ConceptHint))
                    sb.AppendLine($"*แนวคิด: {s.ConceptHint}*");
                sb.AppendLine();
            }
        }

        var statusLabel = session.Status switch
        {
            "done"        => "เสร็จสิ้น",
            "in_progress" => "กำลังดำเนินการ",
            _             => session.Status,
        };
        sb.AppendLine("## สรุป");
        sb.AppendLine($"สถานะ: {statusLabel} · mode: {session.Mode}");

        var safeTopic = string.Concat(
            session.Topic.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var filename = $"{date}_homework_{safeTopic}.md";
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/markdown; charset=utf-8", filename);
    }
}
