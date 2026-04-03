using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.AnalysisPrograms;

public record AnalysisProgramAddProfilesDto(
    [Required] IList<Guid> ProfileIds);
