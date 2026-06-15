using backend.Services;
using Microsoft.AspNetCore.Mvc;

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
}
