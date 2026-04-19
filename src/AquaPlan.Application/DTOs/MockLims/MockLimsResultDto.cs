namespace AquaPlan.Application.DTOs.MockLims;

public record MockLimsResultDto(
    string ParameterCode,
    decimal Value,
    string Unit,
    decimal? ReferenceMin,
    decimal? ReferenceMax,
    bool IsConform);

public record MockLimsResultListDto(
    Guid LimsOrderId,
    IReadOnlyList<MockLimsResultDto> Results);
