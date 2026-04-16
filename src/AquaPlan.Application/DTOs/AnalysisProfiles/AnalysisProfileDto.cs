using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.AnalysisProfiles;

public record AnalysisProfileDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    AnalysisCategory Category,
    bool IsActive,
    Guid ContainerId,
    string ContainerCode,
    string ContainerName,
    DateTime CreatedAt);
