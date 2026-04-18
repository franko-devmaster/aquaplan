using AquaPlan.Application.DTOs.SamplingRounds;

namespace AquaPlan.Application.Services.Interfaces;

public interface ISamplingRoundService
{
    Task<SamplingRoundDetailDto> CreateAsync(SamplingRoundCreateDto dto, string createdById, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingRoundDetailDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingRoundPagedResultDto> GetFilteredAsync(string userId, Guid tenantId, SamplingRoundFilterDto filter, bool isAdmin, CancellationToken cancellationToken = default);
    Task<SamplingRoundDetailDto?> UpdateAsync(Guid id, SamplingRoundUpdateDto dto, string updatedBy, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
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
}
