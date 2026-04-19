namespace AquaPlan.Application.DTOs.MockLims;

public record MockLimsOrderCreatedDto(
    Guid LimsOrderId,
    DateTime AcceptedAt);
