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
    ILogger<OrderStatusService> logger) : IOrderStatusService
{
    private static readonly Dictionary<OrderStatus, OrderStatusDto> StatusDefinitions = new()
    {
        [OrderStatus.Draft] = new OrderStatusDto(OrderStatus.Draft, "Draft", "Order created, not yet assigned", "#9E9E9E", false),
        [OrderStatus.Assigned] = new OrderStatusDto(OrderStatus.Assigned, "Assigned", "Sampler assigned to the order", "#2196F3", false),
        [OrderStatus.InProgress] = new OrderStatusDto(OrderStatus.InProgress, "InProgress", "Sampling in progress", "#FF9800", false),
        [OrderStatus.SamplingCompleted] = new OrderStatusDto(OrderStatus.SamplingCompleted, "SamplingCompleted", "Sampling completed, awaiting validation", "#7B1FA2", false),
        [OrderStatus.Validated] = new OrderStatusDto(OrderStatus.Validated, "Validated", "Results validated, ready to send to LIMS", "#00BCD4", false),
        [OrderStatus.SentToLims] = new OrderStatusDto(OrderStatus.SentToLims, "SentToLims", "Order sent to Limsophy LIMS", "#3F51B5", false),
        [OrderStatus.ResultsReceived] = new OrderStatusDto(OrderStatus.ResultsReceived, "ResultsReceived", "Results received from LIMS", "#8BC34A", false),
        [OrderStatus.Completed] = new OrderStatusDto(OrderStatus.Completed, "Completed", "Order fully completed", "#4CAF50", true),
        [OrderStatus.Cancelled] = new OrderStatusDto(OrderStatus.Cancelled, "Cancelled", "Order cancelled", "#F44336", true),
    };

    private static readonly Dictionary<OrderStatus, OrderStatus[]> TransitionMatrix = new()
    {
        [OrderStatus.Draft] = [OrderStatus.Assigned, OrderStatus.Cancelled],
        [OrderStatus.Assigned] = [OrderStatus.InProgress, OrderStatus.Cancelled],
        [OrderStatus.InProgress] = [OrderStatus.SamplingCompleted, OrderStatus.Cancelled],
        [OrderStatus.SamplingCompleted] = [OrderStatus.Validated, OrderStatus.Cancelled],
        [OrderStatus.Validated] = [OrderStatus.SentToLims, OrderStatus.Cancelled],
        [OrderStatus.SentToLims] = [OrderStatus.ResultsReceived],
        [OrderStatus.ResultsReceived] = [OrderStatus.Completed],
        [OrderStatus.Completed] = [],
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

        logger.LogInformation(
            "Order {OrderId} transitioned from {FromStatus} to {ToStatus} by {UserId}",
            orderId, currentStatus, newStatus, userId);

        return new OrderStatusTransitionDto(currentStatus, newStatus, transitionDate);
    }
}
