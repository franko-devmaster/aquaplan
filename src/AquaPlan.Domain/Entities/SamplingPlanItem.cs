namespace AquaPlan.Domain.Entities;

public class SamplingPlanItem
{
    public Guid Id { get; set; }
    public Guid SamplingPlanId { get; set; }
    public SamplingPlan? SamplingPlan { get; set; }
    public Guid SamplingLocationId { get; set; }
    public SamplingLocation? SamplingLocation { get; set; }
    public Guid AnalysisProfileId { get; set; }
    public AnalysisProfile? AnalysisProfile { get; set; }
    public int FrequencyPerYear { get; set; }
    public List<int> PlannedMonths { get; set; } = new();
}
