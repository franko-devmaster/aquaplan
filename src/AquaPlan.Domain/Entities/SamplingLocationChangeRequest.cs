using AquaPlan.Domain.Enums;

namespace AquaPlan.Domain.Entities;

public class SamplingLocationChangeRequest
{
    public Guid Id { get; set; }
    public ChangeRequestType RequestType { get; set; }
    public ChangeRequestStatus Status { get; set; } = ChangeRequestStatus.Pending;

    public Guid? SamplingLocationId { get; set; }
    public SamplingLocation? SamplingLocation { get; set; }

    public Guid DistributorId { get; set; }
    public Distributor? Distributor { get; set; }

    public string? ProposedName { get; set; }
    public string? ProposedLocationCode { get; set; }
    public string? ProposedDescription { get; set; }

    public string RequestedById { get; set; } = string.Empty;
    public AppUser? RequestedBy { get; set; }
    public DateTime RequestedAt { get; set; } = DateTime.UtcNow;

    public string? ReviewedById { get; set; }
    public AppUser? ReviewedBy { get; set; }
    public DateTime? ReviewedAt { get; set; }
    public string? ReviewComment { get; set; }

    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
}
