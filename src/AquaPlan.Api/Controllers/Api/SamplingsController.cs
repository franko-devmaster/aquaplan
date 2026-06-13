using System.Security.Claims;
using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.DTOs.Samplings;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/orders/{orderId:guid}/sampling")]
[ApiController]
[Authorize]
public class SamplingsController(
    ISamplingService samplingService,
    IOrderService orderService,
    IPermissionService permissionService,
    ILogger<SamplingsController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<SamplingDto>> GetSampling(Guid orderId, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var result = await samplingService.GetByOrderIdAsync(orderId, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<SamplingDto>> CreateSampling(
        Guid orderId, [FromBody] SamplingCreateDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        // Ensure the orderId in the route matches the DTO
        var correctedDto = dto with { OrderId = orderId };
        var result = await samplingService.CreateAsync(correctedDto, userId, tenantId, cancellationToken);
        return CreatedAtAction(nameof(GetSampling), new { orderId }, result);
    }

    [HttpPut]
    public async Task<ActionResult<SamplingDto>> UpdateSampling(
        Guid orderId, [FromBody] SamplingCreateDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var result = await samplingService.UpdateAsync(orderId, dto, userId, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpPost("complete")]
    public async Task<ActionResult> CompleteSampling(Guid orderId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var result = await samplingService.CompleteAsync(orderId, userId, tenantId, cancellationToken);
        if (!result) return NotFound();
        return Ok();
    }

    // Sprint Robustesse F-115 — validation is a quality-control step reserved to mandataires and
    // admins (not the field préleveur), and the caller must have access to the order. Previously
    // any authenticated tenant user could validate any order's sampling.
    [HttpPost("validate")]
    [Authorize(Roles = $"{RoleName.Administrator},{RoleName.Requerant},{RoleName.RequerantPreleveur}")]
    public async Task<ActionResult> ValidateSampling(Guid orderId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        var hasViewAll = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        if (!hasViewAll)
        {
            var canAccess = await orderService.UserCanAccessOrderAsync(userId, orderId, tenantId, cancellationToken);
            if (!canAccess)
            {
                var exists = await orderService.GetOrderByIdAsync(orderId, tenantId, cancellationToken);
                return exists is null ? NotFound() : Forbid();
            }
        }

        var result = await samplingService.ValidateAsync(orderId, userId, tenantId, cancellationToken);
        if (!result) return NotFound();
        return Ok();
    }

    [HttpPost("barcode")]
    public async Task<ActionResult<SamplingDto>> ScanBarcode(
        Guid orderId, [FromBody] BarcodeScanDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var result = await samplingService.ScanBarcodeAsync(orderId, dto.Barcode, userId, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
    }

    [HttpGet("~/api/samplings/by-barcode/{barcode}")]
    public async Task<ActionResult<SamplingDto>> GetByBarcode(string barcode, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var result = await samplingService.GetByBarcodeAsync(barcode, tenantId, cancellationToken);
        if (result is null) return NotFound();
        return Ok(result);
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
