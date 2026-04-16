namespace AquaPlan.Domain.Entities;

public class SamplingLocation
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string LocationCode { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string? Address { get; set; }
    public string? AccessDescription { get; set; }
    public bool IsActive { get; set; } = true;
    public bool IsValidated { get; set; } = true;
    public Guid DistributorId { get; set; }
    public Distributor? Distributor { get; set; }
    public Guid SectorId { get; set; }
    public Sector? Sector { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
