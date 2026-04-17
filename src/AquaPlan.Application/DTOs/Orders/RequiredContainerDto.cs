namespace AquaPlan.Application.DTOs.Orders;

public record RequiredContainerDto(
    Guid ContainerId,
    string Code,
    string Name,
    string Material,
    int VolumeMl,
    string? ExistingBarcode);
