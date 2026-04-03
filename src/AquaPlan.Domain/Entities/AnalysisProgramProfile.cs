namespace AquaPlan.Domain.Entities;

public class AnalysisProgramProfile
{
    public Guid AnalysisProgramId { get; set; }
    public AnalysisProgram? AnalysisProgram { get; set; }
    public Guid AnalysisProfileId { get; set; }
    public AnalysisProfile? AnalysisProfile { get; set; }
}
