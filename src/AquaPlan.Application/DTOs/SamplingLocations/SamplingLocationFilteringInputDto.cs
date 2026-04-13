namespace AquaPlan.Application.DTOs.SamplingLocations;

public record SamplingLocationFilteringInputDto(
    Guid? DistributorId = null,
    Guid? SectorId = null,
    string? Search = null,
    bool? IsActive = null,
    int Page = 1,
    int PageSize = 25);
