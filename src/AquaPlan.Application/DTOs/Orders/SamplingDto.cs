namespace AquaPlan.Application.DTOs.Orders;

public record SamplingDto(
    Guid Id,
    Guid OrderId,
    string PreleveurId,
    string? PreleveurName,
    DateTime SamplingDateTime,
    double? Temperature,
    string? Weather,
    string? Notes,
    bool? HasWaterSoftener,
    bool IsChlorinated,
    string? SampleBarcode,
    DateTime? BarcodeScannedAt,
    bool IsValidated,
    DateTime? ValidatedAt,
    DateTime CreatedAt,
    IList<SamplingContainerDto> Containers);

public record SamplingContainerDto(
    Guid Id,
    Guid ContainerId,
    string? Barcode,
    DateTime? BarcodeScannedAt);
