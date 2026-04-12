namespace AquaPlan.Application.DTOs.Orders;

public record SamplingDto(
    Guid Id,
    Guid OrderId,
    string PreleveurId,
    string? PreleveurName,
    DateTime SamplingDateTime,
    double? Temperature,
    string? Weather,
    double? LocationLat,
    double? LocationLng,
    string? Notes,
    string? SampleBarcode,
    DateTime? BarcodeScannedAt,
    bool IsValidated,
    DateTime? ValidatedAt,
    DateTime CreatedAt);
