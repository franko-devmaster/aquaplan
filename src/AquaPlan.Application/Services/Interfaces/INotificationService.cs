using AquaPlan.Application.DTOs.Notifications;
using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.Services.Interfaces;

/// <summary>
/// AQ-43 — Notification center service. v0.92 is Mock-only: each <c>CreateAsync</c>
/// call persists a <c>Notification</c> row PLUS a <c>NotificationLog</c> row that
/// captures the email that WOULD have been sent (subject + body) — without
/// actually dispatching anything over SMTP.
/// </summary>
public interface INotificationService
{
    /// <summary>
    /// Creates a new notification for the given recipient and persists the mock-email trace.
    /// Returns the persisted notification id.
    /// </summary>
    Task<Guid> CreateAsync(
        string userId,
        NotificationType type,
        string title,
        string message,
        Guid tenantId,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null,
        bool isUrgent = false,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the recent notifications of a single user, most recent first.
    /// </summary>
    Task<NotificationListDto> GetForUserAsync(
        string userId,
        Guid tenantId,
        bool unreadOnly,
        int take,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Count of unread notifications for the current user (cheap query for polling the bell badge).
    /// </summary>
    Task<int> GetUnreadCountAsync(
        string userId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a single notification as read. Idempotent.
    /// Returns <c>true</c> when the notification exists and belongs to the caller,
    /// <c>false</c> when not found, throws <see cref="UnauthorizedAccessException"/>
    /// when the notification belongs to another user.
    /// </summary>
    Task<bool> MarkAsReadAsync(
        Guid notificationId,
        string userId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks every unread notification of the caller as read. Returns the count of updated rows.
    /// </summary>
    Task<int> MarkAllAsReadAsync(
        string userId,
        Guid tenantId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Admin-only: returns the recent mock-email log entries for the tenant.
    /// </summary>
    Task<IReadOnlyList<NotificationLogDto>> GetLogsAsync(
        Guid tenantId,
        int take,
        CancellationToken cancellationToken = default);
}
