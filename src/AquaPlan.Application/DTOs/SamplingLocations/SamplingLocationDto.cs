namespace AquaPlan.Application.DTOs.SamplingLocations;

public record SamplingLocationDto(
    Guid Id,
    string Name,
    string LocationCode,
    double? Latitude,
    double? Longitude,
    string? Description,
    string? Address,
    string? AccessDescription,
    bool IsActive,
    Guid DistributorId,
    string? DistributorName,
    Guid SectorId,
    string? SectorName,
    DateTime CreatedAt);
