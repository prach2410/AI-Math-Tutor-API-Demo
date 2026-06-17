using backend.Services;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;

namespace backend.Controllers;

[ApiController]
[Route("api/learning-journal")]
public class LearningJournalController(
    LearningJournalService service,
    LearningRecordsService records) : ControllerBase
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

        // Compute combined image hash (order-independent: sort individual hashes before combining)
        var sortedHashes = imageData
            .Select(img => Convert.ToHexString(SHA256.HashData(img.Bytes)).ToLowerInvariant())
            .OrderBy(h => h);
        var imageHash = Convert.ToHexString(
            SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(string.Concat(sortedHashes)))
        ).ToLowerInvariant();

        // Check duplicate BEFORE calling vision API (saves cost)
        var existing = await records.ExistsByHashAsync(imageHash);
        if (existing.HasValue)
        {
            return Ok(new
            {
                readable     = true,
                message      = "บันทึกไว้แล้ว",
                documentType = "",
                topic        = "",
                summary      = "",
                highlights   = Array.Empty<string>(),
                keywords     = Array.Empty<string>(),
                duplicate    = true,
                existingDate = existing.Value.Date,
            });
        }

        var result = await service.AnalyzeAsync(imageData);

        var savedId = "";
        if (result.Readable)
        {
            try { savedId = await records.SaveAsync(result, imageHash); }
            catch { /* don't break analysis on save failure */ }
        }

        return Ok(new
        {
            readable     = result.Readable,
            id           = savedId,
            message      = result.Message,
            documentType = result.DocumentType,
            topic        = result.Topic,
            summary      = result.Summary,
            highlights   = result.Highlights,
            keywords     = result.Keywords,
            duplicate    = false,
            existingDate = "",
        });
    }
}
