using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.Samplings;

// Polish F-220 — SampleBarcode length mirrors the EF column limit (samplings.sample_barcode 100);
// free-text Weather/Notes are bounded defensively.
public record SamplingCreateDto(
    [Required] Guid OrderId,
    DateTime SamplingDateTime,
    double? Temperature,
    [StringLength(200)] string? Weather,
    [StringLength(2000)] string? Notes,
    bool? HasWaterSoftener,
    bool IsChlorinated = false,
    [StringLength(100)] string? SampleBarcode = null,
    IList<SamplingContainerInputDto>? Containers = null);
