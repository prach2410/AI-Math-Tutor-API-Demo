using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/learning")]
public class LearningFlowController(LearningFlowService service) : ControllerBase
{
    [HttpGet("start/{scenarioId}")]
    public ActionResult<StartResponse> Start(string scenarioId)
    {
        var scenario = service.GetScenario(scenarioId);
        if (scenario is null) return NotFound();

        var first = scenario.Steps[0];
        return Ok(new StartResponse(
            ScenarioId: scenarioId,
            StepNumber: first.StepNumber,
            TotalSteps: first.TotalSteps,
            Question: first.Question,
            IsLast: first.IsLast,
            RealWorldUses: scenario.RealWorldUses
        ));
    }

    [HttpGet("assist/{scenarioId}/{stepNumber}/{type}")]
    public ActionResult<AssistResponse> Assist(string scenarioId, int stepNumber, string type)
    {
        return Ok(service.GetAssist(scenarioId, stepNumber, type));
    }

    [HttpPost("evaluate")]
    public ActionResult<EvaluateResponse> Evaluate([FromBody] EvaluateRequest request)
    {
        return Ok(service.Evaluate(request));
    }
}
