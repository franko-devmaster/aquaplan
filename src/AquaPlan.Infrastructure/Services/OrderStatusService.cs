using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

internal class OrderStatusService(
    AquaPlanDbContext dbContext,
    IOrderAuditService auditService,
    IOrderTransmissionService transmissionService,
    ILogger<OrderStatusService> logger) : IOrderStatusService
{
    private static readonly Dictionary<OrderStatus, OrderStatusDto> StatusDefinitions = new()
    {
        [OrderStatus.New] = new OrderStatusDto(OrderStatus.New, "New", "Order created, waiting for sampling", "#9E9E9E", false),
        [OrderStatus.InProgress] = new OrderStatusDto(OrderStatus.InProgress, "InProgress", "Sampling in progress", "#FF9800", false),
        [OrderStatus.Completed] = new OrderStatusDto(OrderStatus.Completed, "Completed", "Sampling completed, ready to transmit", "#7B1FA2", false),
        [OrderStatus.Transmitted] = new OrderStatusDto(OrderStatus.Transmitted, "Transmitted", "Order sent to Limsophy LIMS", "#3F51B5", false),
        [OrderStatus.Done] = new OrderStatusDto(OrderStatus.Done, "Done", "Results received, order done", "#4CAF50", true),
        [OrderStatus.Cancelled] = new OrderStatusDto(OrderStatus.Cancelled, "Cancelled", "Order cancelled", "#F44336", true),
    };

    private static readonly Dictionary<OrderStatus, OrderStatus[]> TransitionMatrix = new()
    {
        [OrderStatus.New] = [OrderStatus.InProgress, OrderStatus.Cancelled],
        [OrderStatus.InProgress] = [OrderStatus.Completed, OrderStatus.Cancelled],
        [OrderStatus.Completed] = [OrderStatus.Transmitted, OrderStatus.Cancelled],
        [OrderStatus.Transmitted] = [OrderStatus.Done],
        [OrderStatus.Done] = [],
        [OrderStatus.Cancelled] = [],
    };

    public IList<OrderStatusDto> GetAllStatuses()
    {
        return StatusDefinitions.Values.ToList();
    }

    public IList<OrderStatusDto> GetAllowedTransitions(OrderStatus current)
    {
        if (!TransitionMatrix.TryGetValue(current, out var allowedStatuses))
        {
            return [];
        }

        return allowedStatuses
            .Select(s => StatusDefinitions[s])
            .ToList();
    }

    public bool ValidateTransition(OrderStatus from, OrderStatus to)
    {
        if (!TransitionMatrix.TryGetValue(from, out var allowedStatuses))
        {
            return false;
        }

        return allowedStatuses.Contains(to);
    }

    public async Task<OrderStatusTransitionDto> TransitionOrderAsync(Guid orderId, OrderStatus newStatus, string userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId, cancellationToken);

        if (order is null)
        {
            throw new KeyNotFoundException($"Order with ID {orderId} not found.");
        }

        var currentStatus = order.Status;

        if (!ValidateTransition(currentStatus, newStatus))
        {
            throw new InvalidOperationException(
                $"Transition from {currentStatus} to {newStatus} is not allowed.");
        }

        var transitionDate = DateTime.UtcNow;

        // Sprint Robustesse F-105 — the Completed → Transmitted transition is owned by the
        // shared transmission service so this single-order path no longer diverges from the
        // bulk/round paths (it previously skipped TransmittedAt + the Mock LIMS forward,
        // leaving Transmitted orders without a LimsOrderId the worker could never pull).
        if (newStatus == OrderStatus.Transmitted)
        {
            await transmissionService.TransmitCompletedOrdersAsync([order], userId, tenantId, cancellationToken);
        }
        else
        {
            order.Status = newStatus;
            order.StatusChangedAt = transitionDate;
            order.StatusChangedBy = userId;
            order.UpdatedAt = transitionDate;
            order.UpdatedBy = userId;

            await dbContext.SaveChangesAsync(cancellationToken);

            await auditService.LogAsync(
                orderId,
                "StatusTransitioned",
                $"Status changed from {currentStatus} to {newStatus}",
                currentStatus.ToString(),
                newStatus.ToString(),
                userId,
                tenantId,
                cancellationToken);
        }

        // Auto-complete round when all orders are transmitted/done
        if (order.SamplingRoundId.HasValue)
        {
            var round = await dbContext.SamplingRounds
                .Include(r => r.Orders)
                .FirstOrDefaultAsync(r => r.Id == order.SamplingRoundId.Value, cancellationToken);

            if (round is not null && round.Status == SamplingRoundStatus.InProgress)
            {
                if (round.Orders.All(o => o.Status is OrderStatus.Transmitted or OrderStatus.Done or OrderStatus.Cancelled))
                {
                    round.Status = SamplingRoundStatus.Completed;
                    round.CompletedAt = DateTime.UtcNow;
                    round.UpdatedAt = DateTime.UtcNow;
                    round.UpdatedBy = userId;
                    // F-215 (related) — releasing the lock on completion is handled by the
                    // round paths; here we keep the existing single-transition behaviour.
                    await dbContext.SaveChangesAsync(cancellationToken);
                }
            }
        }

        logger.LogInformation(
            "Order {OrderId} transitioned from {FromStatus} to {ToStatus} by {UserId}",
            orderId, currentStatus, newStatus, userId);

        return new OrderStatusTransitionDto(currentStatus, newStatus, transitionDate);
    }
}
