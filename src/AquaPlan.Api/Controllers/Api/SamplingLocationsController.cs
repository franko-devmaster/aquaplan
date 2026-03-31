using System.Security.Claims;
using AquaPlan.Application.DTOs.SamplingLocations;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/sampling-locations")]
[ApiController]
[Authorize]
public class SamplingLocationsController(
    ISamplingLocationService samplingLocationService,
    IPermissionService permissionService,
    ILogger<SamplingLocationsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IList<SamplingLocationDto>>> GetForCurrentUser(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        var hasViewAll = await permissionService.UserHasPermissionAsync(userId, "AdministerSystem", cancellationToken);
        var locations = hasViewAll
            ? await samplingLocationService.GetAllAsync(tenantId, cancellationToken)
            : await samplingLocationService.GetForUserAsync(userId, tenantId, cancellationToken);
        return Ok(locations);
    }

    [HttpGet("by-distributor/{distributorId:guid}")]
    public async Task<ActionResult<IList<SamplingLocationDto>>> GetByDistributor(Guid distributorId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var locations = await samplingLocationService.GetByDistributorAsync(distributorId, tenantId, cancellationToken);
        return Ok(locations);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SamplingLocationDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var location = await samplingLocationService.GetByIdAsync(id, tenantId, cancellationToken);
        if (location is null)
        {
            return NotFound();
        }
        return Ok(location);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SamplingLocationDto>> Create([FromBody] SamplingLocationCreateDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var location = await samplingLocationService.CreateAsync(dto, tenantId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = location.Id }, location);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SamplingLocationDto>> Update(Guid id, [FromBody] SamplingLocationUpdateDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var location = await samplingLocationService.UpdateAsync(id, dto, tenantId, cancellationToken);
        if (location is null)
        {
            return NotFound();
        }
        return Ok(location);
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
