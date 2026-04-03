using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.SamplingLocations;

public record SamplingLocationUpdateDto(
    [Required][StringLength(200)] string Name,
    [Required][StringLength(50)] string LocationCode,
    double? Latitude,
    double? Longitude,
    [StringLength(1000)] string? Description,
    bool IsActive);
