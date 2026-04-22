namespace AquaPlan.Application.DTOs.Results;

/// <summary>
/// AQ-415 — Conformity status of an order's results, derived from the parameter set.
/// </summary>
public enum ResultConformity
{
    Pending = 0,
    Green = 1,
    Yellow = 2,
    Red = 3,
}

/// <summary>
/// AQ-415 — One card in the "recent results" zone of the /results screen.
/// </summary>
public record RecentResultDto(
    Guid OrderId,
    string OrderNumber,
    Guid? SamplingLocationId,
    string LocationName,
    string LocationCode,
    string ProgramName,
    DateTime ReceivedAt,
    ResultConformity Conformity);

/// <summary>
/// AQ-415 — One location row of the results matrix.
/// </summary>
public record ResultsMatrixLocationDto(
    Guid Id,
    string Code,
    string Name,
    string SectorName,
    Guid DistributorId,
    string DistributorName);

/// <summary>
/// AQ-415 — One non-empty cell of the matrix (location × date).
/// </summary>
public record ResultsCellDto(
    Guid LocationId,
    DateTime Date,
    Guid OrderId,
    string OrderNumber,
    string ProgramName,
    ResultConformity Conformity);

/// <summary>
/// AQ-415 — Full payload for the matrix view.
/// </summary>
public record ResultsMatrixDto(
    IReadOnlyList<ResultsMatrixLocationDto> Locations,
    IReadOnlyList<DateTime> Dates,
    IReadOnlyList<ResultsCellDto> Cells);
