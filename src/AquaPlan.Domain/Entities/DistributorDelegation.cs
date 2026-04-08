namespace AquaPlan.Domain.Entities;

public class DistributorDelegation
{
    public Guid Id { get; set; }
    public Guid DelegatingDistributorId { get; set; }
    public Distributor? DelegatingDistributor { get; set; }
    public Guid DelegatedToDistributorId { get; set; }
    public Distributor? DelegatedToDistributor { get; set; }
    public DateTime ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
