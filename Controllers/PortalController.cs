using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/portal")]
public class PortalController : ControllerBase
{
    [HttpGet("status")]
    public IActionResult GetStatus()
    {
        return Ok(new
        {
            currentPhase    = "Demo V1.5",
            sprintStatus    = "READY_FOR_CODING",
            currentPriority = "Project Brain Portal — Live Status Feed",
            currentWork     = "เชื่อม Build tab กับ API จริง เพื่อ validate H14",
            nextMilestone   = "Portal แสดงข้อมูล live ได้ → H14 first validation",
            updatedAt       = "2026-06-10"
        });
    }
}
