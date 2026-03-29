namespace AquaPlan.Application.DTOs.SamplingLocations;

public record SamplingLocationCreateDto(
    string Name,
    string LocationCode,
    double? Latitude,
    double? Longitude,
    string? Description,
    Guid DistributorId);
