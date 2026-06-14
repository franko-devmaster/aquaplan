using System.ComponentModel.DataAnnotations;
using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.Orders;

// Polish F-220 — string lengths mirror the EF column limits (orders.notes 2000,
// orders.unplanned_reason_details 1000) so over-long input is rejected with a 400 at the
// controller instead of bubbling a DbUpdateException as a 500.
public record OrderCreateDto(
    [property: Required] Guid DistributorId,
    Guid? SamplingLocationId,
    string? PreleveurId,
    DateTime? PlannedDate,
    List<Guid>? AnalysisProgramIds,
    [property: StringLength(2000)] string? Notes,
    bool IsUnplanned,
    UnplannedReason? UnplannedReason = null,
    [property: StringLength(1000)] string? UnplannedReasonDetails = null);
