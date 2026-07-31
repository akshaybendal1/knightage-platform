using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Knightage.Platform.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get() => Ok(new { status = "ok", service = "knightage-platform" });

    [Authorize]
    [HttpGet("secure")]
    public IActionResult GetSecure() => Ok(new
    {
        status = "ok",
        service = "knightage-platform",
        userId = User.FindFirst("sub")?.Value,
        orgId = User.FindFirst("org_id")?.Value
    });
}
