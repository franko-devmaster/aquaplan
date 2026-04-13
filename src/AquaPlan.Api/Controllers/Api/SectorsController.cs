using System.Security.Claims;
using AquaPlan.Application.DTOs.Sectors;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SectorsController(
    ISectorService sectorService,
    ILogger<SectorsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IList<SectorListDto>>> GetAll(
        [FromQuery] string? name,
        [FromQuery] bool? isActive,
        [FromQuery] Guid? distributorId,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var filter = new SectorFilteringInputDto(name, isActive, distributorId);
        var sectors = await sectorService.GetAllAsync(filter, tenantId, cancellationToken);
        return Ok(sectors);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SectorDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var sector = await sectorService.GetByIdAsync(id, tenantId, cancellationToken);
        if (sector is null)
        {
            return NotFound();
        }
        return Ok(sector);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SectorDto>> Create([FromBody] SectorAddDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var sector = await sectorService.CreateAsync(dto, tenantId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = sector.Id }, sector);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SectorDto>> Update(Guid id, [FromBody] SectorUpdateDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var sector = await sectorService.UpdateAsync(id, dto, tenantId, cancellationToken);
        if (sector is null)
        {
            return NotFound();
        }
        return Ok(sector);
    }

    [HttpPut("{id:guid}/toggle-status")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SectorDto>> ToggleStatus(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var sector = await sectorService.ToggleStatusAsync(id, tenantId, cancellationToken);
        if (sector is null)
        {
            return NotFound();
        }
        return Ok(sector);
    }

    private Guid GetTenantId()
    {
        var tenantClaim = User.FindFirst("tenant_id")?.Value ?? throw new UnauthorizedAccessException();
        return Guid.Parse(tenantClaim);
    }
}
