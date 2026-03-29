using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.Orders;

public record OrderDetailDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    bool IsUnplanned,
    string CreatedById,
    string? CreatedByName,
    string? PreleveurId,
    string? PreleveurName,
    Guid DistributorId,
    string DistributorName,
    Guid TenantId,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    SamplingDto? Sampling);
