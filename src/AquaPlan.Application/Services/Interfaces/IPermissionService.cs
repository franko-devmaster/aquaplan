using AquaPlan.Application.DTOs.Roles;

namespace AquaPlan.Application.Services.Interfaces;

public interface IPermissionService
{
    Task<IList<RoleDto>> GetRolesAsync(CancellationToken cancellationToken = default);
    Task<RoleWithPermissionsDto?> GetRoleWithPermissionsAsync(string roleId, CancellationToken cancellationToken = default);
    Task<IList<PermissionDto>> GetPermissionsAsync(CancellationToken cancellationToken = default);
    Task<bool> AssignRoleToUserAsync(string userId, string roleName, CancellationToken cancellationToken = default);
    Task<bool> RemoveRoleFromUserAsync(string userId, string roleName, CancellationToken cancellationToken = default);
    Task<IList<string>> GetUserPermissionsAsync(string userId, CancellationToken cancellationToken = default);
    Task<bool> UserHasPermissionAsync(string userId, string permissionName, CancellationToken cancellationToken = default);
}
