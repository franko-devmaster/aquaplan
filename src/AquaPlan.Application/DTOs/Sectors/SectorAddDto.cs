using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.Sectors;

public record SectorAddDto(
    [Required] string Name,
    [Required] string Code,
    string? Description,
    [Required] Guid DistributorId);
