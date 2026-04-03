namespace AquaPlan.Application.DTOs.AnalysisPrograms;

public record AnalysisProgramListDto(
    Guid Id,
    string Code,
    string Name,
    bool IsActive,
    int ProfileCount);
