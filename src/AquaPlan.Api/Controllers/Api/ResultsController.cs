using System.Security.Claims;
using AquaPlan.Application.DTOs.Results;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

/// <summary>
/// AQ-415 — Read-only endpoints feeding the /results screen:
///   - recent results (7d window) card zone
///   - LDP × dates matrix with filters.
/// Préleveur-only users are intentionally excluded (they land on their tournées screen).
/// </summary>
[Route("api/results")]
[ApiController]
[Authorize(Roles = $"{RoleName.Administrator},{RoleName.Requerant},{RoleName.RequerantPreleveur}")]
public class ResultsController(
    IResultsService resultsService,
    IPermissionService permissionService) : ControllerBase
{
    [HttpGet("recent")]
    public async Task<ActionResult<IReadOnlyList<RecentResultDto>>> GetRecent(
        [FromQuery] int days = 7,
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var isAdmin = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);

        var result = await resultsService.GetRecentAsync(userId, tenantId, isAdmin, days, take, cancellationToken);
        return Ok(result);
    }

    [HttpGet("matrix")]
    public async Task<ActionResult<ResultsMatrixDto>> GetMatrix(
        [FromQuery] Guid? distributorId = null,
        [FromQuery] Guid? sectorId = null,
        [FromQuery] bool anomaliesOnly = false,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var isAdmin = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);

        var matrix = await resultsService.GetMatrixAsync(
            userId, tenantId, isAdmin,
            distributorId, sectorId, anomaliesOnly,
            dateFrom, dateTo,
            cancellationToken);
        return Ok(matrix);
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
    }

    private Guid GetTenantId()
    {
        var tenantClaim = User.FindFirst("tenant_id")?.Value ?? throw new UnauthorizedAccessException();
        return Guid.Parse(tenantClaim);
    }
}
