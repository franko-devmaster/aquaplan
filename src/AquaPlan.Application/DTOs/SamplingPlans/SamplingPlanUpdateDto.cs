using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.SamplingPlans;

public record SamplingPlanUpdateDto(
    [StringLength(2000)] string? Notes,
    [Required] List<SamplingPlanItemCreateDto> Items);
