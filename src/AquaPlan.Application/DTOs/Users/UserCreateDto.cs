namespace AquaPlan.Application.DTOs.Users;

public record UserCreateDto(
    string Email,
    string FirstName,
    string LastName,
    string? Organization,
    string Password,
    Guid TenantId,
    List<string> Roles,
    List<Guid> DistributorIds);
