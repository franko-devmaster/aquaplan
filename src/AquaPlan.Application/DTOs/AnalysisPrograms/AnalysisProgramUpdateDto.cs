using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.AnalysisPrograms;

public record AnalysisProgramUpdateDto(
    [Required] [MaxLength(50)] string Code,
    [Required] [MaxLength(200)] string Name,
    [MaxLength(1000)] string? Description,
    bool IsActive);
