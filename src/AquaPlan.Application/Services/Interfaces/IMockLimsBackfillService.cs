using AquaPlan.Application.DTOs.MockLims;

namespace AquaPlan.Application.Services.Interfaces;

/// <summary>
/// AQ-404 — Retroactively reconciles <c>Transmitted</c> orders without a <c>LimsOrderId</c>
/// with the Mock LIMS: outbound forward then inbound pull, per order, in isolation.
/// Used when production/demo data was restored from a snapshot taken before the Mock LIMS
/// integration was in place.
/// </summary>
public interface IMockLimsBackfillService
{
    /// <summary>
    /// Runs the backfill for the given tenant (defaults to the caller's tenant) and the given
    /// maximum order count (defaults to <paramref name="defaultMaxOrders"/>).
    /// Each order is processed in isolation — a failure does not stop the loop.
    /// </summary>
    Task<MockLimsBackfillResultDto> BackfillAsync(
        Guid tenantId,
        int maxOrders,
        CancellationToken cancellationToken = default);
}
