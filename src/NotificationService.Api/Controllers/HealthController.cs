using Microsoft.AspNetCore.Mvc;

namespace NotificationService.Api.Controllers;

[ApiController]
[Route("api/[controller]")]
public class HealthController : ControllerBase
{
    [HttpGet]
    public IActionResult Get()
    {
        return Ok(new
        {
            status = "ok",
            service = "NotificationService.Api",
            timestamp = DateTime.UtcNow
        });
    }
}
