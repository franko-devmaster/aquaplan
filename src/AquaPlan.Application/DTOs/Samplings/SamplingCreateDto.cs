namespace AquaPlan.Application.DTOs.Samplings;

public record SamplingCreateDto(
    Guid OrderId,
    DateTime SamplingDateTime,
    double? Temperature,
    string? Weather,
    string? Notes,
    bool? HasWaterSoftener,
    bool IsChlorinated = false,
    string? SampleBarcode = null);
