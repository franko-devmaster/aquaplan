namespace AquaPlan.Application.DTOs.Sectors;

public record SectorFilteringInputDto(
    string? Name,
    bool? IsActive);
