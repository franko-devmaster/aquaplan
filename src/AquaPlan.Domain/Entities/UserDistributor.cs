namespace AquaPlan.Domain.Entities;

public class UserDistributor
{
    public string UserId { get; set; } = string.Empty;
    public AppUser? User { get; set; }
    public Guid DistributorId { get; set; }
    public Distributor? Distributor { get; set; }
}
