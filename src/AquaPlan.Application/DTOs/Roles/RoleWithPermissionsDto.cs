namespace AquaPlan.Application.DTOs.Roles;

public record RoleWithPermissionsDto(
    string Id,
    string Name,
    string? Description,
    IList<PermissionDto> Permissions);

public record PermissionDto(Guid Id, string Name, string? Description);
