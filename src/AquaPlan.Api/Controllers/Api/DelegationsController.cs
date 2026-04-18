using System.Security.Claims;
using AquaPlan.Application.DTOs.Delegations;
using AquaPlan.Application.DTOs.Distributors;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DelegationsController(
    IDelegationService delegationService,
    IPermissionService permissionService,
    ILogger<DelegationsController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<List<DistributorDelegationDto>>> GetAll(CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var result = await delegationService.GetAllAsync(tenantId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<DistributorDelegationDto>> Create([FromBody] DistributorDelegationCreateDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var created = await delegationService.CreateAsync(dto, tenantId, cancellationToken);
        return CreatedAtAction(nameof(GetAll), created);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var deleted = await delegationService.DeleteAsync(id, tenantId, cancellationToken);
        if (!deleted)
        {
            return NotFound();
        }
        return NoContent();
    }

    /// <summary>
    /// AQ-369 — returns distributors on which the current user is authorized to create
    /// orders/rounds (own + those that delegated to the user). Admins get all tenant distributors.
    /// </summary>
    [HttpGet("my-authorized-distributors")]
    public async Task<ActionResult<List<DistributorDto>>> GetMyAuthorizedDistributors(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var isAdmin = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        var result = await delegationService.GetAuthorizedDistributorsForUserAsync(userId, tenantId, isAdmin, cancellationToken);
        return Ok(result);
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
