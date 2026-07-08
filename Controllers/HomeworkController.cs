using System.Text;
using System.Text.Json;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/homework")]
public class HomeworkController(HomeworkAnalysisService service) : ControllerBase
{
    private static readonly string[] AllowedTypes =
        ["image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp"];

    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze(List<IFormFile> images, [FromForm] string studentName = "")
    {
        if (images is null || images.Count == 0)
            return BadRequest(new { error = "กรุณาแนบรูปภาพอย่างน้อย 1 รูป" });

        foreach (var img in images)
        {
            if (img.Length == 0)
                return BadRequest(new { error = $"ไฟล์ {img.FileName} ว่างเปล่า" });

            if (!AllowedTypes.Contains(img.ContentType.ToLowerInvariant()))
                return BadRequest(new { error = "รองรับเฉพาะไฟล์รูปภาพ (JPEG, PNG, GIF, WEBP)" });
        }

        var imageData = new List<(byte[] Bytes, string MediaType)>(images.Count);
        foreach (var img in images)
        {
            using var ms = new MemoryStream();
            await img.CopyToAsync(ms);
            imageData.Add((ms.ToArray(), img.ContentType));
        }

        var fileName = images.Count == 1
            ? images[0].FileName
            : $"{images[0].FileName} +{images.Count - 1} รูป";

        var result = await service.AnalyzeAsync(imageData, fileName, studentName);

        return Ok(new
        {
            readable          = result.Readable,
            message           = result.Message,
            visionModel       = result.VisionModel,
            analysisStartedAt = result.StartedAt,
            analysisEndedAt   = result.EndedAt,
            problems          = result.Problems.Select(p => new
            {
                index       = p.Index,
                problemText = p.ProblemText,
                latex       = p.Latex,
                topic       = p.Topic,
                hasFigure   = p.HasFigure,
                groupIndex  = p.GroupIndex,
                groupTitle  = p.GroupTitle,
                subText     = p.SubText,
            }),
        });
    }

