using System.Security.Claims;
using AquaPlan.Application.DTOs.Users;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class UsersController(
    IUserManagementService userManagementService,
    ILogger<UsersController> logger) : ControllerBase
{
    [HttpGet]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<IList<UserListDto>>> GetUsers(
        [FromQuery] string? role = null,
        [FromQuery] Guid? distributorId = null,
        [FromQuery] bool? isActive = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var users = await userManagementService.GetUsersAsync(tenantId, role, distributorId, isActive, cancellationToken);
        return Ok(users);
    }

    [HttpGet("preleveurs")]
    [Authorize]
    public async Task<ActionResult<IList<UserListDto>>> GetPreleveurs(
        [FromQuery] Guid? distributorId = null,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetTenantId();
        var users = await userManagementService.GetUsersAsync(tenantId, null, distributorId, true, cancellationToken);
        var preleveurs = users.Where(u =>
            u.Role != null && (
                u.Role.Contains("réleveur", StringComparison.OrdinalIgnoreCase) ||
                u.Role.Contains("releveur", StringComparison.OrdinalIgnoreCase)
            )).ToList();
        return Ok(preleveurs);
    }

    [HttpGet("{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<UserDetailDto>> GetUser(string id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var user = await userManagementService.GetUserByIdAsync(id, tenantId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }
        return Ok(user);
    }

    [HttpPost]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<UserDetailDto>> CreateUser([FromBody] UserCreateDto dto, CancellationToken cancellationToken)
    {
        // Sprint Sec F-004 — the user is created in the caller's tenant (JWT claim),
        // never in a client-supplied tenant.
        var createdBy = GetUserId();
        var tenantId = GetTenantId();
        var user = await userManagementService.CreateUserAsync(dto, createdBy, tenantId, cancellationToken);
        return CreatedAtAction(nameof(GetUser), new { id = user.Id }, user);
    }

    [HttpPut("{id}")]
    [Authorize(Roles = "Administrator")]
    public async Task<ActionResult<UserDetailDto>> UpdateUser(string id, [FromBody] UserUpdateDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var updatedBy = GetUserId();
        var user = await userManagementService.UpdateUserAsync(id, dto, updatedBy, tenantId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }
        return Ok(user);
    }

    [HttpPost("{id}/deactivate")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> DeactivateUser(string id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var updatedBy = GetUserId();
        var result = await userManagementService.DeactivateUserAsync(id, updatedBy, tenantId, cancellationToken);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpPost("{id}/activate")]
    [Authorize(Roles = "Administrator")]
    public async Task<IActionResult> ActivateUser(string id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var updatedBy = GetUserId();
        var result = await userManagementService.ActivateUserAsync(id, updatedBy, tenantId, cancellationToken);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpGet("me")]
    public async Task<ActionResult<UserDetailDto>> GetCurrentUser(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var user = await userManagementService.GetUserByIdAsync(userId, tenantId, cancellationToken);
        if (user is null)
        {
            return NotFound();
        }
        return Ok(user);
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
