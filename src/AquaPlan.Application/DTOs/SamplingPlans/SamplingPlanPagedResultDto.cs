namespace AquaPlan.Application.DTOs.SamplingPlans;

public record SamplingPlanPagedResultDto(
    List<SamplingPlanListDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
