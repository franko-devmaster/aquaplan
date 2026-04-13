using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.Sectors;

public record SectorUpdateDto(
    [Required] string Name,
    [Required] string Code,
    string? Description,
    [Required] Guid DistributorId);
