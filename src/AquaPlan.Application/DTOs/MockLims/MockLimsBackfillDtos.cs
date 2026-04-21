namespace AquaPlan.Application.DTOs.MockLims;

/// <summary>
/// AQ-404 — Optional payload for the admin backfill endpoint. Both properties are optional:
/// when omitted, the service backfills every eligible order of the caller's tenant.
/// </summary>
public record MockLimsBackfillRequestDto(
    Guid? TenantId = null,
    int? MaxOrders = null);

/// <summary>
/// AQ-404 — Summary returned by the backfill endpoint. Each failure is kept per order so the
/// administrator can inspect which mandates could not be reconciled with the Mock LIMS.
/// </summary>
public record MockLimsBackfillResultDto(
    int TotalEligible,
    int Processed,
    int SuccessCount,
    int FailureCount,
    IReadOnlyList<MockLimsBackfillFailureDto> Failures);

public record MockLimsBackfillFailureDto(
    Guid OrderId,
    string OrderNumber,
    string Error);
