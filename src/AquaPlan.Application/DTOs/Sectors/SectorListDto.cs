namespace AquaPlan.Application.DTOs.Sectors;

public record SectorListDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    DateTime CreatedAt);
