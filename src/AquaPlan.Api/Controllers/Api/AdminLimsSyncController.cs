using System.Security.Claims;
using AquaPlan.Application.DTOs.LimsSync;
using AquaPlan.Application.DTOs.MockLims;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

/// <summary>
/// AQ-35 — Admin-only monitoring endpoints for the LIMS sync worker.
/// AQ-404 — Also exposes a retroactive backfill endpoint for Transmitted orders without a LimsOrderId.
/// </summary>
[Route("api/admin/lims-sync")]
[ApiController]
[Authorize(Roles = RoleName.Administrator)]
public class AdminLimsSyncController(
    ILimsSyncService limsSyncService,
    IMockLimsBackfillService backfillService) : ControllerBase
{
    private const int DefaultBackfillMaxOrders = 100;
    private const int BackfillHardCap = 500;

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

    /// <summary>
    /// AQ-404 — Reconciles legacy Transmitted orders (no <c>LimsOrderId</c>) with the Mock LIMS.
    /// Runs outbound + inbound pull per order, in isolation. Administrator-only.
    /// </summary>
    [HttpPost("backfill-results")]
    public async Task<ActionResult<MockLimsBackfillResultDto>> BackfillResults(
        [FromBody] MockLimsBackfillRequestDto? request,
        CancellationToken cancellationToken)
    {
        var callerTenantId = GetTenantId();
        var tenantId = request?.TenantId ?? callerTenantId;
        var maxOrders = Math.Clamp(
            request?.MaxOrders ?? DefaultBackfillMaxOrders,
            1,
            BackfillHardCap);

        var result = await backfillService.BackfillAsync(tenantId, maxOrders, cancellationToken);
        return Ok(result);
    }

    private Guid GetTenantId()
    {
        var tenantClaim = User.FindFirst("tenant_id")?.Value ?? throw new UnauthorizedAccessException();
        return Guid.Parse(tenantClaim);
    }
}
