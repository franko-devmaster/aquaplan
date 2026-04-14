using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.Orders;

public record OrderDetailDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    bool IsUnplanned,
    UnplannedReason? UnplannedReason,
    string? UnplannedReasonDetails,
    string CreatedById,
    string? CreatedByName,
    string? PreleveurId,
    string? PreleveurName,
    Guid DistributorId,
    string DistributorName,
    Guid? SamplingLocationId,
    string? SamplingLocationName,
    DateTime? PlannedDate,
    string? Notes,
    bool IsDelegated,
    List<OrderAnalysisProfileDto> AnalysisProfiles,
    Guid TenantId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    SamplingDto? Sampling,
    Guid? SamplingRoundId);

public record OrderAnalysisProfileDto(
    Guid AnalysisProfileId,
    string Code,
    string Name);
