namespace AquaPlan.Application.DTOs.Containers;

public record ContainerListDto(
    Guid Id,
    string Code,
    string Name,
    string Material,
    int VolumeMl,
    string Color,
    bool IsActive);
