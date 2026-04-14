using System.Security.Claims;
using AquaPlan.Application.DTOs.SamplingRounds;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/sampling-rounds")]
[ApiController]
[Authorize]
public class SamplingRoundsController(
    ISamplingRoundService samplingRoundService,
    IPermissionService permissionService,
    ILogger<SamplingRoundsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SamplingRoundPagedResultDto>> GetRounds(
        [FromQuery] SamplingRoundStatus? status,
        [FromQuery] Guid? distributorId,
        [FromQuery] string? preleveurId,
        [FromQuery] DateTime? deadlineFrom,
        [FromQuery] DateTime? deadlineTo,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var isAdmin = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);

        var filter = new SamplingRoundFilterDto(
            status, distributorId, preleveurId, deadlineFrom, deadlineTo, search, page, pageSize);

        var result = await samplingRoundService.GetFilteredAsync(userId, tenantId, filter, isAdmin, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<SamplingRoundDetailDto>> GetRound(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var result = await samplingRoundService.GetByIdAsync(id, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<SamplingRoundDetailDto>> CreateRound(
        [FromBody] SamplingRoundCreateDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var result = await samplingRoundService.CreateAsync(dto, userId, tenantId, cancellationToken);
        return CreatedAtAction(nameof(GetRound), new { id = result.Id }, result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<SamplingRoundDetailDto>> UpdateRound(
        Guid id, [FromBody] SamplingRoundUpdateDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var result = await samplingRoundService.UpdateAsync(id, dto, userId, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteRound(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var deleted = await samplingRoundService.DeleteAsync(id, tenantId, cancellationToken);
        if (!deleted) return NotFound();
        return NoContent();
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<ActionResult<SamplingRoundDetailDto>> AssignPreleveur(
        Guid id, [FromBody] SamplingRoundAssignDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var result = await samplingRoundService.AssignPreleveurAsync(id, dto, userId, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("{id:guid}/revert-to-draft")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<SamplingRoundDetailDto>> RevertToDraft(
        Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var result = await samplingRoundService.RevertToDraftAsync(id, userId, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("{id:guid}/transmit-all")]
    public async Task<ActionResult<SamplingRoundDetailDto>> TransmitAll(
        Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var result = await samplingRoundService.TransmitAllAsync(id, userId, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<ActionResult<SamplingRoundDetailDto>> CancelRound(
        Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var result = await samplingRoundService.CancelAsync(id, userId, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("{id:guid}/orders")]
    public async Task<ActionResult<SamplingRoundDetailDto>> AddOrder(
        Guid id, [FromBody] SamplingRoundAddOrderDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var result = await samplingRoundService.AddOrderAsync(id, dto.OrderId, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpDelete("{id:guid}/orders/{orderId:guid}")]
    public async Task<ActionResult> RemoveOrder(Guid id, Guid orderId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var removed = await samplingRoundService.RemoveOrderAsync(id, orderId, tenantId, cancellationToken);
        if (!removed) return NotFound();
        return NoContent();
    }

    [HttpPut("{id:guid}/orders/reorder")]
    public async Task<ActionResult<SamplingRoundDetailDto>> ReorderOrders(
        Guid id, [FromBody] SamplingRoundReorderDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var result = await samplingRoundService.ReorderAsync(id, dto, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("~/api/orders/{orderId:guid}/replace-location")]
    public async Task<ActionResult> ReplaceLocation(
        Guid orderId, [FromBody] LocationReplacementDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var result = await samplingRoundService.ReplaceLocationAsync(orderId, dto, userId, tenantId, cancellationToken);
        if (!result) return NotFound();
        return Ok();
    }

    [HttpPost("~/api/orders/{orderId:guid}/start")]
    public async Task<ActionResult> StartOrder(Guid orderId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var result = await samplingRoundService.StartOrderAsync(orderId, userId, tenantId, cancellationToken);
        if (!result) return NotFound();
        return Ok();
    }

    [HttpPut("~/api/orders/{orderId:guid}/sampler-comment")]
    public async Task<ActionResult> UpdateSamplerComment(
        Guid orderId, [FromBody] SamplerCommentDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var result = await samplingRoundService.UpdateSamplerCommentAsync(orderId, dto, userId, tenantId, cancellationToken);
        if (!result) return NotFound();
        return Ok();
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
