using System.Security.Claims;
using AquaPlan.Application.DTOs.Roles;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize(Roles = "Administrator")]
public class RolesController(
    IPermissionService permissionService,
    ILogger<RolesController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IList<RoleDto>>> GetRoles(CancellationToken cancellationToken)
    {
        var roles = await permissionService.GetRolesAsync(cancellationToken);
        return Ok(roles);
    }

    [HttpGet("{id}")]
    public async Task<ActionResult<RoleWithPermissionsDto>> GetRole(string id, CancellationToken cancellationToken)
    {
        var role = await permissionService.GetRoleWithPermissionsAsync(id, cancellationToken);
        if (role is null)
        {
            return NotFound();
        }
        return Ok(role);
    }

    [HttpGet("permissions")]
    public async Task<ActionResult<IList<PermissionDto>>> GetPermissions(CancellationToken cancellationToken)
    {
        var permissions = await permissionService.GetPermissionsAsync(cancellationToken);
        return Ok(permissions);
    }

    [HttpPost("users/{userId}/assign")]
    public async Task<IActionResult> AssignRole(string userId, [FromBody] RoleAssignDto dto, CancellationToken cancellationToken)
    {
        // F-103 — pass the caller's tenant so a tenant-A admin cannot grant roles in tenant B.
        var callerTenantId = GetTenantId();
        var result = await permissionService.AssignRoleToUserAsync(userId, dto.RoleName, callerTenantId, cancellationToken);
        if (!result)
        {
            return NotFound();
        }
        return NoContent();
    }

    [HttpPost("users/{userId}/remove")]
    public async Task<IActionResult> RemoveRole(string userId, [FromBody] RoleAssignDto dto, CancellationToken cancellationToken)
    {
        var callerTenantId = GetTenantId();
        var result = await permissionService.RemoveRoleFromUserAsync(userId, dto.RoleName, callerTenantId, cancellationToken);
        if (!result)
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
