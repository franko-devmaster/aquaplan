namespace AquaPlan.Application.DTOs.MockLims;

/// <summary>
/// AQ-404 — Optional payload for the admin backfill endpoint. When omitted, the service
/// backfills every eligible order of the caller's tenant.
/// Sprint Sec F-003 — <c>TenantId</c> was removed: the backfill always operates on the
/// caller's tenant (taken from the JWT), never on a client-supplied tenant.
/// </summary>
public record MockLimsBackfillRequestDto(
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
