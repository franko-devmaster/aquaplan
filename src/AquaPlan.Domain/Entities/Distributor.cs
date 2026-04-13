namespace AquaPlan.Domain.Entities;

public class Distributor
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? CantonRegion { get; set; }
    public string? DistributionNetwork { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<SamplingLocation> SamplingLocations { get; set; } = new List<SamplingLocation>();
    public ICollection<UserDistributor> UserDistributors { get; set; } = new List<UserDistributor>();
}
