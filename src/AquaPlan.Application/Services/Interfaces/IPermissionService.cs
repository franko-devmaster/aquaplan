using AquaPlan.Application.DTOs.Roles;

namespace AquaPlan.Application.Services.Interfaces;

public interface IPermissionService
{
    Task<IList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<RoleWithPermissionsDto?> GetRoleWithPermissionsAsync(string roleId, CancellationToken cancellationToken = default);
    Task<IList<PermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken = default);
    /// <summary>
    /// Sprint Robustesse F-103 — assigns a role only when the target user belongs to
    /// <paramref name="callerTenantId"/> and <paramref name="roleName"/> is a known role.
    /// </summary>
    Task<bool> AssignRoleToUserAsync(string userId, string roleName, Guid callerTenantId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Sprint Robustesse F-103 — removes a role only when the target user belongs to
    /// <paramref name="callerTenantId"/> and <paramref name="roleName"/> is a known role.
    /// </summary>
    Task<bool> RemoveRoleFromUserAsync(string userId, string roleName, Guid callerTenantId, CancellationToken cancellationToken = default);
    Task<IList<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> UserHasPermissionAsync(string userId, string permissionName, CancellationToken cancellationToken = default);
}
