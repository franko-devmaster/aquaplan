using System.Security.Claims;
using AquaPlan.Application.DTOs.ChangeRequests;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/sampling-location-requests")]
[ApiController]
[Authorize]
public class SamplingLocationChangeRequestsController(
    ISamplingLocationChangeRequestService changeRequestService,
    ILogger<SamplingLocationChangeRequestsController> logger) : ControllerBase
{
    [HttpPost("create")]
    public async Task<ActionResult<ChangeRequestDto>> SubmitCreateRequest([FromBody] ChangeRequestCreateDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        try
        {
            var result = await changeRequestService.SubmitCreateRequestAsync(dto, userId, tenantId, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("{slId:guid}/update")]
    public async Task<ActionResult<ChangeRequestDto>> SubmitUpdateRequest(Guid slId, [FromBody] ChangeRequestUpdateDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        try
        {
            var result = await changeRequestService.SubmitUpdateRequestAsync(slId, dto, userId, tenantId, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("{slId:guid}/deactivate")]
    public async Task<ActionResult<ChangeRequestDto>> SubmitDeactivateRequest(Guid slId, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        try
        {
            var result = await changeRequestService.SubmitDeactivateRequestAsync(slId, userId, tenantId, cancellationToken);
            return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpGet("my")]
    public async Task<ActionResult<IList<ChangeRequestDto>>> GetMyRequests(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var requests = await changeRequestService.GetMyRequestsAsync(userId, tenantId, cancellationToken);
        return Ok(requests);
    }

    [HttpGet("pending")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<IList<ChangeRequestDto>>> GetPendingRequests(CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var requests = await changeRequestService.GetPendingRequestsAsync(tenantId, cancellationToken);
        return Ok(requests);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ChangeRequestDto>> GetById(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var request = await changeRequestService.GetByIdAsync(id, tenantId, cancellationToken);
        if (request is null)
        {
            return NotFound();
        }
        return Ok(request);
    }

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<ChangeRequestDto>> Approve(Guid id, [FromBody] ChangeRequestReviewDto dto, CancellationToken cancellationToken)
    {
        var reviewerId = GetUserId();
        var tenantId = GetTenantId();
        var result = await changeRequestService.ApproveAsync(id, dto.Comment, reviewerId, tenantId, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }
        return Ok(result);
    }

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<ChangeRequestDto>> Reject(Guid id, [FromBody] ChangeRequestReviewDto dto, CancellationToken cancellationToken)
    {
        var reviewerId = GetUserId();
        var tenantId = GetTenantId();

        if (string.IsNullOrWhiteSpace(dto.Comment))
        {
            return BadRequest(new { error = "A comment is required when rejecting a request." });
        }

        var result = await changeRequestService.RejectAsync(id, dto.Comment, reviewerId, tenantId, cancellationToken);
        if (result is null)
        {
            return NotFound();
        }
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
