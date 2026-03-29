using AquaPlan.Domain.Enums;

namespace AquaPlan.Domain.Entities;

public class Order
{
    public Guid Id { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public OrderStatus Status { get; set; } = OrderStatus.Draft;
    public bool IsUnplanned { get; set; }
    public string CreatedById { get; set; } = string.Empty;
    public AppUser? CreatedBy { get; set; }
    public string? PreleveurId { get; set; }
    public AppUser? Preleveur { get; set; }
    public Guid DistributorId { get; set; }
    public Distributor? Distributor { get; set; }
    public Guid TenantId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? UpdatedBy { get; set; }

    public Sampling? Sampling { get; set; }
}
