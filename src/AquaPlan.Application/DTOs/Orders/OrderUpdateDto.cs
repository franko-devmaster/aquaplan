namespace AquaPlan.Application.DTOs.Orders;

public record OrderUpdateDto(
    Guid? SamplingLocationId,
    string? PreleveurId,
    DateTime? PlannedDate,
    List<Guid>? AnalysisProfileIds,
    string? Notes);
