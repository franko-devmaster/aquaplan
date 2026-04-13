namespace AquaPlan.Application.DTOs.Distributors;

public record DistributorListDto(
    Guid Id,
    string Name,
    string? ShortName,
    string? CantonRegion,
    string? DistributionNetwork,
    bool IsActive,
    DateTime CreatedAt);
