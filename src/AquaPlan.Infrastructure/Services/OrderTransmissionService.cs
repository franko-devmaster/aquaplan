using AquaPlan.Application.DTOs.MockLims;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AquaPlan.Infrastructure.Services;

/// <summary>
/// Sprint Robustesse F-105 / F-108 — the one and only implementation of the
/// <c>Completed → Transmitted</c> transition. OrderService (bulk), OrderStatusService
/// (single) and SamplingRoundService (whole round) all delegate here so the side effects
/// are guaranteed identical: status + timestamps, Mock LIMS forward (LimsOrderId), audit.
/// </summary>
internal class OrderTransmissionService(
    AquaPlanDbContext dbContext,
    IOrderAuditService auditService,
    IMockLimsService mockLimsService,
    IOptions<MockLimsOptions> mockLimsOptions,
    ILogger<OrderTransmissionService> logger) : IOrderTransmissionService
{
    public async Task<int> TransmitCompletedOrdersAsync(
        IReadOnlyCollection<Order> orders, string userId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var toTransmit = orders.Where(o => o.Status == OrderStatus.Completed).ToList();
        if (toTransmit.Count == 0)
        {
            return 0;
        }

        var now = DateTime.UtcNow;
        foreach (var order in toTransmit)
        {
            TransmitOrderInternal(order, userId, now);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // AQ-33 — forward to the Mock LIMS so results can later be pulled. Best-effort:
        // failures are logged and the order keeps Transmitted status without a LimsOrderId.
        if (mockLimsOptions.Value.Enabled)
        {
            await TransmitOrdersToMockLimsAsync(toTransmit, tenantId, cancellationToken);
        }

        foreach (var order in toTransmit)
        {
            await auditService.LogAsync(
                order.Id,
                "StatusTransitioned",
                $"Status changed from {OrderStatus.Completed} to {OrderStatus.Transmitted}",
                OrderStatus.Completed.ToString(),
                OrderStatus.Transmitted.ToString(),
                userId,
                tenantId,
                cancellationToken);
        }

        logger.LogInformation(
            "Transmitted {Count} orders (Completed -> Transmitted) by {UserId} in tenant {TenantId}",
            toTransmit.Count, userId, tenantId);

        return toTransmit.Count;
    }

    /// <summary>
    /// The single mutation applied to every transmitted order, regardless of the calling path.
    /// </summary>
    private static void TransmitOrderInternal(Order order, string userId, DateTime now)
    {
        order.Status = OrderStatus.Transmitted;
        order.StatusChangedAt = now;
        order.StatusChangedBy = userId;
        order.TransmittedAt = now;
        order.UpdatedAt = now;
        order.UpdatedBy = userId;
    }

    /// <summary>
    /// AQ-33 — Forward transmitted orders to the Mock LIMS and persist the returned LimsOrderId.
    /// Failures are logged and swallowed per order (best-effort, do not block the transition).
    /// </summary>
    private async Task TransmitOrdersToMockLimsAsync(
        IList<Order> orders, Guid tenantId, CancellationToken cancellationToken)
    {
        var orderIds = orders.Select(o => o.Id).ToList();
        var programsByOrder = await dbContext.OrderAnalysisPrograms
            .Where(oap => orderIds.Contains(oap.OrderId))
            .Include(oap => oap.AnalysisProgram!)
                .ThenInclude(p => p.AnalysisProgramProfiles)
                    .ThenInclude(app => app.AnalysisProfile)
            .ToListAsync(cancellationToken);

        var persistNeeded = false;
        foreach (var order in orders)
        {
            if (order.LimsOrderId.HasValue)
            {
                continue; // idempotence — already transmitted previously
            }

            var parameters = programsByOrder
                .Where(oap => oap.OrderId == order.Id && oap.AnalysisProgram is not null)
                .SelectMany(oap => oap.AnalysisProgram!.AnalysisProgramProfiles)
                .Where(app => app.AnalysisProfile is not null)
                .Select(app => app.AnalysisProfile!.Code)
                .Distinct()
                .ToList();

            if (parameters.Count == 0)
            {
                parameters = MockLimsParameterCatalog.All.Select(p => p.Code).ToList();
            }
            else
            {
                var recognized = parameters.Where(code => MockLimsParameterCatalog.FindByCode(code) is not null).ToList();
                parameters = recognized.Count == 0
                    ? MockLimsParameterCatalog.All.Select(p => p.Code).ToList()
                    : recognized;
            }

            var dto = new MockLimsOrderCreateDto(
                OrderReference: order.OrderNumber,
                SamplingDate: order.Sampling?.SamplingDateTime ?? order.PlannedDate ?? DateTime.UtcNow,
                Parameters: parameters,
                SourceOrderId: order.Id);

            try
            {
                var response = await mockLimsService.ReceiveOrderAsync(dto, tenantId, cancellationToken);
                order.LimsOrderId = response.LimsOrderId;
                persistNeeded = true;
                logger.LogInformation(
                    "Order {OrderNumber} forwarded to Mock LIMS -> LimsOrderId={LimsOrderId}",
                    order.OrderNumber, response.LimsOrderId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to forward order {OrderNumber} to Mock LIMS; will remain without LimsOrderId",
                    order.OrderNumber);
            }
        }

        if (persistNeeded)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }
}
