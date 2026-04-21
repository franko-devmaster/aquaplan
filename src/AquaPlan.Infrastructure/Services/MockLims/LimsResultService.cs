using AquaPlan.Application.DTOs.SamplingResults;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using Microsoft.AspNetCore.Identity;
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
    INotificationService notificationService,
    UserManager<AppUser> userManager,
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

        // AQ-44 / AQ-45 — notify the requester + administrators of the tenant.
        // We only emit notifications on the first successful pull (newEntities > 0)
        // to avoid spamming admins when the worker re-enters an already pulled order.
        if (newEntities.Count > 0)
        {
            await CreateResultNotificationsAsync(order, newEntities, tenantId, cancellationToken);
        }

        logger.LogInformation(
            "Pulled {Count} results for order {OrderId} (tenant {TenantId}); transitionedToDone={Transitioned}",
            newEntities.Count, orderId, tenantId, transitionedToDone);

        return new PullResultsOutcome(MapList(newEntities), newEntities.Count, transitionedToDone);
    }

    private async Task CreateResultNotificationsAsync(
        Order order,
        IReadOnlyList<SamplingResult> results,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        try
        {
            var conformCount = results.Count(r => r.IsConform);
            var nonConformCount = results.Count - conformCount;

            var recipients = await GetResultRecipientsAsync(order, tenantId, cancellationToken);
            if (recipients.Count == 0)
            {
                return;
            }

            // AQ-44 — base "ResultsReceived" notification for everyone.
            var title = "Résultats reçus";
            var locationLabel = order.OrderNumber;
            var message = nonConformCount == 0
                ? $"Mandat {locationLabel} — {conformCount} paramètre{(conformCount > 1 ? "s" : string.Empty)} conforme{(conformCount > 1 ? "s" : string.Empty)}."
                : $"Mandat {locationLabel} — {nonConformCount} paramètre{(nonConformCount > 1 ? "s" : string.Empty)} non conforme{(nonConformCount > 1 ? "s" : string.Empty)} sur {results.Count}.";

            foreach (var userId in recipients)
            {
                await notificationService.CreateAsync(
                    userId,
                    NotificationType.ResultsReceived,
                    title,
                    message,
                    tenantId,
                    relatedEntityType: "Order",
                    relatedEntityId: order.Id,
                    isUrgent: false,
                    cancellationToken);
            }

            // AQ-45 — urgent "NonConformResult" notification if at least one parameter is non conform.
            if (nonConformCount > 0)
            {
                var urgentTitle = "⚠ Résultats non conformes";
                var urgentMessage = $"Mandat {locationLabel} — {nonConformCount} paramètre{(nonConformCount > 1 ? "s" : string.Empty)} non conforme{(nonConformCount > 1 ? "s" : string.Empty)} détecté{(nonConformCount > 1 ? "s" : string.Empty)}.";
                foreach (var userId in recipients)
                {
                    await notificationService.CreateAsync(
                        userId,
                        NotificationType.NonConformResult,
                        urgentTitle,
                        urgentMessage,
                        tenantId,
                        relatedEntityType: "Order",
                        relatedEntityId: order.Id,
                        isUrgent: true,
                        cancellationToken);
                }
            }
        }
        catch (Exception ex)
        {
            // Notifications are best-effort — never break the LIMS pull if they fail.
            logger.LogWarning(ex, "Failed to emit result notifications for order {OrderId}", order.Id);
        }
    }

    private async Task<List<string>> GetResultRecipientsAsync(
        Order order, Guid tenantId, CancellationToken cancellationToken)
    {
        var recipients = new HashSet<string>(StringComparer.Ordinal);

        // Requester (creator of the mandate) — always part of the audience.
        if (!string.IsNullOrWhiteSpace(order.CreatedById))
        {
            recipients.Add(order.CreatedById);
        }

        // Every administrator of the tenant.
        var admins = await userManager.GetUsersInRoleAsync(RoleName.Administrator);
        foreach (var admin in admins.Where(u => u.TenantId == tenantId && !string.IsNullOrEmpty(u.Id)))
        {
            recipients.Add(admin.Id);
        }

        return recipients.ToList();
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
