using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AquaPlan.Infrastructure.Services;

internal class OrderAuditService(AquaPlanDbContext dbContext) : IOrderAuditService
{
    public async Task<List<OrderAuditLogDto>> GetByOrderIdAsync(
        Guid orderId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.OrderAuditLogs
            .Include(a => a.PerformedBy)
            .Where(a => a.OrderId == orderId && a.TenantId == tenantId)
            .OrderByDescending(a => a.PerformedAt)
            .Select(a => new OrderAuditLogDto(
                a.Id,
                a.OrderId,
                a.Action,
                a.Details,
                a.OldValue,
                a.NewValue,
                a.PerformedById,
                a.PerformedBy != null ? a.PerformedBy.FirstName + " " + a.PerformedBy.LastName : null,
                a.PerformedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task LogAsync(
        Guid orderId, string action, string? details, string? oldValue, string? newValue,
        string performedById, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var log = new OrderAuditLog
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            Action = action,
            Details = details,
            OldValue = oldValue,
            NewValue = newValue,
            PerformedById = performedById,
            PerformedAt = DateTime.UtcNow,
            TenantId = tenantId,
        };

        dbContext.OrderAuditLogs.Add(log);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
