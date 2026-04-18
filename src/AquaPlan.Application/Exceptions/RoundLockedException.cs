namespace AquaPlan.Application.Exceptions;

/// <summary>
/// AQ-371 — thrown when a write operation targets a sampling round that is
/// currently locked by a different user (préleveur working offline).
/// Mapped to HTTP 409 Conflict by the API exception filters.
/// </summary>
public class RoundLockedException : Exception
{
    public Guid RoundId { get; }
    public string? LockedById { get; }
    public string? LockedByName { get; }
    public DateTime? LockedAt { get; }

    public RoundLockedException(Guid roundId, string? lockedById, string? lockedByName, DateTime? lockedAt)
        : base($"Sampling round {roundId} is locked by {lockedByName ?? lockedById ?? "another user"}.")
    {
        RoundId = roundId;
        LockedById = lockedById;
        LockedByName = lockedByName;
        LockedAt = lockedAt;
    }
}
