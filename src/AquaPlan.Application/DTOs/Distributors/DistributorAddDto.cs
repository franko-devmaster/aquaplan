using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.Distributors;

public record DistributorAddDto(
    [Required] string Name,
    string? CantonRegion,
    string? DistributionNetwork);
