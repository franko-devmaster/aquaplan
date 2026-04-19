using AquaPlan.Application.DTOs.LimsSync;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AquaPlan.Infrastructure.HostedServices;

/// <summary>
/// AQ-35 — Background worker that periodically pulls analysis results from the Mock LIMS
/// for orders in status <c>Transmitted</c> that have no <see cref="Domain.Entities.SamplingResult"/>.
/// One iteration = one cycle; each cycle writes <see cref="Domain.Entities.LimsSyncLog"/> rows.
/// </summary>
public class MockLimsResultsSyncWorker(
    IServiceScopeFactory scopeFactory,
    IOptions<LimsSyncOptions> options,
    ILogger<MockLimsResultsSyncWorker> logger) : BackgroundService
{
    private int _consecutiveErrorCycles;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var opts = options.Value;
        if (!opts.Enabled)
        {
            logger.LogInformation("MockLimsResultsSyncWorker is disabled (LimsSync:Enabled=false)");
            return;
        }

        var interval = TimeSpan.FromMinutes(Math.Max(1, opts.IntervalMinutes));
        logger.LogInformation(
            "MockLimsResultsSyncWorker started — interval={Interval}", interval);

        // Small initial delay so the app finishes booting before the first cycle.
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
                _consecutiveErrorCycles = 0;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _consecutiveErrorCycles++;
                logger.LogError(ex, "MockLimsResultsSyncWorker cycle failed (consecutive errors={Count})", _consecutiveErrorCycles);
                if (_consecutiveErrorCycles >= 3)
                {
                    logger.LogWarning(
                        "MockLimsResultsSyncWorker has failed {Count} consecutive cycles — investigate Mock LIMS connectivity",
                        _consecutiveErrorCycles);
                }
            }

            try
            {
                await Task.Delay(interval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }

        logger.LogInformation("MockLimsResultsSyncWorker stopped");
    }

    /// <summary>
    /// Runs a single sync cycle. Exposed as <c>internal</c> to let tests drive one iteration
    /// deterministically without having to start/stop the underlying <see cref="BackgroundService"/>.
    /// </summary>
    internal async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AquaPlanDbContext>();
        var resultService = scope.ServiceProvider.GetRequiredService<ILimsResultService>();
        var syncService = scope.ServiceProvider.GetRequiredService<ILimsSyncService>();

        var cycleId = Guid.NewGuid();

        // Target: orders that have been Transmitted (LIMS received them) but no SamplingResult yet.
        var pending = await dbContext.Orders
            .Where(o => o.Status == OrderStatus.Transmitted
                && o.LimsOrderId != null
                && !dbContext.SamplingResults.Any(r => r.OrderId == o.Id))
            .Select(o => new { o.Id, o.TenantId, o.OrderNumber })
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            logger.LogDebug("LimsSync cycle {CycleId}: no pending orders", cycleId);
            return;
        }

        logger.LogInformation("LimsSync cycle {CycleId}: {Count} pending orders", cycleId, pending.Count);

        foreach (var order in pending)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                break;
            }

            var startedAt = DateTime.UtcNow;
            try
            {
                var outcome = await resultService.PullAsync(order.Id, order.TenantId, cancellationToken);
                var completedAt = DateTime.UtcNow;

                if (outcome is null)
                {
                    continue; // order vanished between scan and pull — ignore
                }

                var status = outcome.NewResultsCount > 0
                    ? LimsSyncStatus.Success
                    : LimsSyncStatus.NoContent;
                var message = outcome.NewResultsCount > 0
                    ? $"{outcome.NewResultsCount} results persisted (order {order.OrderNumber}){(outcome.TransitionedToDone ? "; transitioned to Done" : string.Empty)}"
                    : $"No results available yet for order {order.OrderNumber}";

                await syncService.WriteLogAsync(
                    cycleId,
                    order.Id,
                    "PullResults",
                    status,
                    message,
                    startedAt,
                    completedAt,
                    order.TenantId,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                var completedAt = DateTime.UtcNow;
                logger.LogWarning(ex, "LimsSync cycle {CycleId} — pull failed for order {OrderId}", cycleId, order.Id);
                try
                {
                    await syncService.WriteLogAsync(
                        cycleId,
                        order.Id,
                        "PullResults",
                        LimsSyncStatus.Error,
                        ex.Message,
                        startedAt,
                        completedAt,
                        order.TenantId,
                        cancellationToken);
                }
                catch (Exception logEx)
                {
                    logger.LogError(logEx, "Failed to persist LimsSyncLog for order {OrderId}", order.Id);
                }
            }
        }
    }
}
