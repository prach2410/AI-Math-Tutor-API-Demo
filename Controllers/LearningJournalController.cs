using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/learning-journal")]
public class LearningJournalController(LearningJournalService service) : ControllerBase
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

        var result = await service.AnalyzeAsync(imageData);

        return Ok(new
        {
            readable     = result.Readable,
            message      = result.Message,
            documentType = result.DocumentType,
            topic        = result.Topic,
            summary      = result.Summary,
            highlights   = result.Highlights,
            keywords     = result.Keywords,
        });
    }
}
