using AquaPlan.Application.DTOs.Orders;

namespace AquaPlan.Application.Services.Interfaces;

public interface IOrderAuditService
{
    Task<List<OrderAuditLogDto>> GetByOrderIdAsync(Guid orderId, Guid tenantId, CancellationToken cancellationToken = default);
    Task LogAsync(Guid orderId, string action, string? details, string? oldValue, string? newValue, string performedById, Guid tenantId, CancellationToken cancellationToken = default);
}
