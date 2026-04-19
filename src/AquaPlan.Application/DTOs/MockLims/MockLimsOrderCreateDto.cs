namespace AquaPlan.Application.DTOs.MockLims;

public record MockLimsOrderCreateDto(
    string OrderReference,
    DateTime SamplingDate,
    IReadOnlyList<string> Parameters,
    Guid? SourceOrderId = null);
