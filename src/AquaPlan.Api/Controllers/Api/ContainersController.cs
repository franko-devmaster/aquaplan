using AquaPlan.Application.DTOs.Containers;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/containers")]
[ApiController]
[Authorize]
public class ContainersController(
    IContainerService containerService,
    ILogger<ContainersController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IList<ContainerListDto>>> GetAll(
        [FromQuery] ContainerFilteringInputDto? filter,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var containers = await containerService.GetAllAsync(tenantId, filter, cancellationToken);
        return Ok(containers);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ContainerDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var container = await containerService.GetByIdAsync(id, tenantId, cancellationToken);
        if (container is null)
        {
            return NotFound();
        }
        return Ok(container);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<ContainerDto>> Create([FromBody] ContainerAddDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var container = await containerService.CreateAsync(dto, tenantId, cancellationToken);
        logger.LogInformation("Container {Code} created by admin", dto.Code);
        return CreatedAtAction(nameof(GetById), new { id = container.Id }, container);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<ContainerDto>> Update(Guid id, [FromBody] ContainerUpdateDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var container = await containerService.UpdateAsync(id, dto, tenantId, cancellationToken);
        if (container is null)
        {
            return NotFound();
        }
        return Ok(container);
    }

    [HttpPatch("{id:guid}/toggle-status")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<ContainerDto>> ToggleStatus(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var container = await containerService.ToggleStatusAsync(id, tenantId, cancellationToken);
        if (container is null)
        {
            return NotFound();
        }
        return Ok(container);
    }

    private Guid GetTenantId()
    {
        var tenantClaim = User.FindFirst("tenant_id")?.Value ?? throw new UnauthorizedAccessException();
        return Guid.Parse(tenantClaim);
    }
}
