namespace AquaPlan.Domain.Entities;

public class AnalysisProgram
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public Guid TenantId { get; set; }
    public Tenant? Tenant { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AnalysisProgramProfile> AnalysisProgramProfiles { get; set; } = new List<AnalysisProgramProfile>();
    public ICollection<OrderAnalysisProgram> OrderAnalysisPrograms { get; set; } = new List<OrderAnalysisProgram>();
}
