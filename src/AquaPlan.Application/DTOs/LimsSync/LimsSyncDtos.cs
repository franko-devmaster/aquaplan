using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.LimsSync;

public record LimsSyncStatusDto(
    bool IsEnabled,
    int IntervalMinutes,
    DateTime? LastCycleAt,
    DateTime? NextCycleAt,
    int PendingOrdersCount,
    int RecentLogsCount);

public record LimsSyncLogDto(
    Guid Id,
    Guid CycleId,
    Guid? OrderId,
    string Operation,
    LimsSyncStatus Status,
    string? Message,
    DateTime StartedAt,
    DateTime CompletedAt,
    long DurationMs);

public class LimsSyncOptions
{
    public const string SectionName = "LimsSync";

    public bool Enabled { get; set; } = true;
    public int IntervalMinutes { get; set; } = 5;
}
