using AquaPlan.Domain.Enums;

namespace AquaPlan.Domain.Entities;

public class SamplingPlan
{
    public Guid Id { get; set; }
    public int Year { get; set; }
    public SamplingPlanStatus Status { get; set; } = SamplingPlanStatus.Draft;
    public Guid DistributorId { get; set; }
    public Distributor? Distributor { get; set; }
    public string CreatedById { get; set; } = string.Empty;
    public AppUser? CreatedBy { get; set; }
    public string? Notes { get; set; }
    public string? RejectionReason { get; set; }
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? StatusChangedAt { get; set; }
    public string? StatusChangedBy { get; set; }

    public ICollection<SamplingPlanItem> Items { get; set; } = new List<SamplingPlanItem>();
}
