using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.AnalysisProfiles;

public record AnalysisProfileListDto(
    Guid Id,
    string Code,
    string Name,
    AnalysisCategory Category,
    bool IsActive,
    Guid ContainerId,
    string ContainerCode);
