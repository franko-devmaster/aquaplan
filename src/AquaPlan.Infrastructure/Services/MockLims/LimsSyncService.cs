using AquaPlan.Application.DTOs.LimsSync;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AquaPlan.Infrastructure.Services.MockLims;

/// <summary>
/// AQ-35 — Read/write access to the LIMS sync operational journal.
/// </summary>
internal class LimsSyncService(
    AquaPlanDbContext dbContext,
    IOptions<LimsSyncOptions> options) : ILimsSyncService
{
    public async Task<LimsSyncStatusDto> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var opts = options.Value;

        var lastCycleAt = await dbContext.LimsSyncLogs
            .Where(l => l.TenantId == tenantId)
            .OrderByDescending(l => l.CompletedAt)
            .Select(l => (DateTime?)l.CompletedAt)
            .FirstOrDefaultAsync(cancellationToken);

        var pendingOrdersCount = await dbContext.Orders
            .Where(o => o.TenantId == tenantId
                && o.Status == OrderStatus.Transmitted
                && !dbContext.SamplingResults.Any(r => r.OrderId == o.Id))
            .CountAsync(cancellationToken);

        var recentLogsCount = await dbContext.LimsSyncLogs
            .Where(l => l.TenantId == tenantId)
            .CountAsync(cancellationToken);

        DateTime? nextCycleAt = null;
        if (opts.Enabled && lastCycleAt.HasValue)
        {
            nextCycleAt = lastCycleAt.Value.AddMinutes(opts.IntervalMinutes);
        }

        return new LimsSyncStatusDto(
            opts.Enabled,
            opts.IntervalMinutes,
            lastCycleAt,
            nextCycleAt,
            pendingOrdersCount,
            recentLogsCount);
    }

    public async Task<IReadOnlyList<LimsSyncLogDto>> GetLogsAsync(
        Guid tenantId,
        int take = 50,
        LimsSyncStatus? status = null,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0)
        {
            take = 50;
        }
        if (take > 500)
        {
            take = 500;
        }

        var query = dbContext.LimsSyncLogs
            .Where(l => l.TenantId == tenantId);

        if (status.HasValue)
        {
            query = query.Where(l => l.Status == status.Value);
        }

        return await query
            .OrderByDescending(l => l.CompletedAt)
            .Take(take)
            .Select(l => new LimsSyncLogDto(
                l.Id,
                l.CycleId,
                l.OrderId,
                l.Operation,
                l.Status,
                l.Message,
                l.StartedAt,
                l.CompletedAt,
                l.DurationMs))
            .ToListAsync(cancellationToken);
    }

    public async Task WriteLogAsync(
        Guid cycleId,
        Guid? orderId,
        string operation,
        LimsSyncStatus status,
        string? message,
        DateTime startedAt,
        DateTime completedAt,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var entry = new LimsSyncLog
        {
            Id = Guid.NewGuid(),
            CycleId = cycleId,
            OrderId = orderId,
            Operation = operation,
            Status = status,
            Message = message,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            DurationMs = (long)(completedAt - startedAt).TotalMilliseconds,
            TenantId = tenantId,
        };
        dbContext.LimsSyncLogs.Add(entry);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
