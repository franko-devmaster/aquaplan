namespace AquaPlan.Application.DTOs.Users;

public record UserCreateDto(
    string Email,
    string FirstName,
    string LastName,
    string Password,
    Guid TenantId,
    string? Role,
    Guid? DistributorId);
