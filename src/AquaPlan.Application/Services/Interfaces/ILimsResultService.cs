using AquaPlan.Application.DTOs.SamplingResults;

namespace AquaPlan.Application.Services.Interfaces;

/// <summary>
/// AQ-34 — Pulls analysis results from the (Mock) LIMS and persists them
/// as <see cref="Domain.Entities.SamplingResult"/> rows. Transitions the parent
/// order from <c>Transmitted</c> to <c>Done</c> on the first successful pull.
/// </summary>
public interface ILimsResultService
{
    /// <summary>
    /// Pulls results for a single order (manual trigger or worker iteration).
    /// Idempotent: if results are already persisted, returns the existing rows unchanged.
    /// Returns null if the order does not exist in the given tenant.
    /// </summary>
    Task<PullResultsOutcome?> PullAsync(Guid orderId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the persisted results for an order (no LIMS call).
    /// Returns null if the order does not exist in the tenant.
    /// </summary>
    Task<SamplingResultListDto?> GetByOrderAsync(Guid orderId, Guid tenantId, CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of a <see cref="ILimsResultService.PullAsync"/> call.
/// </summary>
/// <param name="Results">The current list of persisted results for the order (may be empty when not ready).</param>
/// <param name="NewResultsCount">Number of rows newly inserted during this call (0 if already pulled or not ready).</param>
/// <param name="TransitionedToDone">Whether the order status transitioned from Transmitted to Done during this call.</param>
public record PullResultsOutcome(
    SamplingResultListDto Results,
    int NewResultsCount,
    bool TransitionedToDone);
