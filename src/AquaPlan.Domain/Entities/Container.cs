namespace AquaPlan.Domain.Entities;

public class Container
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Material { get; set; } = string.Empty;
    public int VolumeMl { get; set; }
    public string Color { get; set; } = string.Empty;
    public bool IsActive { get; set; } = true;
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
