using System.ComponentModel.DataAnnotations;
using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.SamplingRounds;

public record SamplingRoundCreateDto(
    [Required][StringLength(200)] string Name,
    [StringLength(2000)] string? Description,
    DateTime? Deadline,
    [StringLength(2000)] string? Notes,
    [Required] Guid DistributorId);

public record SamplingRoundUpdateDto(
    [Required][StringLength(200)] string Name,
    [StringLength(2000)] string? Description,
    DateTime? Deadline,
    [StringLength(2000)] string? Notes);

public record SamplingRoundAssignDto(
    [Required] string PreleveurId);

public record SamplingRoundAddOrderDto(
    [Required] Guid OrderId);

public record SamplingRoundReorderDto(
    [Required] List<OrderPositionDto> Positions);

public record OrderPositionDto(
    [Required] Guid OrderId,
    [Required][Range(0, 999)] int SortOrder);

public record LocationReplacementDto(
    [Required] Guid NewSamplingLocationId,
    [Required][StringLength(500, MinimumLength = 5)] string Reason);

public record SamplerCommentDto(
    [Required][StringLength(2000)] string Comment);

public record SamplingRoundListDto(
    Guid Id,
    string Name,
    string? Description,
    DateTime? Deadline,
    SamplingRoundStatus Status,
    string? PreleveurId,
    string? PreleveurName,
    Guid DistributorId,
    string DistributorName,
    string? DistributorShortName,
    string? Notes,
    int OrderCount,
    int CompletedOrderCount,
    DateTime CreatedAt,
    bool IsLocked,
    string? LockedById,
    string? LockedByName,
    DateTime? LockedAt);

public record SamplingRoundDetailDto(
    Guid Id,
    string Name,
    string? Description,
    DateTime? Deadline,
    SamplingRoundStatus Status,
    string? PreleveurId,
    string? PreleveurName,
    Guid DistributorId,
    string DistributorName,
    string? DistributorShortName,
    string? Notes,
    string CreatedById,
    string CreatedByName,
    DateTime CreatedAt,
    DateTime? UpdatedAt,
    DateTime? CompletedAt,
    List<SamplingRoundOrderDto> Orders,
    IList<RoundContainerSummaryDto> ContainerSummary,
    bool IsLocked,
    string? LockedById,
    string? LockedByName,
    DateTime? LockedAt);

public record RoundContainerSummaryDto(
    Guid ContainerId,
    string Code,
    string Name,
    string Material,
    int VolumeMl,
    int Count);

public record SamplingRoundOrderDto(
    Guid Id,
    string OrderNumber,
    OrderStatus Status,
    int SortOrder,
    Guid? SamplingLocationId,
    string? SamplingLocationName,
    string? SamplingLocationCode,
    string? SectorName,
    Guid? OriginalSamplingLocationId,
    string? OriginalSamplingLocationName,
    string? LocationReplacementReason,
    string? SamplerComment,
    string? Notes,
    List<string> AnalysisProgramNames,
    // AQ-414 — indicator flags for the sampling-round detail table.
    bool HasMandatorNote,
    bool HasPreleveurNote,
    bool HasReplacedLocation,
    string? PreleveurNote);

public record SamplingRoundFilterDto(
    IReadOnlyList<SamplingRoundStatus>? Statuses = null,
    Guid? DistributorId = null,
    string? PreleveurId = null,
    DateTime? DeadlineFrom = null,
    DateTime? DeadlineTo = null,
    string? Search = null,
    int Page = 1,
    int PageSize = 20);

public record SamplingRoundPagedResultDto(
    List<SamplingRoundListDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
