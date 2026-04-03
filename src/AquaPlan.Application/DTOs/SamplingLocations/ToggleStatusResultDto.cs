namespace AquaPlan.Application.DTOs.SamplingLocations;

public record ToggleStatusResultDto(
    SamplingLocationDto Location,
    bool HasActiveReferences,
    string? Warning = null);
