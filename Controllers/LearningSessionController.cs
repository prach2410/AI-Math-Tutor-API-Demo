using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
public class LearningSessionController(LearningSessionService service) : ControllerBase
{
    [HttpPost("api/learning-sessions")]
    public async Task<IActionResult> Create([FromBody] CreateSessionRequest req)
    {
        await service.CreateAsync(req);
        return Ok();
    }

    [HttpPost("api/learning-sessions/{sessionId}/complete")]
    public async Task<IActionResult> Complete(string sessionId, [FromBody] CompleteSessionRequest req)
    {
        var ok = await service.CompleteAsync(sessionId, req);
        return ok ? Ok() : NotFound();
    }

    [HttpPost("api/learning-sessions/{sessionId}/parent-feedback")]
    public async Task<IActionResult> ParentFeedback(string sessionId, [FromBody] ParentFeedbackRequest req)
    {
        var ok = await service.UpdateParentFeedbackAsync(sessionId, req);
        return ok ? Ok() : NotFound();
    }

    [HttpPost("api/learning-sessions/{sessionId}/reflection")]
    public async Task<IActionResult> Reflection(string sessionId, [FromBody] ReflectionRequest req)
    {
        var ok = await service.UpdateReflectionAsync(sessionId, req);
        return ok ? Ok() : NotFound();
    }

    [HttpGet("api/admin/learning-sessions/export")]
    public async Task<IActionResult> Export()
    {
        var result = await service.ExportAsync();
        return Ok(result);
    }

    [HttpDelete("api/admin/learning-sessions")]
    public async Task<IActionResult> DeleteAll()
    {
        var count = await service.DeleteAllAsync();
        return Ok(new { deleted = count });
    }
}
