using AquaPlan.Application.DTOs.Orders;

namespace AquaPlan.Application.Services.Interfaces;

public interface IOrderService
{
    Task<IList<OrderListDto>> GetOrdersForUserAsync(string userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IList<OrderListDto>> GetAllOrdersAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<OrderDetailDto?> GetOrderByIdAsync(Guid orderId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<OrderDetailDto> CreateOrderAsync(OrderCreateDto dto, string createdById, Guid tenantId, CancellationToken cancellationToken = default);
    Task<OrderDetailDto?> AssignPreleveurAsync(Guid orderId, OrderAssignDto dto, string updatedBy, Guid tenantId, CancellationToken cancellationToken = default);
}
