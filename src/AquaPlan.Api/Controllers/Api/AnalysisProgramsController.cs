using System.Security.Claims;
using AquaPlan.Application.DTOs.AnalysisPrograms;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/analysis-programs")]
[ApiController]
[Authorize]
public class AnalysisProgramsController(
    IAnalysisProgramService analysisProgramService,
    ILogger<AnalysisProgramsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IList<AnalysisProgramListDto>>> GetAll(
        [FromQuery] AnalysisProgramFilteringInputDto? filter,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var programs = await analysisProgramService.GetAllAsync(tenantId, filter, cancellationToken);
        return Ok(programs);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AnalysisProgramDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var program = await analysisProgramService.GetByIdAsync(id, tenantId, cancellationToken);
        if (program is null)
        {
            return NotFound();
        }
        return Ok(program);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<AnalysisProgramDto>> Create([FromBody] AnalysisProgramAddDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var program = await analysisProgramService.CreateAsync(dto, tenantId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = program.Id }, program);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<AnalysisProgramDto>> Update(Guid id, [FromBody] AnalysisProgramUpdateDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var program = await analysisProgramService.UpdateAsync(id, dto, tenantId, cancellationToken);
        if (program is null)
        {
            return NotFound();
        }
        return Ok(program);
    }

    [HttpPatch("{id:guid}/toggle-status")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<AnalysisProgramDto>> ToggleStatus(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var program = await analysisProgramService.ToggleStatusAsync(id, tenantId, cancellationToken);
        if (program is null)
        {
            return NotFound();
        }
        return Ok(program);
    }

    [HttpPost("{id:guid}/profiles")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<AnalysisProgramDto>> AddProfiles(Guid id, [FromBody] AnalysisProgramAddProfilesDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var program = await analysisProgramService.AddProfilesAsync(id, dto.ProfileIds, tenantId, cancellationToken);
        if (program is null)
        {
            return NotFound();
        }
        return Ok(program);
    }

    [HttpDelete("{id:guid}/profiles/{profileId:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult> RemoveProfile(Guid id, Guid profileId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var removed = await analysisProgramService.RemoveProfileAsync(id, profileId, tenantId, cancellationToken);
        if (!removed)
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
