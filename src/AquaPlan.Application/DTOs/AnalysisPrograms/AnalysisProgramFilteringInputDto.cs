namespace AquaPlan.Application.DTOs.AnalysisPrograms;

public record AnalysisProgramFilteringInputDto(
    string? Search,
    bool? IsActive);
