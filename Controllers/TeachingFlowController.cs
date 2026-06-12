using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/teaching")]
public class TeachingFlowController(TeachingFlowService service) : ControllerBase
{
    public record StartRequest(string ProblemText, string Latex, string Topic, bool HasFigure);
    public record AnswerRequest(string Answer);
    public record HintRequest(int Level);

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ProblemText))
            return BadRequest(new { error = "กรุณาระบุโจทย์" });

        try
        {
            var result = await service.StartAsync(req.ProblemText, req.Latex, req.Topic, req.HasFigure);
            return Ok(new
            {
                sessionId  = result.SessionId,
                currentStep = new
                {
                    step            = result.CurrentStep.Step,
                    goal            = result.CurrentStep.Goal,
                    guidingQuestion = result.CurrentStep.GuidingQuestion,
                    conceptHint     = result.CurrentStep.ConceptHint,
                },
                totalSteps = result.TotalSteps,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "ไม่สามารถเริ่มการสอนได้ กรุณาลองใหม่", detail = ex.Message });
        }
    }

    [HttpPost("{sessionId}/answer")]
    public async Task<IActionResult> Answer(string sessionId, [FromBody] AnswerRequest req)
    {
        try
        {
            var result = await service.AnswerAsync(sessionId, req.Answer);
            return Ok(new
            {
                verdict      = result.Verdict,
                reason       = result.Reason,
                missing      = result.Missing,
                encouragement = result.Encouragement,
                nextStep     = result.NextStep is null ? null : new
                {
                    step            = result.NextStep.Step,
                    goal            = result.NextStep.Goal,
                    guidingQuestion = result.NextStep.GuidingQuestion,
                    conceptHint     = result.NextStep.ConceptHint,
                },
                done = result.Done,
            });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "ไม่พบ session นี้" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "เกิดข้อผิดพลาด กรุณาลองใหม่", detail = ex.Message });
        }
    }

    [HttpPost("{sessionId}/hint")]
    public async Task<IActionResult> Hint(string sessionId, [FromBody] HintRequest req)
    {
        try
        {
            var result = await service.HintAsync(sessionId, req.Level);
            return Ok(new { level = result.Level, help = result.Help });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "ไม่พบ session นี้" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "ขอความช่วยเหลือไม่สำเร็จ กรุณาลองใหม่", detail = ex.Message });
        }
    }
}
