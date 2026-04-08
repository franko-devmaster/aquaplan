using System.Security.Claims;
using AquaPlan.Application.DTOs.Delegations;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Administrator")]
public class DelegationsController(
    IDelegationService delegationService,
    ILogger<DelegationsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<List<DistributorDelegationDto>>> GetAll(CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var result = await delegationService.GetAllAsync(tenantId, cancellationToken);
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<DistributorDelegationDto>> Create([FromBody] DistributorDelegationCreateDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var created = await delegationService.CreateAsync(dto, tenantId, cancellationToken);
        return CreatedAtAction(nameof(GetAll), created);
    }

    [HttpDelete("{id:guid}")]
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

    private Guid GetTenantId()
    {
        var tenantClaim = User.FindFirst("tenant_id")?.Value ?? throw new UnauthorizedAccessException();
        return Guid.Parse(tenantClaim);
    }
}
