using Microsoft.AspNetCore.Identity;

namespace AquaPlan.Domain.Entities;

public class AppUser : IdentityUser
{
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public int UserNumber { get; set; }
    public string? Organization { get; set; }
    public string? ExternalId { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid? DistributorId { get; set; }
    public Distributor? Distributor { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiryTime { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
    public string? CreatedBy { get; set; }
    public string? UpdatedBy { get; set; }

    public ICollection<UserDistributor> UserDistributors { get; set; } = new List<UserDistributor>();
}
