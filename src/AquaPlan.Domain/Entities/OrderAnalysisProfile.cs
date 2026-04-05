namespace AquaPlan.Domain.Entities;

public class OrderAnalysisProfile
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public Guid AnalysisProfileId { get; set; }
    public AnalysisProfile? AnalysisProfile { get; set; }
}
