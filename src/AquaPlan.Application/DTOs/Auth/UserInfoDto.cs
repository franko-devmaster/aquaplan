namespace AquaPlan.Application.DTOs.Auth;

public record UserInfoDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? Organization,
    Guid TenantId,
    IList<string> Roles);
