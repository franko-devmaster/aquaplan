using AquaPlan.Application.DTOs.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

// Sprint Robustesse F-114 — the client log sink is no longer anonymous: an unauthenticated
// caller could otherwise flood the Serilog files and inject arbitrary content. The SPA only
// emits client logs from authenticated sessions, so requiring authentication is non-breaking.
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class LogsController(ILogger<LogsController> logger) : ControllerBase
{
    [HttpPost]
    public IActionResult Post([FromBody] ClientLogDto dto)
    {
        var logLevel = dto.Level?.ToLowerInvariant() switch
        {
            "error" => LogLevel.Error,
            "warn" or "warning" => LogLevel.Warning,
            "info" or "information" => LogLevel.Information,
            "debug" => LogLevel.Debug,
            _ => LogLevel.Information,
        };

        logger.Log(logLevel, "Client [{Level}] {Context}: {Message}",
            dto.Level, dto.Context ?? "unknown", dto.Message);

        return Ok();
    }
}
