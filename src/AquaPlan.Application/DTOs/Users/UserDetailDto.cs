namespace AquaPlan.Application.DTOs.Users;

public record UserDetailDto(
    string Id,
    string Email,
    string FirstName,
    string LastName,
    string? Organization,
    bool IsActive,
    Guid TenantId,
    IList<string> Roles,
    IList<DistributorSummaryDto> Distributors,
    DateTime CreatedAt,
    DateTime? UpdatedAt);

public record DistributorSummaryDto(Guid Id, string Name);
