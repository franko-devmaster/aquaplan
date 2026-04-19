using AquaPlan.Application.DTOs.LimsSync;
using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.Services.Interfaces;

/// <summary>
/// AQ-35 — Query surface for the LIMS sync worker:
/// status dashboard + log history. Also exposes the log writer used by the worker.
/// </summary>
public interface ILimsSyncService
{
    Task<LimsSyncStatusDto> GetStatusAsync(Guid tenantId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LimsSyncLogDto>> GetLogsAsync(
        Guid tenantId,
        int take = 50,
        LimsSyncStatus? status = null,
        CancellationToken cancellationToken = default);

    Task WriteLogAsync(
        Guid cycleId,
        Guid? orderId,
        string operation,
        LimsSyncStatus status,
        string? message,
        DateTime startedAt,
        DateTime completedAt,
        Guid tenantId,
        CancellationToken cancellationToken = default);
}
