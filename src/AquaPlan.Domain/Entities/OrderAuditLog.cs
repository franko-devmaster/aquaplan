namespace AquaPlan.Domain.Entities;

public class OrderAuditLog
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public string Action { get; set; } = string.Empty;
    public string? Details { get; set; }
    public string? OldValue { get; set; }
    public string? NewValue { get; set; }
    public string PerformedById { get; set; } = string.Empty;
    public AppUser? PerformedBy { get; set; }
    public DateTime PerformedAt { get; set; } = DateTime.UtcNow;
    public Guid TenantId { get; set; }
}
