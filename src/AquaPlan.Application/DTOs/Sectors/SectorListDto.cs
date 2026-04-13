namespace AquaPlan.Application.DTOs.Sectors;

public record SectorListDto(
    Guid Id,
    string Name,
    string Code,
    string? Description,
    bool IsActive,
    Guid DistributorId,
    string? DistributorName,
    DateTime CreatedAt);
