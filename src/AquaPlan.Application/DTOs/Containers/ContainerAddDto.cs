using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.Containers;

public record ContainerAddDto(
    [Required] [MaxLength(50)] string Code,
    [Required] [MaxLength(200)] string Name,
    [Required] [MaxLength(100)] string Material,
    [Range(1, 100000)] int VolumeMl,
    [Required] [MaxLength(50)] string Color);
