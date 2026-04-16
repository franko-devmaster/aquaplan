using AquaPlan.Domain.Enums;

namespace AquaPlan.Domain.Entities;

public class AnalysisProfile
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public AnalysisCategory Category { get; set; } = AnalysisCategory.Other;
    public bool IsActive { get; set; } = true;
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public Guid ContainerId { get; set; }
    public Container? Container { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AnalysisProgramProfile> AnalysisProgramProfiles { get; set; } = new List<AnalysisProgramProfile>();
}
