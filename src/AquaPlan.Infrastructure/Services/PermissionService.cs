using AquaPlan.Application.DTOs.Roles;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

internal class PermissionService(
    RoleManager<ApplicationRole> roleManager,
    UserManager<AppUser> userManager,
    AquaPlanDbContext dbContext,
    ILogger<PermissionService> logger) : IPermissionService
{
    public async Task<IList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default)
    {
        var roles = await roleManager.Roles
            .OrderBy(r => r.Name)
            .ToListAsync(cancellationToken);

        return roles.Select(r => new RoleDto(r.Id, r.Name ?? string.Empty, r.Description, r.CreatedAt)).ToList();
    }

    public async Task<RoleWithPermissionsDto?> GetRoleWithPermissionsAsync(string roleId, CancellationToken cancellationToken = default)
    {
        var role = await roleManager.FindByIdAsync(roleId);
        if (role is null)
        {
            return null;
        }

        var permissions = await dbContext.RolePermissions
            .Where(rp => rp.RoleId == roleId)
            .Include(rp => rp.Permission)
            .Select(rp => new PermissionDto(rp.Permission!.Id, rp.Permission.Name, rp.Permission.Description))
            .ToListAsync(cancellationToken);

        return new RoleWithPermissionsDto(role.Id, role.Name ?? string.Empty, role.Description, permissions);
    }

    public async Task<IList<PermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken = default)
    {
        return await dbContext.Permissions
            .OrderBy(p => p.Name)
            .Select(p => new PermissionDto(p.Id, p.Name, p.Description))
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> AssignRoleToUserAsync(string userId, string roleName, Guid callerTenantId, CancellationToken cancellationToken = default)
    {
        // F-103 — reject unknown role names so a caller cannot create an arbitrary role
        // membership or probe role existence.
        if (!RoleName.All.Contains(roleName))
        {
            return false;
        }

        var user = await userManager.FindByIdAsync(userId);
        // F-103 — tenant isolation: the target user must belong to the caller's tenant.
        if (user is null || user.TenantId != callerTenantId)
        {
            return false;
        }

        if (await userManager.IsInRoleAsync(user, roleName))
        {
            return true;
        }

        var result = await userManager.AddToRoleAsync(user, roleName);
        if (result.Succeeded)
        {
            logger.LogInformation("Role {Role} assigned to user {UserId}", roleName, userId);
        }
        return result.Succeeded;
    }

    public async Task<bool> RemoveRoleFromUserAsync(string userId, string roleName, Guid callerTenantId, CancellationToken cancellationToken = default)
    {
        if (!RoleName.All.Contains(roleName))
        {
            return false;
        }

        var user = await userManager.FindByIdAsync(userId);
        // F-103 — tenant isolation: the target user must belong to the caller's tenant.
        if (user is null || user.TenantId != callerTenantId)
        {
            return false;
        }

        if (!await userManager.IsInRoleAsync(user, roleName))
        {
            return true;
        }

        var result = await userManager.RemoveFromRoleAsync(user, roleName);
        if (result.Succeeded)
        {
            logger.LogInformation("Role {Role} removed from user {UserId}", roleName, userId);
        }
        return result.Succeeded;
    }

    public async Task<IList<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default)
    {
        var user = await userManager.FindByIdAsync(userId);
        if (user is null)
        {
            return [];
        }

        var roles = await userManager.GetRolesAsync(user);
        var roleIds = await roleManager.Roles
            .Where(r => roles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);

        return await dbContext.RolePermissions
            .Where(rp => roleIds.Contains(rp.RoleId))
            .Include(rp => rp.Permission)
            .Select(rp => rp.Permission!.Name)
            .Distinct()
            .ToListAsync(cancellationToken);
    }

    public async Task<bool> UserHasPermissionAsync(string userId, string permissionName, CancellationToken cancellationToken = default)
    {
        var permissions = await GetUserPermissionsAsync(userId, cancellationToken);
        return permissions.Contains(permissionName);
    }
}
