namespace AquaPlan.Domain.Entities;

public class RolePermission
{
    public string RoleId { get; set; } = string.Empty;
    public ApplicationRole? Role { get; set; }
    public Guid PermissionId { get; set; }
    public Permission? Permission { get; set; }
}
