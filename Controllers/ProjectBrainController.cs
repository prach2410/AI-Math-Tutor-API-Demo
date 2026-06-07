using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/project-brain")]
public class ProjectBrainController(ProjectBrainTutorService service) : ControllerBase
{
    [HttpPost("chat")]
    public ActionResult<ProjectBrainResponse> Chat([FromBody] ProjectBrainRequest request)
    {
        return Ok(service.Chat(request));
    }
}
