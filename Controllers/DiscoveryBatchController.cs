using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/admin/discovery-batches")]
public class DiscoveryBatchController(DiscoveryBatchService service) : ControllerBase
{
    [HttpGet("unreviewed-count")]
    public async Task<IActionResult> UnreviewedCount()
        => Ok(await service.GetUnreviewedCountAsync());

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateBatchRequest req)
        => Ok(await service.CreateBatchAsync(req.MaxSessions));

    [HttpGet]
    public async Task<IActionResult> List()
        => Ok(await service.ListBatchesAsync());

    [HttpGet("{batchId}")]
    public async Task<IActionResult> Get(string batchId)
    {
        var batch = await service.GetBatchAsync(batchId);
        return batch is null ? NotFound() : Ok(batch);
    }

    [HttpGet("{batchId}/export")]
    public async Task<IActionResult> Export(string batchId)
    {
        var result = await service.ExportBatchAsync(batchId);
        return result is null ? NotFound() : Ok(result);
    }

    [HttpGet("analysis-prompt")]
    public IActionResult GetAnalysisPrompt()
        => Ok(new { prompt = DiscoveryBatchService.AnalysisPromptText });

    [HttpPut("{batchId}/notes")]
    public async Task<IActionResult> UpdateNotes(string batchId, [FromBody] UpdateNotesRequest req)
    {
        var ok = await service.UpdateNotesAsync(batchId, req);
        return ok ? Ok() : NotFound();
    }

    [HttpPost("{batchId}/mark-reviewed")]
    public async Task<IActionResult> MarkReviewed(string batchId)
    {
        var ok = await service.MarkReviewedAsync(batchId);
        return ok ? Ok() : NotFound();
    }
}
