namespace AquaPlan.Application.DTOs.SamplingPlans;

public record SamplingPlanCreateDto(
    Guid DistributorId,
    int Year,
    string? Notes,
    List<SamplingPlanItemCreateDto> Items);

public record SamplingPlanItemCreateDto(
    Guid SamplingLocationId,
    Guid AnalysisProfileId,
    int FrequencyPerYear,
    List<int> PlannedMonths);
