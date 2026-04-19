using AquaPlan.Application.DTOs.MockLims;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace AquaPlan.Api.Controllers.Api;

/// <summary>
/// AQ-32 — Internal Mock LIMS endpoints used to simulate the LIMS outbound/inbound loop.
/// Reachable only when configuration <c>MockLims:Enabled=true</c>. Administrator-only.
/// </summary>
[Route("api/mock-lims")]
[ApiController]
[Authorize(Roles = "Administrator")]
public class MockLimsController(
    IMockLimsService mockLimsService,
    IOptions<MockLimsOptions> options,
    ILogger<MockLimsController> logger) : ControllerBase
{
    [HttpPost("orders")]
    public async Task<ActionResult<MockLimsOrderCreatedDto>> CreateOrder(
        [FromBody] MockLimsOrderCreateDto dto,
        CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return NotFound();
        }

        var tenantId = GetTenantId();
        try
        {
            var created = await mockLimsService.ReceiveOrderAsync(dto, tenantId, cancellationToken);
            logger.LogInformation("Mock LIMS received order {OrderReference} -> {LimsOrderId}", dto.OrderReference, created.LimsOrderId);
            return StatusCode(StatusCodes.Status201Created, created);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("orders/{limsOrderId:guid}")]
    public async Task<ActionResult<MockLimsOrderDto>> GetOrder(
        Guid limsOrderId,
        CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return NotFound();
        }

        var tenantId = GetTenantId();
        var order = await mockLimsService.GetAsync(limsOrderId, tenantId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }
        return Ok(order);
    }

    [HttpGet("orders/{limsOrderId:guid}/results")]
    public async Task<ActionResult<MockLimsResultListDto>> GetResults(
        Guid limsOrderId,
        CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled)
        {
            return NotFound();
        }

        var tenantId = GetTenantId();
        var results = await mockLimsService.GetResultsAsync(limsOrderId, tenantId, cancellationToken);
        if (results is null)
        {
            return NotFound();
        }
        return Ok(results);
    }

    private Guid GetTenantId()
    {
        var tenantClaim = User.FindFirst("tenant_id")?.Value ?? throw new UnauthorizedAccessException();
        return Guid.Parse(tenantClaim);
    }
}

