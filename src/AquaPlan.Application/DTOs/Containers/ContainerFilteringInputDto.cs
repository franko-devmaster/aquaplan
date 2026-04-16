namespace AquaPlan.Application.DTOs.Containers;

public record ContainerFilteringInputDto(
    string? Search,
    bool? IsActive);