    [HttpPost("typed")]
    public async Task<IActionResult> SaveTyped([FromBody] TypedProblemRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ProblemText))
            return BadRequest(new { error = "problemText ว่าง" });
        await service.SaveTypedAsync(req.ProblemText.Trim(), req.StudentName ?? "");
        return Ok(new { saved = true });
    }

    [HttpGet("reads")]
    public async Task<IActionResult> GetReads([FromQuery] int limit = 30, [FromQuery] string name = "")
    {
        var entries = await service.GetRecentAsync(limit, name);
        var result = new List<object>();
        foreach (var e in entries)
        {
            if (!e.Readable) continue;
            List<ProblemItem>? problems;
            try { problems = JsonSerializer.Deserialize<List<ProblemItem>>(e.ProblemText, new JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
            catch { continue; }
            if (problems == null || problems.Count == 0) continue;

            result.Add(new
            {
                id                = e.Id,
                createdAt         = e.CreatedAt,
                topic             = e.Topic,
                problemCount      = problems.Count,
                visionModel       = e.VisionModel,
                analysisStartedAt = e.AnalysisStartedAt,
                analysisEndedAt   = e.AnalysisEndedAt,
                problems          = problems.Select(p => new
                {
                    index       = p.Index,
                    problemText = p.ProblemText,
                    latex       = p.Latex,
                    topic       = p.Topic,
                    hasFigure   = p.HasFigure,
                    groupIndex  = p.GroupIndex,
                    groupTitle  = p.GroupTitle,
                    subText     = p.SubText,
                }),
            });
        }
        return Ok(result);
    }

    // ⚠️ ไม่มี auth + เก็บข้อมูลโจทย์ → demo/test เท่านั้น ปิดก่อนเปิดให้นักเรียนจริง
    [HttpGet("debug")]
    public async Task<IActionResult> Debug()
    {
        var entries = await service.GetRecentAsync(50);
        var rows = string.Join("", entries.Select(e =>
        {
            var bkk = DateTimeOffset.Parse(e.CreatedAt).ToOffset(TimeSpan.FromHours(7));
            return $@"
            <tr>
              <td style=""text-align:center;font-weight:600"">#{e.Id}</td>
              <td>{bkk:HH:mm:ss}</td>
              <td style=""text-align:center"">{(e.Readable ? "✅" : "❌")}</td>
              <td><code>{HtmlEncode(e.Reason)}</code></td>
              <td style=""color:#64748b;font-size:12px"">{HtmlEncode(e.Filename)}</td>
              <td>{HtmlEncode(e.ProblemText)}</td>
              <td><pre>{HtmlEncode(e.RawResponse)}</pre></td>
            </tr>";
        }));

        var html = $@"<!doctype html><html lang=""th""><head><meta charset=""utf-8"">
          <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
          <title>ผลการอ่านโจทย์</title>
          <style>
            body{{font-family:system-ui,sans-serif;padding:16px;background:#fafafa}}
            h2{{margin:0 0 4px}}
            p{{margin:0 0 12px;color:#666;font-size:13px}}
            .toolbar{{margin-bottom:12px}}
            a.btn{{display:inline-block;padding:8px 16px;background:#2563eb;color:#fff;text-decoration:none;border-radius:6px;font-size:13px;font-weight:600}}
            a.btn:hover{{background:#1d4ed8}}
            table{{border-collapse:collapse;width:100%;background:#fff}}
            td,th{{border:1px solid #ddd;padding:8px;vertical-align:top;font-size:13px;text-align:left}}
            th{{background:#f0f0f0}}
            pre{{white-space:pre-wrap;word-break:break-word;max-width:380px;margin:0;font-size:12px;color:#444}}
            code{{background:#f3f3f3;padding:2px 4px;border-radius:3px}}
          </style></head><body>
          <h2>ผลการอ่านโจทย์ล่าสุด ({entries.Count}) — เวลา BKK (UTC+7)</h2>
          <p>reason: <code>ok</code> อ่านได้ · <code>model_unreadable</code> model บอกอ่านไม่ออก · <code>parse_error</code> parse JSON พัง (บั๊ก) · <code>api_error_*</code> API ขัดข้อง</p>
          <div class=""toolbar"">
            <a class=""btn"" href=""/api/homework/export"">⬇ Export CSV</a>
          </div>
          <table>
            <tr><th>#</th><th>เวลา</th><th>อ่านได้</th><th>reason</th><th>ชื่อไฟล์</th><th>problemText</th><th>raw response</th></tr>
            {rows}
          </table></body></html>";

        return Content(html, "text/html");
    }

    // ⚠️ ไม่มี auth → demo/test เท่านั้น
    [HttpGet("export")]
    public async Task<IActionResult> Export()
    {
        var entries = await service.GetAllAsync();

        var sb = new StringBuilder();
        sb.AppendLine("seq,เวลา (BKK),readable,reason,topic,problemText");

        foreach (var e in entries)
        {
            var bkk = DateTimeOffset.Parse(e.CreatedAt).ToOffset(TimeSpan.FromHours(7));
            sb.AppendLine(string.Join(",",
                e.Id.ToString(),
                CsvQuote(bkk.ToString("yyyy-MM-dd HH:mm:ss")),
                e.Readable ? "true" : "false",
                CsvQuote(e.Reason),
                CsvQuote(e.Topic),
                CsvQuote(e.ProblemText)
            ));
        }

        // UTF-8 BOM ให้ Excel เปิดภาษาไทยออก
        var bom = new byte[] { 0xEF, 0xBB, 0xBF };
        var body = Encoding.UTF8.GetBytes(sb.ToString());
        var bytes = bom.Concat(body).ToArray();

        var filename = $"homework_reads_{DateTimeOffset.UtcNow.ToOffset(TimeSpan.FromHours(7)):yyyy-MM-dd}.csv";
        return File(bytes, "text/csv; charset=utf-8", filename);
    }

    private static string HtmlEncode(string s) => System.Net.WebUtility.HtmlEncode(s);

    private static string CsvQuote(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return '"' + value.Replace("\"", "\"\"") + '"';
        return value;
    }
}

public record TypedProblemRequest(string ProblemText, string StudentName = "");
