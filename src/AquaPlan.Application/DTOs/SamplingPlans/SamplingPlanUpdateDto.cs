namespace AquaPlan.Application.DTOs.SamplingPlans;

public record SamplingPlanUpdateDto(
    string? Notes,
    List<SamplingPlanItemCreateDto> Items);
