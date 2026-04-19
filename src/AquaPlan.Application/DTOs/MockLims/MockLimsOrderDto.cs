using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.MockLims;

public record MockLimsOrderDto(
    Guid Id,
    Guid LimsOrderId,
    Guid? OrderId,
    string OrderReference,
    DateTime SamplingDate,
    IReadOnlyList<string> Parameters,
    MockLimsOrderStatus Status,
    DateTime ReceivedAt,
    DateTime? ResultsReadyAt);
