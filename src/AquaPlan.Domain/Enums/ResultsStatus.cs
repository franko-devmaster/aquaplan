namespace AquaPlan.Domain.Enums;

/// <summary>
/// AQ-31 — Aggregated conformity status of an Order's analysis results.
/// Derived from <see cref="Entities.SamplingResult"/> collection.
/// </summary>
public enum ResultsStatus
{
    /// <summary>No results received yet (order not Done or results pending).</summary>
    NotReceived = 0,

    /// <summary>All received results are conform (value within reference range).</summary>
    Conform = 1,

    /// <summary>At least one received result is non-conform.</summary>
    NonConform = 2,
}
