using System.Security.Claims;
using AquaPlan.Application.DTOs.SamplingPlans;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class SamplingPlansController(
    ISamplingPlanService samplingPlanService,
    IPermissionService permissionService,
    ILogger<SamplingPlansController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SamplingPlanPagedResultDto>> GetPlans(
        [FromQuery] List<SamplingPlanStatus>? statuses,
        [FromQuery] string? search,
        [FromQuery] int? year,
        [FromQuery] Guid? distributorId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = true,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var isAdmin = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);

        var filter = new SamplingPlanFilterDto(statuses, search, year, distributorId, page, pageSize, sortBy, sortDescending);
        var result = await samplingPlanService.GetPlansFilteredAsync(userId, tenantId, filter, isAdmin, cancellationToken);

        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SamplingPlanDetailDto>> GetPlan(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        var plan = await samplingPlanService.GetPlanByIdAsync(id, tenantId, cancellationToken);
        if (plan is null)
        {
            return NotFound();
        }

        var hasViewAll = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        if (!hasViewAll)
        {
            var hasAccess = await samplingPlanService.UserHasDistributorAccessAsync(userId, plan.DistributorId, cancellationToken);
            if (!hasAccess)
            {
                return Forbid();
            }
        }

        return Ok(plan);
    }

    [HttpPost]
    [Authorize(Roles = $"{RoleName.Administrator},{RoleName.Requerant},{RoleName.RequerantPreleveur}")]
    public async Task<ActionResult<SamplingPlanDetailDto>> CreatePlan([FromBody] SamplingPlanCreateDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        var hasViewAll = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        if (!hasViewAll)
        {
            var hasAccess = await samplingPlanService.UserHasDistributorAccessAsync(userId, dto.DistributorId, cancellationToken);
            if (!hasAccess)
            {
                return Forbid();
            }
        }

        try
        {
            var plan = await samplingPlanService.CreatePlanAsync(dto, userId, tenantId, cancellationToken);
            return CreatedAtAction(nameof(GetPlan), new { id = plan.Id }, plan);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SamplingPlanDetailDto>> UpdatePlan(Guid id, [FromBody] SamplingPlanUpdateDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        var existing = await samplingPlanService.GetPlanByIdAsync(id, tenantId, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        var hasViewAll = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        if (!hasViewAll)
        {
            var hasAccess = await samplingPlanService.UserHasDistributorAccessAsync(userId, existing.DistributorId, cancellationToken);
            if (!hasAccess)
            {
                return Forbid();
            }
        }

        try
        {
            var plan = await samplingPlanService.UpdatePlanAsync(id, dto, userId, tenantId, cancellationToken);
            if (plan is null)
            {
                return NotFound();
            }
            return Ok(plan);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeletePlan(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        var existing = await samplingPlanService.GetPlanByIdAsync(id, tenantId, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        var hasViewAll = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        if (!hasViewAll)
        {
            var hasAccess = await samplingPlanService.UserHasDistributorAccessAsync(userId, existing.DistributorId, cancellationToken);
            if (!hasAccess)
            {
                return Forbid();
            }
        }

        try
        {
            var deleted = await samplingPlanService.DeletePlanAsync(id, userId, tenantId, cancellationToken);
            if (!deleted)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    // Polish F-207 — submitting a plan is a state change; like Update/Delete it must verify the
    // caller is authorized on the plan's distributor. Previously any tenant user could submit
    // another distributor's Draft plan.
    [HttpPost("{id:guid}/submit")]
    public async Task<ActionResult<SamplingPlanDetailDto>> SubmitPlan(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        var existing = await samplingPlanService.GetPlanByIdAsync(id, tenantId, cancellationToken);
        if (existing is null)
        {
            return NotFound();
        }

        var hasViewAll = await permissionService.UserHasPermissionAsync(userId, PermissionName.ViewAllOrders, cancellationToken);
        if (!hasViewAll)
        {
            var hasAccess = await samplingPlanService.UserHasDistributorAccessAsync(userId, existing.DistributorId, cancellationToken);
            if (!hasAccess)
            {
                return Forbid();
            }
        }

        try
        {
            var plan = await samplingPlanService.SubmitPlanAsync(id, userId, tenantId, cancellationToken);
            if (plan is null)
            {
                return NotFound();
            }
            return Ok(plan);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/validate")]
    public async Task<ActionResult<SamplingPlanDetailDto>> ValidatePlan(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        var isAdmin = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        if (!isAdmin)
        {
            return Forbid();
        }

        try
        {
            var plan = await samplingPlanService.ValidatePlanAsync(id, userId, tenantId, cancellationToken);
            if (plan is null)
            {
                return NotFound();
            }
            return Ok(plan);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<ActionResult<SamplingPlanDetailDto>> RejectPlan(Guid id, [FromBody] SamplingPlanRejectDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        var isAdmin = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        if (!isAdmin)
        {
            return Forbid();
        }

        try
        {
            var plan = await samplingPlanService.RejectPlanAsync(id, dto.Reason, userId, tenantId, cancellationToken);
            if (plan is null)
            {
                return NotFound();
            }
            return Ok(plan);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPost("{id:guid}/generate-orders")]
    public async Task<ActionResult<GenerateOrdersResultDto>> GenerateOrders(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        var isAdmin = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        if (!isAdmin)
        {
            var plan = await samplingPlanService.GetPlanByIdAsync(id, tenantId, cancellationToken);
            if (plan is null)
            {
                return NotFound();
            }
            var hasAccess = await samplingPlanService.UserHasDistributorAccessAsync(userId, plan.DistributorId, cancellationToken);
            if (!hasAccess)
            {
                return Forbid();
            }
        }

        try
        {
            var result = await samplingPlanService.GenerateOrdersFromPlanAsync(id, userId, tenantId, cancellationToken);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
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
