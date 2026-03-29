using AquaPlan.Application.DTOs.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
[AllowAnonymous]
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
