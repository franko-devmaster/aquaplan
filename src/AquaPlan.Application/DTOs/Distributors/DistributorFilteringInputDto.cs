namespace AquaPlan.Application.DTOs.Distributors;

public record DistributorFilteringInputDto(
    string? Name,
    bool? IsActive);
