using AquaPlan.Application.DTOs.AnalysisProfiles;

namespace AquaPlan.Application.DTOs.AnalysisPrograms;

public record AnalysisProgramDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    DateTime CreatedAt,
    IList<AnalysisProfileListDto> Profiles);
