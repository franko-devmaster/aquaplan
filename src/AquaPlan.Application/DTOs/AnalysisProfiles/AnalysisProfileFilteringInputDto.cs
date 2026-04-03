using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.AnalysisProfiles;

public record AnalysisProfileFilteringInputDto(
    string? Search,
    AnalysisCategory? Category,
    bool? IsActive);
