using AquaPlan.Domain.Enums;

namespace AquaPlan.Domain.Entities;

public class SamplingRound
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public DateTime? Deadline { get; set; }
    public SamplingRoundStatus Status { get; set; } = SamplingRoundStatus.Draft;
    public string? PreleveurId { get; set; }
    public AppUser? Preleveur { get; set; }
    public Guid DistributorId { get; set; }
    public Distributor? Distributor { get; set; }
    public string? Notes { get; set; }
    public Guid TenantId { get; set; }
    public string CreatedById { get; set; } = string.Empty;
    public AppUser? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ICollection<Order> Orders { get; set; } = new List<Order>();
}
