using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.SamplingPlans;

public record SamplingPlanDetailDto(
    Guid Id,
    int Year,
    SamplingPlanStatus Status,
    Guid DistributorId,
    string DistributorName,
    string CreatedById,
    string? CreatedByName,
    string? Notes,
    string? RejectionReason,
    List<SamplingPlanItemDto> Items,
    Guid TenantId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? StatusChangedAt);

public record SamplingPlanItemDto(
    Guid Id,
    Guid SamplingLocationId,
    string SamplingLocationName,
    string SamplingLocationCode,
    Guid AnalysisProfileId,
    string AnalysisProfileCode,
    string AnalysisProfileName,
    int FrequencyPerYear,
    List<int> PlannedMonths);
