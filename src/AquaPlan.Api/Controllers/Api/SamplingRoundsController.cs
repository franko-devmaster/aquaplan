using System.Security.Claims;
using AquaPlan.Application.DTOs.SamplingRounds;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/sampling-rounds")]
[ApiController]
[Authorize]
public class SamplingRoundsController(
    ISamplingRoundService samplingRoundService,
    IPermissionService permissionService,
    IDelegationService delegationService,
    ILogger<SamplingRoundsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SamplingRoundPagedResultDto>> GetRounds(
        [FromQuery(Name = "statuses")] SamplingRoundStatus[]? statuses,
        [FromQuery] Guid? distributorId,
        [FromQuery] string? preleveurId,
        [FromQuery] DateTime? deadlineFrom,
        [FromQuery] DateTime? deadlineTo,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var isAdmin = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);

        // AQ-398 — A user is "préleveur only" when they hold the Préleveur role and
        // none of the requérant roles. Such users only see rounds they are assigned to.
        var isPreleveurOnly = IsPreleveurOnly();

        var filter = new SamplingRoundFilterDto(
            statuses, distributorId, preleveurId, deadlineFrom, deadlineTo, search, page, pageSize);

        var result = await samplingRoundService.GetFilteredAsync(userId, tenantId, filter, isAdmin, isPreleveurOnly, cancellationToken);
        return Ok(result);
    }

    // Polish F-205 — reading a round by id must honour the same visibility policy as the listing
    // (AQ-398): admins see everything, a "préleveur only" sees rounds assigned to them, mandataires
    // see rounds of their authorized distributors. Previously any tenant user could read any round.
    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SamplingRoundDetailDto>> GetRound(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var result = await samplingRoundService.GetByIdAsync(id, tenantId, cancellationToken);
        if (result is null) return NotFound();

        var hasViewAll = await permissionService.UserHasPermissionAsync(userId, PermissionName.ViewAllOrders, cancellationToken);
        if (!hasViewAll)
        {
            if (IsPreleveurOnly())
            {
                if (result.PreleveurId != userId) return Forbid();
            }
            else
            {
                var authorizedIds = await delegationService.GetAuthorizedDistributorIdsForUserAsync(userId, cancellationToken);
                if (!authorizedIds.Contains(result.DistributorId)) return Forbid();
            }
        }

        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = $"{RoleName.Administrator},{RoleName.Requerant},{RoleName.RequerantPreleveur}")]
    public async Task<ActionResult<SamplingRoundDetailDto>> CreateRound(
        [FromBody] SamplingRoundCreateDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        var hasViewAll = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        if (!hasViewAll)
        {
            // AQ-369 — user can only create on own distributor + distributors that delegated to him.
            var authorizedIds = await delegationService.GetAuthorizedDistributorIdsForUserAsync(userId, cancellationToken);
            if (!authorizedIds.Contains(dto.DistributorId))
            {
                return Forbid();
            }
        }

        var result = await samplingRoundService.CreateAsync(dto, userId, tenantId, cancellationToken);
        return CreatedAtAction(nameof(GetRound), new { id = result.Id }, result);
    }

    // Sprint Robustesse F-106 — round write operations are mandataire/admin actions (same roles
    // as CreateRound), scoped to the caller's authorized distributors for non-admins. Previously
    // any authenticated tenant user (incl. a "préleveur only") could update/cancel/reorder any
    // round, which for cancel meant cancelling every order of the round.
    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{RoleName.Administrator},{RoleName.Requerant},{RoleName.RequerantPreleveur}")]
    public async Task<ActionResult<SamplingRoundDetailDto>> UpdateRound(
        Guid id, [FromBody] SamplingRoundUpdateDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var access = await EnsureCanAccessRoundAsync(id, userId, tenantId, cancellationToken);
        if (access is not null) return access;
        var result = await samplingRoundService.UpdateAsync(id, dto, userId, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    // Sprint Robustesse F-107 — DELETE is now an admin-only soft-cancel (round -> Cancelled,
    // orders detached, never hard-deleted). See SamplingRoundService.DeleteAsync.
    [HttpDelete("{id:guid}")]
    [Authorize(Roles = RoleName.Administrator)]
    public async Task<ActionResult> DeleteRound(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var deleted = await samplingRoundService.DeleteAsync(id, userId, tenantId, cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpPost("{id:guid}/assign")]
    [Authorize(Roles = $"{RoleName.Administrator},{RoleName.Requerant},{RoleName.RequerantPreleveur}")]
    public async Task<ActionResult<SamplingRoundDetailDto>> AssignPreleveur(
        Guid id, [FromBody] SamplingRoundAssignDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var access = await EnsureCanAccessRoundAsync(id, userId, tenantId, cancellationToken);
        if (access is not null) return access;
        var result = await samplingRoundService.AssignPreleveurAsync(id, dto, userId, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("{id:guid}/revert-to-draft")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SamplingRoundDetailDto>> RevertToDraft(
        Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var result = await samplingRoundService.RevertToDraftAsync(id, userId, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("{id:guid}/transmit-all")]
    [Authorize(Roles = $"{RoleName.Administrator},{RoleName.Requerant},{RoleName.RequerantPreleveur}")]
    public async Task<ActionResult<SamplingRoundDetailDto>> TransmitAll(
        Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var access = await EnsureCanAccessRoundAsync(id, userId, tenantId, cancellationToken);
        if (access is not null) return access;
        var result = await samplingRoundService.TransmitAllAsync(id, userId, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    /// <summary>AQ-370 — starts the round (Assigned → InProgress) and poses the lock.</summary>
    [HttpPost("{id:guid}/start")]
    [Authorize(Roles = $"{RoleName.Administrator},{RoleName.Preleveur},{RoleName.RequerantPreleveur}")]
    public async Task<ActionResult<SamplingRoundDetailDto>> StartRound(
        Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var isAdmin = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        var result = await samplingRoundService.StartAsync(id, userId, tenantId, isAdmin, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    /// <summary>AQ-372 — admin force-unlock: releases the préleveur lock and reverts InProgress → Assigned.</summary>
    [HttpPost("{id:guid}/force-unlock")]
    [Authorize(Roles = RoleName.Administrator)]
    public async Task<ActionResult<SamplingRoundDetailDto>> ForceUnlock(
        Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var result = await samplingRoundService.ForceUnlockAsync(id, userId, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    /// <summary>AQ-373 — aggregated offline snapshot for the préleveur (round + orders + locations + catalog).</summary>
    [HttpGet("{id:guid}/offline-snapshot")]
    [Authorize(Roles = $"{RoleName.Administrator},{RoleName.Preleveur},{RoleName.RequerantPreleveur}")]
    public async Task<ActionResult<OfflineSnapshotDto>> GetOfflineSnapshot(
        Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var isAdmin = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        try
        {
            var result = await samplingRoundService.GetOfflineSnapshotAsync(id, userId, isAdmin, tenantId, cancellationToken);
            if (result is null) return NotFound();
            return Ok(result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = $"{RoleName.Administrator},{RoleName.Requerant},{RoleName.RequerantPreleveur}")]
    public async Task<ActionResult<SamplingRoundDetailDto>> CancelRound(
        Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var access = await EnsureCanAccessRoundAsync(id, userId, tenantId, cancellationToken);
        if (access is not null) return access;
        var result = await samplingRoundService.CancelAsync(id, userId, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("{id:guid}/orders")]
    [Authorize(Roles = $"{RoleName.Administrator},{RoleName.Requerant},{RoleName.RequerantPreleveur}")]
    public async Task<ActionResult<SamplingRoundDetailDto>> AddOrder(
        Guid id, [FromBody] SamplingRoundAddOrderDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var result = await samplingRoundService.AddOrderAsync(id, dto.OrderId, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    /// <summary>
    /// AQ-413 — detaches an order from a sampling round WITHOUT deleting it.
    /// Restricted to administrators and mandataires (Requérant / Requérant-Préleveur).
    /// Returns 204 on success, 404 when the round or the link does not exist,
    /// 409 when the round is in a status that forbids modification (InProgress / Completed / Cancelled).
    /// </summary>
    [HttpDelete("{id:guid}/orders/{orderId:guid}")]
    [Authorize(Roles = $"{RoleName.Administrator},{RoleName.Requerant},{RoleName.RequerantPreleveur}")]
    public async Task<ActionResult> RemoveOrder(Guid id, Guid orderId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var removed = await samplingRoundService.RemoveOrderAsync(id, orderId, tenantId, cancellationToken);
        if (!removed) return NotFound();
        return NoContent();
    }

    [HttpPut("{id:guid}/orders/reorder")]
    [Authorize(Roles = $"{RoleName.Administrator},{RoleName.Requerant},{RoleName.RequerantPreleveur}")]
    public async Task<ActionResult<SamplingRoundDetailDto>> ReorderOrders(
        Guid id, [FromBody] SamplingRoundReorderDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var access = await EnsureCanAccessRoundAsync(id, userId, tenantId, cancellationToken);
        if (access is not null) return access;
        var result = await samplingRoundService.ReorderAsync(id, dto, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("~/api/orders/{orderId:guid}/replace-location")]
    public async Task<ActionResult> ReplaceLocation(
        Guid orderId, [FromBody] LocationReplacementDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var isAdmin = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        var result = await samplingRoundService.ReplaceLocationAsync(orderId, dto, userId, tenantId, isAdmin, cancellationToken);
        if (!result) return NotFound();
        return Ok();
    }

    [HttpPost("~/api/orders/{orderId:guid}/start")]
    public async Task<ActionResult> StartOrder(Guid orderId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var result = await samplingRoundService.StartOrderAsync(orderId, userId, tenantId, cancellationToken);
        if (!result) return NotFound();
        return Ok();
    }

    [HttpPut("~/api/orders/{orderId:guid}/sampler-comment")]
    public async Task<ActionResult> UpdateSamplerComment(
        Guid orderId, [FromBody] SamplerCommentDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var result = await samplingRoundService.UpdateSamplerCommentAsync(orderId, dto, userId, tenantId, cancellationToken);
        if (!result) return NotFound();
        return Ok();
    }

    /// <summary>
    /// Sprint Robustesse F-106 — for non-admins, verifies the round belongs to one of the
    /// caller's authorized distributors (own + active delegations), the same rule applied to
    /// round visibility (AQ-398) and creation (AQ-369). Returns 404 when the round does not
    /// exist, 403 when it exists but is out of scope, or null when access is granted.
    /// </summary>
    private async Task<ActionResult?> EnsureCanAccessRoundAsync(
        Guid roundId, string userId, Guid tenantId, CancellationToken cancellationToken)
    {
        var hasViewAll = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        if (hasViewAll)
        {
            return null;
        }

        var round = await samplingRoundService.GetByIdAsync(roundId, tenantId, cancellationToken);
        if (round is null)
        {
            return NotFound();
        }

        var authorizedIds = await delegationService.GetAuthorizedDistributorIdsForUserAsync(userId, cancellationToken);
        return authorizedIds.Contains(round.DistributorId) ? null : Forbid();
    }

    /// <summary>
    /// AQ-398 — a user is "préleveur only" when they hold the Préleveur role and none of the
    /// requérant/admin roles. Such users only see/act on rounds assigned to them.
    /// </summary>
    private bool IsPreleveurOnly()
    {
        return User.IsInRole(RoleName.Preleveur)
            && !User.IsInRole(RoleName.Requerant)
            && !User.IsInRole(RoleName.RequerantPreleveur)
            && !User.IsInRole(RoleName.Administrator);
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
