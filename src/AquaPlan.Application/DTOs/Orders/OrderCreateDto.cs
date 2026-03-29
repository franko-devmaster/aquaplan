namespace AquaPlan.Application.DTOs.Orders;

public record OrderCreateDto(
    Guid DistributorId,
    string? PreleveurId,
    bool IsUnplanned);
