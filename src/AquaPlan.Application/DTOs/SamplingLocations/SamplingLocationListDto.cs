namespace AquaPlan.Application.DTOs.SamplingLocations;

public record SamplingLocationListDto(
    IList<SamplingLocationDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
