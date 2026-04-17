namespace AquaPlan.Domain.Entities;

public class SamplingContainer
{
    public Guid Id { get; set; }
    public Guid SamplingId { get; set; }
    public Sampling? Sampling { get; set; }
    public Guid ContainerId { get; set; }
    public Container? Container { get; set; }
    public DateTime? BarcodeScannedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
