using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.SamplingPlans;

public record SamplingPlanCreateDto(
    [Required] Guid DistributorId,
    [Required][Range(2020, 2100)] int Year,
    [StringLength(2000)] string? Notes,
    [Required] List<SamplingPlanItemCreateDto> Items);

public record SamplingPlanItemCreateDto(
    [Required] Guid SamplingLocationId,
    [Required] Guid AnalysisProfileId,
    [Range(1, 365)] int FrequencyPerYear,
    [Required] List<int> PlannedMonths);
