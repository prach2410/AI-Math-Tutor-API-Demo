using backend.Models;
using backend.Services;
using Microsoft.AspNetCore.Mvc;

namespace backend.Controllers;

[ApiController]
[Route("api/freetalk")]
public class FreeTalkController(FreeTalkService service) : ControllerBase
{
    [HttpPost]
    public ActionResult<FreeTalkResponse> Chat([FromBody] FreeTalkRequest request) =>
        Ok(service.Reply(request));
}
