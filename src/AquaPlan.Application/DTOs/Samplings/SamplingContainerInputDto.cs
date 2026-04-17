namespace AquaPlan.Application.DTOs.Samplings;

public record SamplingContainerInputDto(
    Guid ContainerId,
    string? Barcode,
    DateTime? BarcodeScannedAt = null);
