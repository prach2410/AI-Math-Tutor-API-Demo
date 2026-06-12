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
}
