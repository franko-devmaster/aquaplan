using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.Notifications;

/// <summary>
/// AQ-43 — Notification item returned to the frontend bell dropdown.
/// </summary>
public record NotificationDto(
    Guid Id,
    NotificationType Type,
    string Title,
    string Message,
    bool IsRead,
    bool IsUrgent,
    DateTime CreatedAt,
    DateTime? ReadAt,
    string? RelatedEntityType,
    Guid? RelatedEntityId);

/// <summary>
/// AQ-43 — Paged notification list for the bell dropdown / notification center.
/// </summary>
public record NotificationListDto(
    IReadOnlyList<NotificationDto> Items,
    int UnreadCount,
    int UrgentUnreadCount,
    int TotalCount);

/// <summary>
/// AQ-43 — Admin-only log entry listing the mock emails that would have been dispatched.
/// </summary>
public record NotificationLogDto(
    Guid Id,
    Guid NotificationId,
    string ToEmail,
    string Subject,
    string Body,
    bool WasActuallySent,
    DateTime CreatedAt);
