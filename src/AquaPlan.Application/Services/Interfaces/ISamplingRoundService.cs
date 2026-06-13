using AquaPlan.Application.DTOs.SamplingRounds;

namespace AquaPlan.Application.Services.Interfaces;

public interface ISamplingRoundService
{
    Task<SamplingRoundDetailDto> CreateAsync(SamplingRoundCreateDto dto, string createdById, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingRoundDetailDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Returns sampling rounds visible to the calling user.
    /// AQ-398 — Visibility rules:
    ///   • Administrators see every round of the tenant.
    ///   • Préleveurs (sole role) only see rounds where they are the assigned <c>PreleveurId</c>.
    ///   • Mandataires (Requérant / Requérant-Préleveur) see every round of the
    ///     distributors they are authorized on (own + active delegations) — they
    ///     are NOT restricted to rounds they created themselves.
    /// </summary>
    Task<SamplingRoundPagedResultDto> GetFilteredAsync(string userId, Guid tenantId, SamplingRoundFilterDto filter, bool isAdmin, bool isPreleveurOnly, CancellationToken cancellationToken = default);
    Task<SamplingRoundDetailDto?> UpdateAsync(Guid id, SamplingRoundUpdateDto dto, string updatedBy, Guid tenantId, CancellationToken cancellationToken = default);
    /// <summary>
    /// Sprint Robustesse F-107 — soft-cancels a round instead of hard-deleting it and its
    /// mandates: the round becomes <c>Cancelled</c> and its orders are detached
    /// (<c>SamplingRoundId = null</c>) but preserved. A locked (InProgress) round is refused
    /// with a <see cref="AquaPlan.Application.Exceptions.ConflictOperationException"/>.
    /// </summary>
    Task<bool> DeleteAsync(Guid id, string userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingRoundDetailDto?> AssignPreleveurAsync(Guid id, SamplingRoundAssignDto dto, string updatedBy, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingRoundDetailDto?> CancelAsync(Guid id, string updatedBy, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingRoundDetailDto?> AddOrderAsync(Guid roundId, Guid orderId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> RemoveOrderAsync(Guid roundId, Guid orderId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingRoundDetailDto?> ReorderAsync(Guid roundId, SamplingRoundReorderDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> ReplaceLocationAsync(Guid orderId, LocationReplacementDto dto, string userId, Guid tenantId, bool isAdmin = false, CancellationToken cancellationToken = default);
    Task<bool> StartOrderAsync(Guid orderId, string userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> UpdateSamplerCommentAsync(Guid orderId, SamplerCommentDto dto, string userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingRoundDetailDto?> RevertToDraftAsync(Guid id, string userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingRoundDetailDto?> TransmitAllAsync(Guid id, string userId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// AQ-370 — transitions a round from Assigned to InProgress, and locks it
    /// so mandataries cannot modify it while the préleveur operates offline.
    /// </summary>
    Task<SamplingRoundDetailDto?> StartAsync(Guid id, string userId, Guid tenantId, bool isAdmin, CancellationToken cancellationToken = default);

    /// <summary>
    /// AQ-372 — Admin-only: releases the lock on a round previously started by a
    /// préleveur whose device is unreachable, moving the round back to Assigned.
    /// </summary>
    Task<SamplingRoundDetailDto?> ForceUnlockAsync(Guid id, string adminUserId, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// AQ-371 — verifies that the round is not locked by someone else. Throws
    /// RoundLockedException if locked by a different user and the caller is not admin.
    /// Safe to call with a null roundId (no-op).
    /// </summary>
    Task EnsureRoundNotLockedForWriteAsync(Guid? roundId, string currentUserId, bool isAdmin, Guid tenantId, CancellationToken cancellationToken = default);

    /// <summary>
    /// AQ-373 — returns a complete offline-ready snapshot of the round: the round
    /// itself, all its orders (with samplings and containers), the distributor's
    /// active+validated sampling locations, and the deduplicated catalog fragments
    /// (programs, profiles, containers). Authorized only for the assigned préleveur
    /// and administrators. Returns null when the round does not exist.
    /// Throws UnauthorizedAccessException when the current user is neither the
    /// assigned préleveur nor an administrator.
    /// </summary>
    Task<OfflineSnapshotDto?> GetOfflineSnapshotAsync(Guid roundId, string currentUserId, bool isAdmin, Guid tenantId, CancellationToken cancellationToken = default);
}
