namespace AquaPlan.Application.DTOs.Orders;

/// <summary>
/// AQ-31 — Aggregate counters for the home dashboard tiles.
/// Counts obey the same tenant + role + delegation scoping as <see cref="OrderListDto"/>.
/// </summary>
public record OrderDashboardSummaryDto(
    int ConformCount,
    int NonConformCount,
    int PendingCount,
    int TotalCount);
