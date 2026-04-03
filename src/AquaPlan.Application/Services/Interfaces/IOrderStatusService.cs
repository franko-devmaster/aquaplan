using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.Services.Interfaces;

public interface IOrderStatusService
{
    IList<OrderStatusDto> GetAllStatuses();
    IList<OrderStatusDto> GetAllowedTransitions(OrderStatus current);
    bool ValidateTransition(OrderStatus from, OrderStatus to);
    Task<OrderStatusTransitionDto> TransitionOrderAsync(Guid orderId, OrderStatus newStatus, string userId, Guid tenantId, CancellationToken cancellationToken = default);
}
