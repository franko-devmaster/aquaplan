using AquaPlan.Domain.Enums;

namespace AquaPlan.Domain.Entities;

/// <summary>
/// AQ-35 — One row per LIMS sync attempt (manual or worker-driven).
/// Acts as the operational audit journal for inbound result pulls.
/// </summary>
public class LimsSyncLog
{
    public Guid Id { get; set; }
    public Guid CycleId { get; set; }
    public Guid? OrderId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public LimsSyncStatus Status { get; set; }
    public string? Message { get; set; }
    public DateTime StartedAt { get; set; }
    public DateTime CompletedAt { get; set; }
    public long DurationMs { get; set; }
    public Guid TenantId { get; set; }
}
