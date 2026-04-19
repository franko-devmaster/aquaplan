using System.Security.Claims;
using AquaPlan.Application.DTOs.LimsSync;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

/// <summary>
/// AQ-35 — Admin-only monitoring endpoints for the LIMS sync worker.
/// </summary>
[Route("api/admin/lims-sync")]
[ApiController]
[Authorize(Roles = RoleName.Administrator)]
public class AdminLimsSyncController(ILimsSyncService limsSyncService) : ControllerBase
{
    [HttpGet("status")]
    public async Task<ActionResult<LimsSyncStatusDto>> GetStatus(CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var status = await limsSyncService.GetStatusAsync(tenantId, cancellationToken);
        return Ok(status);
    }

    [HttpGet("logs")]
    public async Task<ActionResult<IReadOnlyList<LimsSyncLogDto>>> GetLogs(
        [FromQuery] int take = 50,
        [FromQuery] LimsSyncStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var logs = await limsSyncService.GetLogsAsync(tenantId, take, status, cancellationToken);
        return Ok(logs);
    }

    private Guid GetTenantId()
    {
        var tenantClaim = User.FindFirst("tenant_id")?.Value ?? throw new UnauthorizedAccessException();
        return Guid.Parse(tenantClaim);
    }
}
