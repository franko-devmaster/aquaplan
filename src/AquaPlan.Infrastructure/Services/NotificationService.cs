using AquaPlan.Application.DTOs.Notifications;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

/// <summary>
/// AQ-43 — Notification service. Persists in-app notifications and a paired
/// "mock email" trace row. v0.92 does NOT dispatch real emails.
/// </summary>
internal class NotificationService(
    AquaPlanDbContext dbContext,
    ILogger<NotificationService> logger) : INotificationService
{
    public async Task<Guid> CreateAsync(
        string userId,
        NotificationType type,
        string title,
        string message,
        Guid tenantId,
        string? relatedEntityType = null,
        Guid? relatedEntityId = null,
        bool isUrgent = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new ArgumentException("userId is required", nameof(userId));
        }

        var now = DateTime.UtcNow;
        var notification = new Notification
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            Type = type,
            Title = title,
            Message = message,
            IsUrgent = isUrgent,
            IsRead = false,
            CreatedAt = now,
            RelatedEntityType = relatedEntityType,
            RelatedEntityId = relatedEntityId,
            TenantId = tenantId,
        };
        dbContext.Notifications.Add(notification);

        // AQ-43 — lookup recipient email for the mock email log. The FK (user_id)
        // is to the Identity users table; we only need the Email column here.
        var recipientEmail = await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => u.Email)
            .FirstOrDefaultAsync(cancellationToken) ?? string.Empty;

        var body = RenderBody(type, title, message, relatedEntityType, relatedEntityId);
        var subject = RenderSubject(type, title);

        var log = new NotificationLog
        {
            Id = Guid.NewGuid(),
            NotificationId = notification.Id,
            ToEmail = recipientEmail,
            Subject = subject,
            Body = body,
            WasActuallySent = false,
            CreatedAt = now,
            TenantId = tenantId,
        };
        dbContext.NotificationLogs.Add(log);

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Notification {NotificationId} (type={Type}, urgent={IsUrgent}) created for user {UserId} in tenant {TenantId}",
            notification.Id, type, isUrgent, userId, tenantId);

        return notification.Id;
    }

    public async Task<NotificationListDto> GetForUserAsync(
        string userId,
        Guid tenantId,
        bool unreadOnly,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0) take = 20;
        if (take > 200) take = 200;

        var query = dbContext.Notifications
            .Where(n => n.UserId == userId && n.TenantId == tenantId);

        if (unreadOnly)
        {
            query = query.Where(n => !n.IsRead);
        }

        // AQ-45 — urgent unread bubbled first, then most recent.
        var items = await query
            .OrderByDescending(n => !n.IsRead && n.IsUrgent)
            .ThenByDescending(n => n.CreatedAt)
            .Take(take)
            .Select(n => new NotificationDto(
                n.Id,
                n.Type,
                n.Title,
                n.Message,
                n.IsRead,
                n.IsUrgent,
                n.CreatedAt,
                n.ReadAt,
                n.RelatedEntityType,
                n.RelatedEntityId))
            .ToListAsync(cancellationToken);

        var unreadCount = await dbContext.Notifications
            .CountAsync(n => n.UserId == userId && n.TenantId == tenantId && !n.IsRead, cancellationToken);
        var urgentUnreadCount = await dbContext.Notifications
            .CountAsync(n => n.UserId == userId && n.TenantId == tenantId && !n.IsRead && n.IsUrgent, cancellationToken);

        return new NotificationListDto(items, unreadCount, urgentUnreadCount, items.Count);
    }

    public async Task<int> GetUnreadCountAsync(
        string userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        return await dbContext.Notifications
            .CountAsync(n => n.UserId == userId && n.TenantId == tenantId && !n.IsRead, cancellationToken);
    }

    public async Task<bool> MarkAsReadAsync(
        Guid notificationId,
        string userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var notification = await dbContext.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.TenantId == tenantId, cancellationToken);
        if (notification is null)
        {
            return false;
        }
        if (notification.UserId != userId)
        {
            throw new UnauthorizedAccessException("Cannot mark as read a notification owned by another user.");
        }
        if (notification.IsRead)
        {
            return true; // idempotent
        }

        notification.IsRead = true;
        notification.ReadAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<int> MarkAllAsReadAsync(
        string userId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;
        var unread = await dbContext.Notifications
            .Where(n => n.UserId == userId && n.TenantId == tenantId && !n.IsRead)
            .ToListAsync(cancellationToken);
        foreach (var n in unread)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }
        if (unread.Count > 0)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        return unread.Count;
    }

    public async Task<IReadOnlyList<NotificationLogDto>> GetLogsAsync(
        Guid tenantId,
        int take,
        CancellationToken cancellationToken = default)
    {
        if (take <= 0) take = 50;
        if (take > 500) take = 500;

        return await dbContext.NotificationLogs
            .Where(l => l.TenantId == tenantId)
            .OrderByDescending(l => l.CreatedAt)
            .Take(take)
            .Select(l => new NotificationLogDto(
                l.Id,
                l.NotificationId,
                l.ToEmail,
                l.Subject,
                l.Body,
                l.WasActuallySent,
                l.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    private static string RenderSubject(NotificationType type, string title)
    {
        var prefix = type switch
        {
            NotificationType.NonConformResult => "[AquaPlan — URGENT]",
            _ => "[AquaPlan]",
        };
        return $"{prefix} {title}";
    }

    private static string RenderBody(
        NotificationType type,
        string title,
        string message,
        string? relatedEntityType,
        Guid? relatedEntityId)
    {
        var link = (relatedEntityType, relatedEntityId) switch
        {
            ("Order", { } id) => $"/orders/{id}",
            ("SamplingRound", { } id) => $"/sampling-rounds/{id}",
            _ => null,
        };

        var linkLine = link is null ? string.Empty : $"\n\nLien : {link}";
        return $"Type : {type}\nTitre : {title}\n\n{message}{linkLine}";
    }
}
