using backend.Services;
using Microsoft.AspNetCore.Mvc;
using System.Text;

namespace backend.Controllers;

[ApiController]
[Route("api/learning-records")]
public class LearningRecordsController(LearningRecordsService service) : ControllerBase
{
    [HttpGet("timeline")]
    public async Task<IActionResult> GetTimeline()
    {
        var groups = await service.GetTimelineAsync();
        return Ok(groups.Select(g => new
        {
            date    = g.Date,
            records = g.Records.Select(r => new
            {
                id           = r.Id,
                documentType = r.DocumentType,
                topic        = r.Topic,
                summary      = r.Summary,
                keywords     = r.Keywords,
                createdAt    = r.CreatedAt,
            })
        }));
    }

    [HttpGet("{id}/export")]
    public async Task<IActionResult> Export(string id)
    {
        var result = await service.ExportMarkdownAsync(id);
        if (result is null) return NotFound();

        var bytes = Encoding.UTF8.GetBytes(result.Value.Markdown);
        return File(bytes, "text/markdown; charset=utf-8", result.Value.Filename);
    }
}
