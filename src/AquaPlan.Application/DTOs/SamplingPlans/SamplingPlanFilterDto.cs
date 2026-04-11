using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.SamplingPlans;

public record SamplingPlanFilterDto(
    List<SamplingPlanStatus>? Statuses = null,
    string? Search = null,
    int? Year = null,
    Guid? DistributorId = null,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    bool SortDescending = true);
