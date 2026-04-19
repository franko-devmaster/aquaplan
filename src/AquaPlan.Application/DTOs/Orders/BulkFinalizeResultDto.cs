namespace AquaPlan.Application.DTOs.Orders;

/// <summary>
/// AQ-406 — result of the atomic "finalize all" action:
/// validates InProgress orders (→ Completed) and then transmits all
/// Completed orders (→ Transmitted) in a single transaction.
/// </summary>
public record BulkFinalizeResultDto(int Validated, int Transmitted);
