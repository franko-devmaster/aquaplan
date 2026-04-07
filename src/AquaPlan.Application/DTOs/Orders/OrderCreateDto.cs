using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.Orders;

public record OrderCreateDto(
    Guid DistributorId,
    Guid? SamplingLocationId,
    string? PreleveurId,
    DateTime? PlannedDate,
    List<Guid>? AnalysisProfileIds,
    string? Notes,
    bool IsUnplanned,
    UnplannedReason? UnplannedReason = null,
    string? UnplannedReasonDetails = null);
