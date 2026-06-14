using AquaPlan.Application.DTOs.Orders;

namespace AquaPlan.Application.Services.Interfaces;

/// <summary>
/// Polish F-214 — a single audit entry, used by <see cref="IOrderAuditService.LogRangeAsync"/> to
/// persist many entries in one SaveChanges instead of one round-trip per order.
/// </summary>
public readonly record struct OrderAuditEntry(
    Guid OrderId,
    string Action,
    string? Details,
    string? OldValue,
    string? NewValue,
    string PerformedById);

public interface IOrderAuditService
{
    Task<List<OrderAuditLogDto>> GetByOrderIdAsync(Guid orderId, Guid tenantId, CancellationToken cancellationToken = default);
    Task LogAsync(Guid orderId, string action, string? details, string? oldValue, string? newValue, string performedById, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Polish F-214 — persists a batch of audit entries with a single SaveChanges, instead of one
    /// save per entry as the caller loop would otherwise produce.
    /// </summary>
    Task LogRangeAsync(IReadOnlyCollection<OrderAuditEntry> entries, Guid tenantId, CancellationToken cancellationToken = default);
}
