using AquaPlan.Domain.Entities;

namespace AquaPlan.Application.Services.Interfaces;

/// <summary>
/// Sprint Robustesse F-105 / F-108 — single source of truth for the
/// <c>Completed → Transmitted</c> transition. Every path that transmits an order
/// (single transition, bulk transmit, whole-round transmit) MUST go through this
/// service so the side effects stay identical: set Transmitted status + timestamps,
/// forward to the Mock LIMS (persisting <see cref="Order.LimsOrderId"/>), and write an
/// audit entry. Previously the round path diverged, producing Transmitted orders with no
/// LimsOrderId that the worker never pulled (root cause of the AQ-404 backfill).
/// </summary>
public interface IOrderTransmissionService
{
    /// <summary>
    /// Applies the <c>Completed → Transmitted</c> transition to the supplied orders that are
    /// currently <c>Completed</c>, persists the change, forwards them to the Mock LIMS when the
    /// feature flag is enabled, and writes one audit entry per order.
    /// </summary>
    /// <param name="orders">Already-loaded, tracked order entities to transmit.</param>
    /// <param name="userId">User performing the transmission (status/audit attribution).</param>
    /// <param name="tenantId">Tenant of the orders.</param>
    /// <returns>The number of orders actually transitioned.</returns>
    Task<int> TransmitCompletedOrdersAsync(
        IReadOnlyCollection<Order> orders, string userId, Guid tenantId,
        CancellationToken cancellationToken = default);
}
