namespace AquaPlan.Application.DTOs.Containers;

public record ContainerDto(
    Guid Id,
    string Code,
    string Name,
    string Material,
    int VolumeMl,
    string Color,
    bool IsActive,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
