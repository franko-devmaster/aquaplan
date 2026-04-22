using AquaPlan.Application.DTOs.Results;

namespace AquaPlan.Application.Services.Interfaces;

/// <summary>
/// AQ-415 — Read-only service feeding the /results screen:
///   - a "recent" zone listing mandates with results received in the last 7 days
///   - a matrix crossing sampling locations × dates with one colored cell per order.
/// All queries are tenant-scoped and, for non-admins, restricted to the distributors
/// the user is authorized to see (own distributor + active delegations).
/// </summary>
public interface IResultsService
{
    /// <summary>
    /// Returns the most recent mandates with LIMS results (by <c>ResultsReceivedAt</c>) in the
    /// trailing <paramref name="daysWindow"/>, capped at <paramref name="take"/>.
    /// </summary>
    Task<IReadOnlyList<RecentResultDto>> GetRecentAsync(
        string userId,
        Guid tenantId,
        bool isAdmin,
        int daysWindow = 7,
        int take = 50,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the matrix of results (locations × dates) filtered by the provided criteria.
    /// Dates are truncated to day (UTC). <paramref name="anomaliesOnly"/> keeps only cells
    /// whose conformity is Yellow or Red.
    /// </summary>
    Task<ResultsMatrixDto> GetMatrixAsync(
        string userId,
        Guid tenantId,
        bool isAdmin,
        Guid? distributorId,
        Guid? sectorId,
        bool anomaliesOnly,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default);
}
