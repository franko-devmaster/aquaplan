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
    string? SectorName,
    DateTime? PlannedDate,
    string? Notes,
    bool IsDelegated,
    List<OrderAnalysisProgramDto> AnalysisPrograms,
    Guid TenantId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    SamplingDto? Sampling,
    Guid? SamplingRoundId,
    bool IsRoundLocked = false,
    string? RoundLockedById = null,
    string? RoundLockedByName = null,
    ResultsStatus ResultsStatus = ResultsStatus.NotReceived);

public record OrderAnalysisProgramDto(
    Guid AnalysisProgramId,
    string Code,
    string Name);
