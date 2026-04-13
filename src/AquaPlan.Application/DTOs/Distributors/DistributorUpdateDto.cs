using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.Distributors;

public record DistributorUpdateDto(
    [Required] string Name,
    string? ShortName,
    string? CantonRegion,
    string? DistributionNetwork);
