using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.SamplingPlans;

public record SamplingPlanRejectDto([Required][StringLength(1000)] string Reason);
