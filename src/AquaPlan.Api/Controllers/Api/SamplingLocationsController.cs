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

    [HttpGet("filtered")]
    public async Task<ActionResult<SamplingLocationListDto>> GetFiltered(
        [FromQuery] SamplingLocationFilteringInputDto filter,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var result = await samplingLocationService.GetFilteredAsync(filter, tenantId, cancellationToken);
        return Ok(result);
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
    public async Task<ActionResult<SamplingLocationDto>> Create([FromBody] SamplingLocationCreateDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();

        var isUnique = await samplingLocationService.IsLocationCodeUniqueAsync(dto.LocationCode, dto.DistributorId, null, tenantId, cancellationToken);
        if (!isUnique)
        {
            return Conflict(new { message = $"A sampling location with code '{dto.LocationCode}' already exists for this distributor." });
        }

        var isAdmin = User.IsInRole("Administrator");
        var location = await samplingLocationService.CreateAsync(dto, tenantId, isValidated: isAdmin, cancellationToken);
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

    [HttpPut("{id:guid}/toggle-status")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<ToggleStatusResultDto>> ToggleStatus(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var result = await samplingLocationService.ToggleStatusAsync(id, tenantId, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    [HttpGet("check-code-unique")]
    public async Task<ActionResult<bool>> CheckCodeUnique(
        [FromQuery] string locationCode,
        [FromQuery] Guid distributorId,
        [FromQuery] Guid? excludeId,
        CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var isUnique = await samplingLocationService.IsLocationCodeUniqueAsync(locationCode, distributorId, excludeId, tenantId, cancellationToken);
        return Ok(isUnique);
    }

    [HttpGet("export-pdf")]
    public async Task<ActionResult> ExportPdf([FromQuery] Guid? distributorId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        var hasViewAll = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        if (!hasViewAll && distributorId is null)
        {
            return Forbid();
        }

        var pdfBytes = await samplingLocationService.ExportPdfAsync(tenantId, distributorId, cancellationToken);
        return File(pdfBytes, "application/pdf", $"lieux-prelevement-{DateTime.UtcNow:yyyyMMdd}.pdf");
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
