namespace AquaPlan.Domain.Entities;

/// <summary>
/// AQ-34 — A single analysis result received from the (Mock) LIMS for a given mandate.
/// One row per parameter × order. Immutable once received (no re-measurement support v0.92).
/// </summary>
public class SamplingResult
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public string ParameterCode { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal? ReferenceMin { get; set; }
    public decimal? ReferenceMax { get; set; }
    public bool IsConform { get; set; }
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public Guid TenantId { get; set; }
}
