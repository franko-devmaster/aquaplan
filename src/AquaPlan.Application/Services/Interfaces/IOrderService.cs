using AquaPlan.Application.DTOs.Orders;

namespace AquaPlan.Application.Services.Interfaces;

public interface IOrderService
{
    /// <summary>
    /// AQ-420 — Role-based visibility:
    ///   • Admin: full tenant view.
    ///   • Préleveur (sole role): only orders where they are the assigned préleveur.
    ///   • Requérant / Requérant-Préleveur: every order of the distributors they are
    ///     authorized on (own + active delegations), regardless of creator.
    /// </summary>
    Task<OrderPagedResultDto> GetOrdersFilteredAsync(string userId, Guid tenantId, OrderFilterDto filter, bool isAdmin, bool isPreleveurOnly, CancellationToken cancellationToken = default);
    Task<OrderDetailDto?> GetOrderByIdAsync(Guid orderId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<OrderDetailDto> CreateOrderAsync(OrderCreateDto dto, string createdById, Guid tenantId, CancellationToken cancellationToken = default);
    Task<OrderDetailDto?> UpdateOrderAsync(Guid orderId, OrderUpdateDto dto, string updatedBy, Guid tenantId, bool isAdmin = false, CancellationToken cancellationToken = default);
    Task<bool> DeleteOrderAsync(Guid orderId, string deletedBy, Guid tenantId, bool isAdmin = false, CancellationToken cancellationToken = default);
    Task<OrderDetailDto?> AssignPreleveurAsync(Guid orderId, OrderAssignDto dto, string updatedBy, Guid tenantId, CancellationToken cancellationToken = default);
    Task<byte[]> ExportOrdersCsvAsync(Guid tenantId, OrderFilterDto filter, CancellationToken cancellationToken = default);
    Task<bool> UserHasDistributorAccessAsync(string userId, Guid distributorId, CancellationToken cancellationToken = default);
    Task<bool> UserCanAccessOrderAsync(string userId, Guid orderId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IList<RequiredContainerDto>?> GetRequiredContainersAsync(Guid orderId, Guid tenantId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Sprint Sec F-005 — bulk transitions follow the same scoping as
    /// <see cref="GetOrdersFilteredAsync"/>: admins act on the whole tenant, every other
    /// caller only on the orders of their authorized distributors (own + delegations).
    /// </summary>
    Task<BulkTransitionResultDto> BulkValidateAsync(string userId, Guid tenantId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<BulkTransitionResultDto> BulkTransmitAsync(string userId, Guid tenantId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<BulkFinalizeResultDto> BulkFinalizeAsync(string userId, Guid tenantId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<OrderDashboardSummaryDto> GetDashboardSummaryAsync(string userId, Guid tenantId, bool isAdmin, bool isPreleveurOnly, CancellationToken cancellationToken = default);
}
