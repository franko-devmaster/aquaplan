namespace AquaPlan.Application.DTOs.Users;

public record UserListDto(
    string Id,
    int UserNumber,
    string Email,
    string FirstName,
    string LastName,
    string? Role,
    Guid? DistributorId,
    string? DistributorName,
    bool IsActive,
    Guid TenantId,
    DateTime CreatedAt);
