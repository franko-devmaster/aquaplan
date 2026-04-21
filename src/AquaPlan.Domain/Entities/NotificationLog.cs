namespace AquaPlan.Domain.Entities;

/// <summary>
/// AQ-43 — Traceability row for the "mock email" that would be dispatched for a
/// <see cref="Notification"/>. v0.92 does NOT send real emails; this log keeps
/// the subject/body/recipient that would have been used, so that devs and admins
/// can audit the trigger chain.
/// </summary>
public class NotificationLog
{
    public Guid Id { get; set; }

    /// <summary>FK to the <see cref="Notification"/> that generated this log.</summary>
    public Guid NotificationId { get; set; }
    public Notification? Notification { get; set; }

    public string ToEmail { get; set; } = string.Empty;
    public string Subject { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;

    /// <summary>
    /// Always <c>false</c> in v0.92 (mock). Reserved for the day real SMTP is wired.
    /// </summary>
    public bool WasActuallySent { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Guid TenantId { get; set; }
}
