using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.Orders;

// Polish F-220 — Notes length mirrors the EF column limit (orders.notes 2000).
public record OrderUpdateDto(
    Guid? SamplingLocationId,
    string? PreleveurId,
    DateTime? PlannedDate,
    List<Guid>? AnalysisProgramIds,
    [property: StringLength(2000)] string? Notes);
