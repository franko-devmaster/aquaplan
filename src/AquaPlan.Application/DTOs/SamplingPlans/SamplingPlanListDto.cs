using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.SamplingPlans;

public record SamplingPlanListDto(
    Guid Id,
    int Year,
    SamplingPlanStatus Status,
    Guid DistributorId,
    string DistributorName,
    string CreatedById,
    string? CreatedByName,
    int ItemCount,
    DateTime CreatedAt);
