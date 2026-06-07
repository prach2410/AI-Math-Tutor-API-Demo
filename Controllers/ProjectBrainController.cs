using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/project-brain")]
public class ProjectBrainController(
    ProjectBrainTutorService service,
    ProjectBrainEvidenceService evidenceService) : ControllerBase
{
    [HttpGet("topics")]
    public ActionResult GetTopics()
    {
        var topics = KnowledgeCurriculum.Topics.Select(t => new
        {
            t.Id,
            t.Title,
            t.Emoji,
            t.Subtitle,
        });
        return Ok(topics);
    }

    [HttpPost("chat")]
    public ActionResult<ProjectBrainResponse> Chat([FromBody] ProjectBrainRequest request)
    {
        return Ok(service.Chat(request));
    }

    [HttpPost("sessions/{sessionId}/evidence")]
    public async Task<IActionResult> SaveEvidence(string sessionId, [FromBody] SaveEvidenceRequest request)
    {
        await evidenceService.SaveAsync(sessionId, request);
        return Ok();
    }

    [HttpGet("evidence")]
    public async Task<IActionResult> GetEvidence([FromQuery] string studentId, [FromQuery] int limit = 5)
    {
        if (string.IsNullOrWhiteSpace(studentId)) return BadRequest("studentId required");
        var results = await evidenceService.GetRecentAsync(studentId, limit);
        return Ok(results);
    }

    [HttpGet("/api/admin/project-brain/export")]
    public async Task<IActionResult> Export()
    {
        var result = await evidenceService.ExportAsync();
        return Ok(result);
    }
}
