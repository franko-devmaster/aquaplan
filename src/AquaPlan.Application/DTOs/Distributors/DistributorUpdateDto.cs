using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.Distributors;

public record DistributorUpdateDto(
    [Required] string Name,
    string? CantonRegion,
    string? DistributionNetwork);
