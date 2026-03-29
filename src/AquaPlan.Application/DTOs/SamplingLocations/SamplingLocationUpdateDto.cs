namespace AquaPlan.Application.DTOs.SamplingLocations;

public record SamplingLocationUpdateDto(
    string Name,
    string LocationCode,
    double? Latitude,
    double? Longitude,
    string? Description,
    bool IsActive);
