using System.Security.Claims;
using AquaPlan.Application.DTOs.Distributors;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class DistributorsController(
    IDistributorService distributorService,
    ILogger<DistributorsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IList<DistributorListDto>>> GetAll(
        [FromQuery] string? name,
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var filter = new DistributorFilteringInputDto(name, isActive);
        var distributors = await distributorService.GetAllAsync(filter, tenantId, cancellationToken);
        return Ok(distributors);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<DistributorDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var distributor = await distributorService.GetByIdAsync(id, tenantId, cancellationToken);
        if (distributor is null)
        {
            return NotFound();
        }
        return Ok(distributor);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<DistributorDto>> Create([FromBody] DistributorAddDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var distributor = await distributorService.CreateAsync(dto, tenantId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = distributor.Id }, distributor);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<DistributorDto>> Update(Guid id, [FromBody] DistributorUpdateDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var distributor = await distributorService.UpdateAsync(id, dto, tenantId, cancellationToken);
        if (distributor is null)
        {
            return NotFound();
        }
        return Ok(distributor);
    }

    [HttpPut("{id:guid}/toggle-status")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<DistributorDto>> ToggleStatus(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var distributor = await distributorService.ToggleStatusAsync(id, tenantId, cancellationToken);
        if (distributor is null)
        {
            return NotFound();
        }
        return Ok(distributor);
    }

    private Guid GetTenantId()
    {
        var tenantClaim = User.FindFirst("tenant_id")?.Value ?? throw new UnauthorizedAccessException();
        return Guid.Parse(tenantClaim);
    }
}
