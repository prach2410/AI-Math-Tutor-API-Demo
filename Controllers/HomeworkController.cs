using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/homework")]
public class HomeworkController(HomeworkAnalysisService service) : ControllerBase
{
    [HttpPost("analyze")]
    public async Task<IActionResult> Analyze(IFormFile image)
    {
        if (image is null || image.Length == 0)
            return BadRequest(new { error = "กรุณาแนบรูปภาพ" });

        var allowed = new[] { "image/jpeg", "image/jpg", "image/png", "image/gif", "image/webp" };
        if (!allowed.Contains(image.ContentType.ToLowerInvariant()))
            return BadRequest(new { error = "รองรับเฉพาะไฟล์รูปภาพ (JPEG, PNG, GIF, WEBP)" });

        using var ms = new MemoryStream();
        await image.CopyToAsync(ms);

        var result = await service.AnalyzeAsync(ms.ToArray(), image.ContentType);

        return Ok(new
        {
            problemText = result.ProblemText,
            latex       = result.Latex,
            topic       = result.Topic,
            readable    = result.Readable,
            message     = result.Message,
        });
    }

    // หน้า debug แสดงผลการอ่านล่าสุด + raw response — เปิดดูตอนทดสอบ 10 รูป
    // ⚠️ ไม่มี auth + เก็บข้อมูลโจทย์ → ใช้ช่วง demo/test เท่านั้น ต้องปิด/ใส่ guard ก่อนเปิดให้นักเรียนจริง
    [HttpGet("debug")]
    public IActionResult Debug()
    {
        var entries = HomeworkDebugLog.Recent();
        var rows = string.Join("", entries.Select(e => $@"
            <tr>
              <td>{e.At:HH:mm:ss}</td>
              <td style=""text-align:center"">{(e.Readable ? "✅" : "❌")}</td>
              <td><code>{System.Net.WebUtility.HtmlEncode(e.Reason)}</code></td>
              <td>{System.Net.WebUtility.HtmlEncode(e.ProblemText)}</td>
              <td><pre>{System.Net.WebUtility.HtmlEncode(e.RawResponse)}</pre></td>
            </tr>"));

        var html = $@"<!doctype html><html lang=""th""><head><meta charset=""utf-8"">
          <meta name=""viewport"" content=""width=device-width, initial-scale=1"">
          <title>ผลการอ่านโจทย์</title>
          <style>
            body{{font-family:system-ui,sans-serif;padding:16px;background:#fafafa}}
            h2{{margin:0 0 12px}}
            table{{border-collapse:collapse;width:100%;background:#fff}}
            td,th{{border:1px solid #ddd;padding:8px;vertical-align:top;font-size:13px;text-align:left}}
            th{{background:#f0f0f0}}
            pre{{white-space:pre-wrap;word-break:break-word;max-width:420px;margin:0;font-size:12px;color:#444}}
            code{{background:#f3f3f3;padding:2px 4px;border-radius:3px}}
          </style></head><body>
          <h2>ผลการอ่านโจทย์ล่าสุด ({entries.Count})</h2>
          <p style=""color:#666;font-size:13px"">reason: <code>ok</code> อ่านได้ · <code>model_unreadable</code> model บอกอ่านไม่ออก · <code>parse_error</code> parse JSON พัง (บั๊ก ไม่ใช่รูปยาก) · <code>api_error_*</code> API ขัดข้อง</p>
          <table>
            <tr><th>เวลา</th><th>อ่านได้</th><th>reason</th><th>problemText</th><th>raw response</th></tr>
            {rows}
          </table></body></html>";

        return Content(html, "text/html");
    }
}
