using AquaPlan.Application.DTOs.SamplingResults;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services.MockLims;

/// <summary>
/// AQ-34 — Manual + worker-driven inbound pull from the Mock LIMS.
/// </summary>
internal class LimsResultService(
    AquaPlanDbContext dbContext,
    IMockLimsService mockLimsService,
    IOrderAuditService auditService,
    ILogger<LimsResultService> logger) : ILimsResultService
{
    // Synthetic performer id for system-triggered actions. Left unused when writing audit logs
    // because `order_audit_logs.performed_by_id` is a FK to `users.id`; the LIMS sync activity
    // is instead traced through `lims_sync_logs` (AQ-35).
    private const string SystemActor = "system:lims-sync";

    public async Task<PullResultsOutcome?> PullAsync(
        Guid orderId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        // Idempotence: already-pulled results are returned as-is.
        var existing = await dbContext.SamplingResults
            .Where(r => r.OrderId == orderId && r.TenantId == tenantId)
            .OrderBy(r => r.ParameterCode)
            .ToListAsync(cancellationToken);

        if (existing.Count > 0)
        {
            return new PullResultsOutcome(MapList(existing), 0, TransitionedToDone: false);
        }

        // Order must carry a LimsOrderId (set when AQ-33 forwarded it to the Mock LIMS).
        if (!order.LimsOrderId.HasValue)
        {
            logger.LogDebug("Order {OrderId} has no LimsOrderId — nothing to pull", orderId);
            return new PullResultsOutcome(MapList([]), 0, TransitionedToDone: false);
        }

        var limsPayload = await mockLimsService.GetResultsAsync(order.LimsOrderId.Value, tenantId, cancellationToken);
        if (limsPayload is null || limsPayload.Results.Count == 0)
        {
            return new PullResultsOutcome(MapList([]), 0, TransitionedToDone: false);
        }

        var now = DateTime.UtcNow;
        var newEntities = limsPayload.Results
            .Select(r => new SamplingResult
            {
                Id = Guid.NewGuid(),
                OrderId = orderId,
                ParameterCode = r.ParameterCode,
                Value = r.Value,
                Unit = r.Unit,
                ReferenceMin = r.ReferenceMin,
                ReferenceMax = r.ReferenceMax,
                IsConform = ComputeConformity(r.Value, r.ReferenceMin, r.ReferenceMax),
                ReceivedAt = now,
                TenantId = tenantId,
            })
            .ToList();

        dbContext.SamplingResults.AddRange(newEntities);

        var transitionedToDone = false;
        if (order.Status == OrderStatus.Transmitted)
        {
            order.Status = OrderStatus.Done;
            order.StatusChangedAt = now;
            order.StatusChangedBy = SystemActor;
            order.UpdatedAt = now;
            order.UpdatedBy = SystemActor;
            order.ResultsReceivedAt = now;
            transitionedToDone = true;
        }
        else
        {
            order.ResultsReceivedAt = now;
            order.UpdatedAt = now;
            order.UpdatedBy = SystemActor;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Note: we deliberately skip OrderAuditService here because its PerformedById is a FK
        // to `users`. The LIMS sync journal (AQ-35, `lims_sync_logs`) already records each
        // pull attempt with cycleId/orderId/status — that's the authoritative audit trail.
        _ = auditService;

        logger.LogInformation(
            "Pulled {Count} results for order {OrderId} (tenant {TenantId}); transitionedToDone={Transitioned}",
            newEntities.Count, orderId, tenantId, transitionedToDone);

        return new PullResultsOutcome(MapList(newEntities), newEntities.Count, transitionedToDone);
    }

    public async Task<SamplingResultListDto?> GetByOrderAsync(
        Guid orderId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var orderExists = await dbContext.Orders
            .AnyAsync(o => o.Id == orderId && o.TenantId == tenantId, cancellationToken);
        if (!orderExists)
        {
            return null;
        }

        var entities = await dbContext.SamplingResults
            .Where(r => r.OrderId == orderId && r.TenantId == tenantId)
            .OrderBy(r => r.ParameterCode)
            .ToListAsync(cancellationToken);

        return MapList(entities);
    }

    private static bool ComputeConformity(decimal value, decimal? min, decimal? max)
    {
        if (min.HasValue && value < min.Value)
        {
            return false;
        }
        if (max.HasValue && value > max.Value)
        {
            return false;
        }
        return true;
    }

    private static SamplingResultListDto MapList(IReadOnlyList<SamplingResult> entities)
    {
        var items = entities
            .OrderBy(r => r.ParameterCode, StringComparer.OrdinalIgnoreCase)
            .Select(r => new SamplingResultDto(
                r.Id,
                r.ParameterCode,
                r.Value,
                r.Unit,
                r.ReferenceMin,
                r.ReferenceMax,
                r.IsConform,
                r.ReceivedAt))
            .ToList();

        var conform = items.Count(i => i.IsConform);
        return new SamplingResultListDto(items, conform, items.Count - conform, items.Count);
    }
}
