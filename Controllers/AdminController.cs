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
    public async Task<IActionResult> GetSessions([FromQuery] string? weekOf)
    {
        var refDate = string.IsNullOrWhiteSpace(weekOf)
            ? DateOnly.FromDateTime(DateTime.UtcNow)
            : DateOnly.ParseExact(weekOf, "yyyy-MM-dd", null);

        // Monday–Sunday of the week containing refDate
        int daysFromMonday = ((int)refDate.DayOfWeek + 6) % 7;
        var monday    = refDate.AddDays(-daysFromMonday);
        var sunday    = monday.AddDays(6);
        var nextMonday = monday.AddDays(7);

        var mondayStr     = monday.ToString("yyyy-MM-dd");
        var sundayStr     = sunday.ToString("yyyy-MM-dd");
        var nextMondayStr = nextMonday.ToString("yyyy-MM-dd");

        var lrEntries = await learningRecords.GetByDateRangeAsync(mondayStr, sundayStr);
        var lrList = lrEntries.Select(r => new
        {
            id           = r.Id,
            date         = r.Date,
            documentType = r.DocumentType,
            topic        = r.Topic,
            summary      = r.Summary,
            keywords     = r.Keywords,
            createdAt    = r.CreatedAt,
            downloadedAt = r.DownloadedAt,
            reflection   = r.Reflection,
        });

        // Filter in-memory: C# has no string comparison operators; dataset is small (personal use)
        var hrEntries = (await db.HomeworkReads.ToListAsync())
            .Where(r => r.CreatedAt.Length >= 10
                     && string.Compare(r.CreatedAt[..10], mondayStr, StringComparison.Ordinal) >= 0
                     && string.Compare(r.CreatedAt[..10], sundayStr, StringComparison.Ordinal) <= 0)
            .OrderBy(r => r.CreatedAt)
            .ToList();

        var allSessions = await db.TeachingSessions.ToListAsync();

        var taughtKeys = allSessions
            .Where(s => !string.IsNullOrEmpty(s.AnalysisStartedAt) && !string.IsNullOrEmpty(s.AnalysisEndedAt))
            .Select(s => (s.AnalysisStartedAt, s.AnalysisEndedAt))
            .ToHashSet();

        var hrList = hrEntries.Select(r => new
        {
            id                = r.Id,
            date              = r.CreatedAt.Length >= 10 ? r.CreatedAt[..10] : mondayStr,
            topic             = r.Topic,
            readable          = r.Readable,
            reason            = r.Reason,
            createdAt         = r.CreatedAt,
            visionModel       = r.VisionModel,
            analysisStartedAt = r.AnalysisStartedAt,
            analysisEndedAt   = r.AnalysisEndedAt,
            studentName       = r.StudentName,
            taught            = !string.IsNullOrEmpty(r.AnalysisStartedAt)
                             && !string.IsNullOrEmpty(r.AnalysisEndedAt)
                             && taughtKeys.Contains((r.AnalysisStartedAt, r.AnalysisEndedAt)),
        });

        var hwSessions = allSessions
            .Where(s => s.CreatedAt.Length >= 10
                     && string.Compare(s.CreatedAt[..10], mondayStr,     StringComparison.Ordinal) >= 0
                     && string.Compare(s.CreatedAt[..10], sundayStr,     StringComparison.Ordinal) <= 0)
            .OrderBy(s => s.CreatedAt)
            .ToList();

        var hwList = hwSessions.Select(s => new
        {
            id                = s.Id,
            date              = s.CreatedAt.Length >= 10 ? s.CreatedAt[..10] : mondayStr,
            topic             = s.Topic,
            problemText       = s.ProblemText,
            status            = s.Status,
            mode              = s.Mode,
            createdAt         = s.CreatedAt,
            downloadedAt      = s.DownloadedAt,
            visionModel       = s.VisionModel,
            analysisStartedAt = s.AnalysisStartedAt,
            analysisEndedAt   = s.AnalysisEndedAt,
            studentName       = s.StudentName,
        });

        return Ok(new { weekStart = mondayStr, weekEnd = sundayStr, learningRecords = lrList, homeworkReads = hrList, homeworkSessions = hwList });
    }

    [HttpDelete("learning-record/{id}")]
    public async Task<IActionResult> DeleteLearningRecord(string id)
    {
        var deleted = await learningRecords.DeleteAsync(id);
        return deleted ? NoContent() : NotFound();
    }

    [HttpDelete("homework-read/{id:int}")]
    public async Task<IActionResult> DeleteHomeworkRead(int id)
    {
        var e = await db.HomeworkReads.FindAsync(id);
        if (e is null) return NotFound();
        db.HomeworkReads.Remove(e);
        await db.SaveChangesAsync();
        return NoContent();
    }

    // debug: ดู raw OCR markdown + qwen structured output เพื่อวินิจฉัย parse failure
    [HttpGet("homework-read/{id:int}/raw")]
    public async Task<IActionResult> GetHomeworkReadRaw(int id)
    {
        var e = await db.HomeworkReads.FindAsync(id);
        if (e is null) return NotFound();
        return Ok(new { e.Id, e.Readable, e.Reason, e.VisionModel, rawLength = e.RawResponse?.Length ?? 0, raw = e.RawResponse });
    }

    [HttpDelete("homework-session/{id}")]
    public async Task<IActionResult> DeleteHomeworkSession(string id)
    {
        var e = await db.TeachingSessions.FindAsync(id);
        if (e is null) return NotFound();
        db.TeachingSessions.Remove(e);
        await db.SaveChangesAsync();
        return NoContent();
    }

    [HttpGet("export/homework/{id}")]
    public async Task<IActionResult> ExportHomework(string id)
    {
        var session = await db.TeachingSessions.FindAsync(id);
        if (session is null) return NotFound();

        session.DownloadedAt = DateTime.UtcNow.ToString("O");
        await db.SaveChangesAsync();

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

        if (!string.IsNullOrWhiteSpace(session.VisionModel))
        {
            var durationMs = 0.0;
            if (DateTime.TryParse(session.AnalysisStartedAt, null, System.Globalization.DateTimeStyles.RoundtripKind, out var tStart) &&
                DateTime.TryParse(session.AnalysisEndedAt,   null, System.Globalization.DateTimeStyles.RoundtripKind, out var tEnd))
                durationMs = (tEnd - tStart).TotalMilliseconds;
            sb.AppendLine();
            sb.AppendLine("---");
            sb.AppendLine($"วิเคราะห์ด้วย: {session.VisionModel} · ใช้เวลา: {(durationMs / 1000.0):F1}s");
        }

        var safeTopic = string.Concat(
            session.Topic.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
        var filename = $"{date}_homework_{safeTopic}.md";
        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/markdown; charset=utf-8", filename);
    }
}
