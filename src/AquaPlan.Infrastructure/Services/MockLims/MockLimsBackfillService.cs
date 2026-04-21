using AquaPlan.Application.DTOs.MockLims;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services.MockLims;

/// <summary>
/// AQ-404 — Retroactive backfill of <c>Transmitted</c> orders that are missing a <c>LimsOrderId</c>.
/// For each eligible order: (1) forward to the Mock LIMS, (2) pull results. Failures are isolated
/// per order so a single broken mandate does not halt the whole batch.
/// </summary>
internal class MockLimsBackfillService(
    AquaPlanDbContext dbContext,
    IMockLimsService mockLimsService,
    ILimsResultService limsResultService,
    ILogger<MockLimsBackfillService> logger) : IMockLimsBackfillService
{
    public async Task<MockLimsBackfillResultDto> BackfillAsync(
        Guid tenantId,
        int maxOrders,
        CancellationToken cancellationToken = default)
    {
        if (maxOrders <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxOrders), "maxOrders must be strictly positive.");
        }

        var totalEligible = await dbContext.Orders
            .CountAsync(
                o => o.TenantId == tenantId
                    && o.Status == OrderStatus.Transmitted
                    && o.LimsOrderId == null,
                cancellationToken);

        var eligible = await dbContext.Orders
            .Where(
                o => o.TenantId == tenantId
                    && o.Status == OrderStatus.Transmitted
                    && o.LimsOrderId == null)
            .OrderBy(o => o.CreatedAt)
            .Take(maxOrders)
            .Include(o => o.Sampling)
            .Include(o => o.OrderAnalysisPrograms)
                .ThenInclude(oap => oap.AnalysisProgram!)
                    .ThenInclude(p => p.AnalysisProgramProfiles)
                        .ThenInclude(app => app.AnalysisProfile)
            .ToListAsync(cancellationToken);

        logger.LogInformation(
            "MockLimsBackfill: tenant {TenantId} has {TotalEligible} eligible orders, processing {Processed}",
            tenantId, totalEligible, eligible.Count);

        var successCount = 0;
        var failures = new List<MockLimsBackfillFailureDto>();

        foreach (var order in eligible)
        {
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                // Safety re-check: another request may have backfilled this order in the meantime.
                if (order.LimsOrderId.HasValue)
                {
                    successCount++;
                    continue;
                }

                var parameters = BuildParameterList(order);
                var createDto = new MockLimsOrderCreateDto(
                    OrderReference: order.OrderNumber,
                    SamplingDate: order.Sampling?.SamplingDateTime ?? order.PlannedDate ?? DateTime.UtcNow,
                    Parameters: parameters,
                    SourceOrderId: order.Id);

                // 1) Outbound — register the order at the Mock LIMS and persist the returned id.
                var received = await mockLimsService.ReceiveOrderAsync(createDto, tenantId, cancellationToken);
                order.LimsOrderId = received.LimsOrderId;
                order.TransmittedAt ??= DateTime.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);

                // 2) Inbound — pull results (persists SamplingResults + transitions to Done).
                var pullOutcome = await limsResultService.PullAsync(order.Id, tenantId, cancellationToken);
                if (pullOutcome is null)
                {
                    failures.Add(new MockLimsBackfillFailureDto(
                        order.Id,
                        order.OrderNumber,
                        "Order disappeared between outbound and inbound pull."));
                    continue;
                }

                logger.LogInformation(
                    "MockLimsBackfill: order {OrderNumber} ({OrderId}) reconciled — LimsOrderId={LimsOrderId}, newResults={NewResults}, toDone={ToDone}",
                    order.OrderNumber, order.Id, order.LimsOrderId, pullOutcome.NewResultsCount, pullOutcome.TransitionedToDone);

                successCount++;
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "MockLimsBackfill: failed to reconcile order {OrderNumber} ({OrderId})",
                    order.OrderNumber, order.Id);
                failures.Add(new MockLimsBackfillFailureDto(
                    order.Id,
                    order.OrderNumber,
                    ex.Message));
            }
        }

        return new MockLimsBackfillResultDto(
            TotalEligible: totalEligible,
            Processed: eligible.Count,
            SuccessCount: successCount,
            FailureCount: failures.Count,
            Failures: failures);
    }

    /// <summary>
    /// Builds the parameter list to forward to the Mock LIMS. Mirrors the logic used by
    /// <c>OrderService.TransmitOrdersToMockLimsAsync</c> (AQ-33) so backfilled orders behave
    /// identically to orders transmitted through the normal flow.
    /// </summary>
    private static List<string> BuildParameterList(Order order)
    {
        var parameters = order.OrderAnalysisPrograms
            .Where(oap => oap.AnalysisProgram is not null)
            .SelectMany(oap => oap.AnalysisProgram!.AnalysisProgramProfiles)
            .Where(app => app.AnalysisProfile is not null)
            .Select(app => app.AnalysisProfile!.Code)
            .Distinct()
            .ToList();

        if (parameters.Count == 0)
        {
            return MockLimsParameterCatalog.All.Select(p => p.Code).ToList();
        }

        var recognized = parameters
            .Where(code => MockLimsParameterCatalog.FindByCode(code) is not null)
            .ToList();
        return recognized.Count == 0
            ? MockLimsParameterCatalog.All.Select(p => p.Code).ToList()
            : recognized;
    }
}
