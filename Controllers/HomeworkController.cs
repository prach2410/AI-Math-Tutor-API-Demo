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
    public async Task<IActionResult> Analyze(List<IFormFile> images)
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

        var result = await service.AnalyzeAsync(imageData, fileName);

        return Ok(new
        {
            problemText = result.ProblemText,
            latex       = result.Latex,
            topic       = result.Topic,
            readable    = result.Readable,
            message     = result.Message,
        });
    }

    // ⚠️ ไม่มี auth + เก็บข้อมูลโจทย์ → demo/test เท่านั้น ปิดก่อนเปิดให้นักเรียนจริง
    [HttpGet("debug")]
    public IActionResult Debug()
    {
        var entries = HomeworkDebugLog.Recent();
        var rows = string.Join("", entries.Select(e => $@"
            <tr>
              <td style=""text-align:center;font-weight:600"">#{e.Seq}</td>
              <td>{e.At:HH:mm:ss}</td>
              <td style=""text-align:center"">{(e.Readable ? "✅" : "❌")}</td>
              <td><code>{System.Net.WebUtility.HtmlEncode(e.Reason)}</code></td>
              <td style=""color:#64748b;font-size:12px"">{System.Net.WebUtility.HtmlEncode(e.FileName)}</td>
              <td>{System.Net.WebUtility.HtmlEncode(e.ProblemText)}</td>
              <td><pre>{System.Net.WebUtility.HtmlEncode(e.RawResponse)}</pre></td>
            </tr>"));

        var html = $@"<!doctype html><html lang=""th""><head><meta charset=""utf-8"">
          <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
          <title>ผลการอ่านโจทย์</title>
          <style>
            body{{font-family:system-ui,sans-serif;padding:16px;background:#fafafa}}
            h2{{margin:0 0 4px}}
            p{{margin:0 0 12px;color:#666;font-size:13px}}
            table{{border-collapse:collapse;width:100%;background:#fff}}
            td,th{{border:1px solid #ddd;padding:8px;vertical-align:top;font-size:13px;text-align:left}}
            th{{background:#f0f0f0}}
            pre{{white-space:pre-wrap;word-break:break-word;max-width:380px;margin:0;font-size:12px;color:#444}}
            code{{background:#f3f3f3;padding:2px 4px;border-radius:3px}}
          </style></head><body>
          <h2>ผลการอ่านโจทย์ล่าสุด ({entries.Count}) — เวลา BKK (UTC+7)</h2>
          <p>reason: <code>ok</code> อ่านได้ · <code>model_unreadable</code> model บอกอ่านไม่ออก · <code>parse_error</code> parse JSON พัง (บั๊ก) · <code>api_error_*</code> API ขัดข้อง</p>
          <table>
            <tr><th>#</th><th>เวลา</th><th>อ่านได้</th><th>reason</th><th>ชื่อไฟล์</th><th>problemText</th><th>raw response</th></tr>
            {rows}
          </table></body></html>";

        return Content(html, "text/html");
    }
}
