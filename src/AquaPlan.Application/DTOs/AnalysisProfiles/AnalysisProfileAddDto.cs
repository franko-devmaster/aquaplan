using System.ComponentModel.DataAnnotations;
using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.AnalysisProfiles;

public record AnalysisProfileAddDto(
    [Required] [MaxLength(50)] string Code,
    [Required] [MaxLength(200)] string Name,
    [MaxLength(1000)] string? Description,
    AnalysisCategory Category);
