namespace AquaPlan.Application.DTOs.SamplingResults;

public record SamplingResultDto(
    Guid Id,
    string ParameterCode,
    decimal Value,
    string Unit,
    decimal? ReferenceMin,
    decimal? ReferenceMax,
    bool IsConform,
    DateTime ReceivedAt);

public record SamplingResultListDto(
    IReadOnlyList<SamplingResultDto> Items,
    int ConformCount,
    int NonConformCount,
    int TotalCount);
