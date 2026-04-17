namespace AquaPlan.Domain.Entities;

public class SamplingContainer
{
    public Guid Id { get; set; }
    public Guid SamplingId { get; set; }
    public Sampling? Sampling { get; set; }
    public Guid ContainerId { get; set; }
    public Container? Container { get; set; }
    public string? Barcode { get; set; }
    public DateTime? BarcodeScannedAt { get; set; }
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
