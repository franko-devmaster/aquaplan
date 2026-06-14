using AquaPlan.Application.DTOs.Results;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

/// <summary>
/// AQ-415 — Provides the two read-only projections used by the /results screen:
/// "recent results" cards and the LDP × date matrix.
/// </summary>
internal class ResultsService(
    AquaPlanDbContext dbContext,
    IDelegationService delegationService,
    IConfiguration configuration,
    ILogger<ResultsService> logger) : IResultsService
{
    // Polish F-212 — hard upper bound on the number of orders rapatriated for the matrix, so a
    // wide date range can never load an unbounded graph (results + LDP + sector + programmes).
    private const int MaxMatrixOrders = 2000;

    // AQ-415 — default list of parameters considered critical for conformity colouring.
    // Overridable via appsettings:ResultsConfig:CriticalParameters.
    private static readonly string[] DefaultCriticalParameters =
    [
        "E_COLI",
        "ENTEROCOQUES",
        "NITRATES",
        "NITRITES",
        "PLOMB",
        "ARSENIC",
        "PESTICIDES",
    ];

    private HashSet<string> GetCriticalParameters()
    {
        var configured = configuration.GetSection("ResultsConfig:CriticalParameters").Get<string[]>();
        var source = configured is { Length: > 0 } ? configured : DefaultCriticalParameters;
        return new HashSet<string>(source, StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlyList<RecentResultDto>> GetRecentAsync(
        string userId,
        Guid tenantId,
        bool isAdmin,
        int daysWindow = 7,
        int take = 50,
        CancellationToken cancellationToken = default)
    {
        if (daysWindow <= 0)
        {
            daysWindow = 7;
        }
        if (take <= 0 || take > 500)
        {
            take = 50;
        }

        var threshold = DateTime.UtcNow.AddDays(-daysWindow);
        var authorizedDistributorIds = isAdmin
            ? null
            : await delegationService.GetAuthorizedDistributorIdsForUserAsync(userId, cancellationToken);

        var query = dbContext.Orders
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId
                        && o.ResultsReceivedAt != null
                        && o.ResultsReceivedAt >= threshold);

        if (!isAdmin)
        {
            var ids = authorizedDistributorIds ?? new List<Guid>();
            query = query.Where(o => ids.Contains(o.DistributorId));
        }

        var orders = await query
            .Include(o => o.SamplingLocation)
            .Include(o => o.SamplingResults)
            .Include(o => o.OrderAnalysisPrograms).ThenInclude(oap => oap.AnalysisProgram)
            .OrderByDescending(o => o.ResultsReceivedAt)
            .Take(take)
            .ToListAsync(cancellationToken);

        var criticals = GetCriticalParameters();
        var result = orders
            .Select(o => new RecentResultDto(
                o.Id,
                o.OrderNumber,
                o.SamplingLocationId,
                o.SamplingLocation?.Name ?? string.Empty,
                o.SamplingLocation?.LocationCode ?? string.Empty,
                BuildProgramName(o),
                o.ResultsReceivedAt ?? DateTime.MinValue,
                ComputeConformity(o.SamplingResults, criticals)))
            .ToList();

        return result;
    }

    public async Task<ResultsMatrixDto> GetMatrixAsync(
        string userId,
        Guid tenantId,
        bool isAdmin,
        Guid? distributorId,
        Guid? sectorId,
        bool anomaliesOnly,
        DateTime? dateFrom,
        DateTime? dateTo,
        CancellationToken cancellationToken = default)
    {
        var authorizedDistributorIds = isAdmin
            ? null
            : await delegationService.GetAuthorizedDistributorIdsForUserAsync(userId, cancellationToken);

        var query = dbContext.Orders
            .AsNoTracking()
            .Where(o => o.TenantId == tenantId
                        && o.ResultsReceivedAt != null
                        && o.SamplingLocationId != null);

        if (!isAdmin)
        {
            var ids = authorizedDistributorIds ?? new List<Guid>();
            query = query.Where(o => ids.Contains(o.DistributorId));
        }

        if (distributorId.HasValue)
        {
            query = query.Where(o => o.DistributorId == distributorId.Value);
        }

        if (sectorId.HasValue)
        {
            query = query.Where(o => o.SamplingLocation != null && o.SamplingLocation.SectorId == sectorId.Value);
        }

        // Polish F-212 — bound the matrix. Without an explicit lower bound the query would load
        // every order with results ever received for the tenant (growing every year). Default to a
        // rolling 12-month window when no dateFrom is supplied.
        var effectiveFrom = dateFrom?.Date ?? DateTime.UtcNow.Date.AddMonths(-12);
        var from = DateTime.SpecifyKind(effectiveFrom, DateTimeKind.Utc);
        query = query.Where(o => o.ResultsReceivedAt >= from);

        if (dateTo.HasValue)
        {
            var to = DateTime.SpecifyKind(dateTo.Value.Date.AddDays(1), DateTimeKind.Utc);
            query = query.Where(o => o.ResultsReceivedAt < to);
        }

        var orders = await query
            .OrderByDescending(o => o.ResultsReceivedAt)
            // Polish F-212 — hard cap so a wide date range cannot rapatriate an unbounded graph.
            .Take(MaxMatrixOrders)
            .Include(o => o.SamplingLocation).ThenInclude(l => l!.Sector)
            .Include(o => o.SamplingLocation).ThenInclude(l => l!.Distributor)
            .Include(o => o.SamplingResults)
            .Include(o => o.OrderAnalysisPrograms).ThenInclude(oap => oap.AnalysisProgram)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var criticals = GetCriticalParameters();

        var cells = orders
            .Select(o => new
            {
                Order = o,
                Conformity = ComputeConformity(o.SamplingResults, criticals),
                Date = (o.ResultsReceivedAt ?? DateTime.UtcNow).Date,
            })
            .Where(x => !anomaliesOnly
                        || x.Conformity == ResultConformity.Yellow
                        || x.Conformity == ResultConformity.Red)
            .Select(x => new ResultsCellDto(
                x.Order.SamplingLocationId!.Value,
                DateTime.SpecifyKind(x.Date, DateTimeKind.Utc),
                x.Order.Id,
                x.Order.OrderNumber,
                BuildProgramName(x.Order),
                x.Conformity))
            .ToList();

        var locations = orders
            .Where(o => o.SamplingLocation != null)
            .Select(o => o.SamplingLocation!)
            .Where(l => cells.Any(c => c.LocationId == l.Id))
            .DistinctBy(l => l.Id)
            .OrderBy(l => l.Name, StringComparer.OrdinalIgnoreCase)
            .Select(l => new ResultsMatrixLocationDto(
                l.Id,
                l.LocationCode,
                l.Name,
                l.Sector?.Name ?? string.Empty,
                l.DistributorId,
                l.Distributor?.Name ?? string.Empty))
            .ToList();

        var dates = cells
            .Select(c => c.Date)
            .Distinct()
            .OrderByDescending(d => d)
            .ToList();

        logger.LogDebug(
            "Results matrix for tenant {TenantId}: {Locations} locations, {Dates} dates, {Cells} cells",
            tenantId, locations.Count, dates.Count, cells.Count);

        return new ResultsMatrixDto(locations, dates, cells);
    }

    private static string BuildProgramName(Order order)
    {
        var names = order.OrderAnalysisPrograms
            .Select(p => p.AnalysisProgram?.Name)
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n!)
            .Distinct()
            .ToList();
        return names.Count == 0 ? string.Empty : string.Join(", ", names);
    }

    /// <summary>
    /// AQ-415 — conformity rules:
    /// - Pending: no SamplingResults yet
    /// - Red: at least one non-conform result on a critical parameter
    /// - Yellow: at least one non-conform result on a non-critical parameter
    /// - Green: all conform
    /// </summary>
    internal static ResultConformity ComputeConformity(
        IEnumerable<SamplingResult>? results,
        HashSet<string> criticalParameters)
    {
        var list = results?.ToList();
        if (list is null || list.Count == 0)
        {
            return ResultConformity.Pending;
        }

        var nonConform = list.Where(r => !r.IsConform).ToList();
        if (nonConform.Count == 0)
        {
            return ResultConformity.Green;
        }

        var hasCritical = nonConform.Any(r => criticalParameters.Contains(r.ParameterCode));
        return hasCritical ? ResultConformity.Red : ResultConformity.Yellow;
    }
}
