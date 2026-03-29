namespace AquaPlan.Application.DTOs.Users;

public record UserListDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? Organization,
    bool IsActive,
    Guid TenantId,
    IList<string> Roles,
    DateTime CreatedAt);
