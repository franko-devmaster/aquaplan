namespace AquaPlan.Domain.Entities;

public class OrderAnalysisProgram
{
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public Guid AnalysisProgramId { get; set; }
    public AnalysisProgram? AnalysisProgram { get; set; }
}
