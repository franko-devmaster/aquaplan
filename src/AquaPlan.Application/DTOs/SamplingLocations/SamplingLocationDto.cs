namespace AquaPlan.Application.DTOs.SamplingLocations;

public record SamplingLocationDto(
    Guid Id,
    string Name,
    string LocationCode,
    string? Description,
    string? Address,
    string? AccessDescription,
    bool IsActive,
    bool IsValidated,
    Guid DistributorId,
    string? DistributorName,
    Guid SectorId,
    string? SectorName,
    DateTime CreatedAt,
    bool CanDelete = false);
