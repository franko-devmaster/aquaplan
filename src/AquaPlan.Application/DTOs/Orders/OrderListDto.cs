using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.Orders;

public record OrderListDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    bool IsUnplanned,
    UnplannedReason? UnplannedReason,
    string CreatedById,
    string? CreatedByName,
    string? PreleveurId,
    string? PreleveurName,
    Guid DistributorId,
    string DistributorName,
    Guid? SamplingLocationId,
    string? SamplingLocationName,
    DateTime? PlannedDate,
    bool IsDelegated,
    DateTime CreatedAt);
