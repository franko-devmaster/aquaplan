namespace AquaPlan.Domain.Entities;

public class Sampling
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public Order? Order { get; set; }
    public string PreleveurId { get; set; } = string.Empty;
    public AppUser? Preleveur { get; set; }
    public DateTime SamplingDateTime { get; set; }
    public double? Temperature { get; set; }
    public string? Weather { get; set; }
    public double? LocationLat { get; set; }
    public double? LocationLng { get; set; }
    public string? Notes { get; set; }
    public bool? HasWaterSoftener { get; set; }
    public bool IsChlorinated { get; set; }
    public string? SampleBarcode { get; set; }
    public DateTime? BarcodeScannedAt { get; set; }
    public bool IsValidated { get; set; }
    public DateTime? ValidatedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? UpdatedAt { get; set; }
}
