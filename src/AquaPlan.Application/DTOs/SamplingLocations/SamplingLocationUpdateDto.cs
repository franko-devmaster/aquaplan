using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.SamplingLocations;

public record SamplingLocationUpdateDto(
    [Required][StringLength(200)] string Name,
    [Required][StringLength(50)] string LocationCode,
    [StringLength(1000)] string? Description,
    [StringLength(500)] string? Address,
    [StringLength(1000)] string? AccessDescription,
    bool IsActive,
    [Required] Guid SectorId);
