namespace AquaPlan.Domain.Enums;

/// <summary>
/// AQ-43 — Types of in-app notifications raised by the platform.
/// </summary>
public enum NotificationType
{
    /// <summary>A mandate has been assigned to the user (préleveur).</summary>
    OrderAssigned = 0,

    /// <summary>A sampling round has been assigned to the user (préleveur).</summary>
    RoundAssigned = 1,

    /// <summary>Results have been received for a mandate (AQ-44).</summary>
    ResultsReceived = 2,

    /// <summary>At least one result is non-conform — urgent alert (AQ-45).</summary>
    NonConformResult = 3,
}
