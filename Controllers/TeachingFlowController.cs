using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/teaching")]
public class TeachingFlowController(TeachingFlowService service) : ControllerBase
{
    public record StartRequest(string ProblemText, string Latex, string Topic, bool HasFigure,
        string VisionModel = "", string AnalysisStartedAt = "", string AnalysisEndedAt = "", string StudentName = "",
        string StudentId = "", string DeviceId = "");
    public record SolveRequest(string ProblemText, string Latex, string Topic,
        string VisionModel = "", string AnalysisStartedAt = "", string AnalysisEndedAt = "", string StudentName = "",
        string StudentId = "", string DeviceId = "");
    public record ExplainRequest(string ProblemText, string Topic, string StepText, string FullSolution = "");
    public record AnswerRequest(string Answer);
    public record HintRequest(int Level);
    public record ConfirmFigureRequest(string StudentNote);
    public record RecallAnswerRequest(string RecallQuestion, string Answer, string Topic = "");

    [HttpPost("explain")]
    public async Task<IActionResult> Explain([FromBody] ExplainRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.StepText))
            return BadRequest(new { error = "กรุณาระบุขั้นที่ต้องการอธิบาย" });

        try
        {
            var explanation = await service.ExplainAsync(req.ProblemText, req.Topic, req.StepText, req.FullSolution);
            return Ok(new { explanation });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "ไม่สามารถอธิบายเพิ่มได้ กรุณาลองใหม่", detail = ex.Message });
        }
    }

    [HttpPost("solve")]
    public async Task<IActionResult> Solve([FromBody] SolveRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ProblemText))
            return BadRequest(new { error = "กรุณาระบุโจทย์" });

        try
        {
            var result = await service.SolveAsync(req.ProblemText, req.Latex, req.Topic,
                req.VisionModel, req.AnalysisStartedAt, req.AnalysisEndedAt, req.StudentName,
                req.StudentId, req.DeviceId);
            return Ok(new
            {
                sessionId         = result.SessionId,
                solutionSteps     = result.SolutionSteps,
                understandingStep = result.UnderstandingStep,
                keyStepIndices    = result.KeyStepIndices,
            });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "ไม่สามารถสร้างวิธีทำได้ กรุณาลองใหม่", detail = ex.Message });
        }
    }

    [HttpGet("recall")]
    public async Task<IActionResult> Recall(
        [FromQuery] string studentId = "", [FromQuery] string deviceId = "", [FromQuery] string topic = "")
    {
        try
        {
            var result = await service.GetRecallAsync(studentId, deviceId, topic);
            return Ok(result is null
                ? new { show = false, recallQuestion = "" }
                : new { show = result.Show, recallQuestion = result.RecallQuestion });
        }
        catch (Exception ex)
        {
            // recall เป็น optional — ล้มเหลวไม่ควรบล็อกการบ้าน
            return Ok(new { show = false, recallQuestion = "", detail = ex.Message });
        }
    }

    [HttpPost("recall-answer")]
    public async Task<IActionResult> RecallAnswer([FromBody] RecallAnswerRequest req)
    {
        try
        {
            var feedback = await service.RecallFeedbackAsync(req.RecallQuestion, req.Answer, req.Topic);
            return Ok(new { feedback });
        }
        catch (Exception ex)
        {
            return Ok(new { feedback = "มาเริ่มการบ้านวันนี้กันเลย!", detail = ex.Message });
        }
    }

    [HttpPost("start")]
    public async Task<IActionResult> Start([FromBody] StartRequest req)
    {
        if (string.IsNullOrWhiteSpace(req.ProblemText))
            return BadRequest(new { error = "กรุณาระบุโจทย์" });

        try
        {
            var result = await service.StartAsync(req.ProblemText, req.Latex, req.Topic, req.HasFigure,
                req.VisionModel, req.AnalysisStartedAt, req.AnalysisEndedAt, req.StudentName,
                req.StudentId, req.DeviceId);

            if (result.NeedsConfirm)
            {
                return Ok(new
                {
                    sessionId         = result.SessionId,
                    needsConfirm      = true,
                    figureDescription = result.FigureDescription,
                    currentStep       = (object?)null,
                    totalSteps        = 0,
                });
            }

            return Ok(new
            {
                sessionId  = result.SessionId,
                needsConfirm = false,
                figureDescription = "",
                currentStep = new
                {
                    step            = result.CurrentStep!.Step,
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

    [HttpPost("{sessionId}/confirm-figure")]
    public async Task<IActionResult> ConfirmFigure(string sessionId, [FromBody] ConfirmFigureRequest req)
    {
        try
        {
            var result = await service.ConfirmFigureAsync(sessionId, req.StudentNote);
            return Ok(new
            {
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
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "ไม่พบ session นี้" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "ยืนยันรูปไม่สำเร็จ กรุณาลองใหม่", detail = ex.Message });
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

    [HttpGet("{sessionId}/notes")]
    public async Task<IActionResult> Notes(string sessionId)
    {
        try
        {
            var (studentNotes, parentSummary) = await service.NotesAndSummaryAsync(sessionId);
            return Ok(new { studentNotes, parentSummary });
        }
        catch (KeyNotFoundException)
        {
            return NotFound(new { error = "ไม่พบ session นี้" });
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { error = "สร้างสรุปไม่สำเร็จ", detail = ex.Message });
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
