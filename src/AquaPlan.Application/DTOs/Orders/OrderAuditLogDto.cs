namespace AquaPlan.Application.DTOs.Orders;

public record OrderAuditLogDto(
    Guid Id,
    Guid OrderId,
    string Action,
    string? Details,
    string? OldValue,
    string? NewValue,
    string PerformedById,
    string? PerformedByName,
    DateTime PerformedAt);
