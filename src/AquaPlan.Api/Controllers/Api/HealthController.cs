using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
[AllowAnonymous]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new { Status = "ok", Timestamp = DateTime.UtcNow });
    }
}
