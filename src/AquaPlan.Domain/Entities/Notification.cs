using AquaPlan.Domain.Enums;

namespace AquaPlan.Domain.Entities;

/// <summary>
/// AQ-43 — In-app notification addressed to a single user.
/// Persisted per-user (no broadcast entity) so that read/unread state is
/// naturally isolated and queries stay simple.
/// </summary>
public class Notification
{
    public Guid Id { get; set; }

    /// <summary>FK to <see cref="AppUser"/> — notification recipient.</summary>
    public string UserId { get; set; } = string.Empty;
    public AppUser? User { get; set; }

    public NotificationType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;

    /// <summary>AQ-45 — marks the notification as visually urgent (red background, icon).</summary>
    public bool IsUrgent { get; set; }

    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadAt { get; set; }

    /// <summary>Optional link to a related entity (e.g. "Order", "SamplingRound").</summary>
    public string? RelatedEntityType { get; set; }
    public Guid? RelatedEntityId { get; set; }

    public Guid TenantId { get; set; }
}
