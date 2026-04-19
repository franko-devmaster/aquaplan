using AquaPlan.Domain.Enums;

namespace AquaPlan.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.New;
    public bool IsUnplanned { get; set; }
    public UnplannedReason? UnplannedReason { get; set; }
    public string? UnplannedReasonDetails { get; set; }
    public bool IsDelegated { get; set; }
    public string CreatedById { get; set; } = string.Empty;
    public AppUser? CreatedBy { get; set; }
    public string? PreleveurId { get; set; }
    public AppUser? Preleveur { get; set; }
    public Guid DistributorId { get; set; }
    public Distributor? Distributor { get; set; }
    public Guid? SamplingRoundId { get; set; }
    public SamplingRound? SamplingRound { get; set; }
    public int SortOrder { get; set; }
    public Guid? SamplingLocationId { get; set; }
    public SamplingLocation? SamplingLocation { get; set; }
    public Guid? OriginalSamplingLocationId { get; set; }
    public SamplingLocation? OriginalSamplingLocation { get; set; }
    public string? LocationReplacementReason { get; set; }
    public string? SamplerComment { get; set; }
    public DateTime? PlannedDate { get; set; }
    public string? Notes { get; set; }
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }
    public DateTime? StatusChangedAt { get; set; }
    public string? StatusChangedBy { get; set; }

    // AQ-33 — Mock LIMS outbound transmission
    public Guid? LimsOrderId { get; set; }
    public DateTime? TransmittedAt { get; set; }

    // AQ-34 — Mock LIMS inbound (timestamp of the last pull that persisted results).
    public DateTime? ResultsReceivedAt { get; set; }

    public Sampling? Sampling { get; set; }
    public ICollection<OrderAnalysisProgram> OrderAnalysisPrograms { get; set; } = new List<OrderAnalysisProgram>();
    public ICollection<SamplingResult> SamplingResults { get; set; } = new List<SamplingResult>();
}
