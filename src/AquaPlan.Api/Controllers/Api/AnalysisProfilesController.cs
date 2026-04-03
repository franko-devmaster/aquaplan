using System.Security.Claims;
using AquaPlan.Application.DTOs.AnalysisProfiles;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/analysis-profiles")]
[ApiController]
[Authorize]
public class AnalysisProfilesController(
    IAnalysisProfileService analysisProfileService,
    ILogger<AnalysisProfilesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IList<AnalysisProfileListDto>>> GetAll(
        [FromQuery] AnalysisProfileFilteringInputDto? filter,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var profiles = await analysisProfileService.GetAllAsync(tenantId, filter, cancellationToken);
        return Ok(profiles);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AnalysisProfileDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var profile = await analysisProfileService.GetByIdAsync(id, tenantId, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }
        return Ok(profile);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<AnalysisProfileDto>> Create([FromBody] AnalysisProfileAddDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var profile = await analysisProfileService.CreateAsync(dto, tenantId, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = profile.Id }, profile);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<AnalysisProfileDto>> Update(Guid id, [FromBody] AnalysisProfileUpdateDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var profile = await analysisProfileService.UpdateAsync(id, dto, tenantId, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }
        return Ok(profile);
    }

    [HttpPatch("{id:guid}/toggle-status")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<AnalysisProfileDto>> ToggleStatus(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var profile = await analysisProfileService.ToggleStatusAsync(id, tenantId, cancellationToken);
        if (profile is null)
        {
            return NotFound();
        }
        return Ok(profile);
    }

    private Guid GetTenantId()
    {
        var tenantClaim = User.FindFirst("tenant_id")?.Value ?? throw new UnauthorizedAccessException();
        return Guid.Parse(tenantClaim);
    }
}
