using System.Security.Claims;
using AquaPlan.Application.DTOs.Notifications;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

/// <summary>
/// AQ-43 — In-app notification center endpoints (per-user).
/// </summary>
[Route("api/notifications")]
[ApiController]
[Authorize]
public class NotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<NotificationListDto>> GetForCurrentUser(
        [FromQuery] bool unreadOnly = false,
        [FromQuery] int take = 20,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var result = await notificationService.GetForUserAsync(userId, tenantId, unreadOnly, take, cancellationToken);
        return Ok(result);
    }

    [HttpGet("unread-count")]
    public async Task<ActionResult<int>> GetUnreadCount(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var count = await notificationService.GetUnreadCountAsync(userId, tenantId, cancellationToken);
        return Ok(count);
    }

    [HttpPost("{id:guid}/mark-read")]
    public async Task<ActionResult> MarkAsRead(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        try
        {
            var updated = await notificationService.MarkAsReadAsync(id, userId, tenantId, cancellationToken);
            if (!updated) return NotFound();
            return NoContent();
        }
        catch (UnauthorizedAccessException)
        {
            return Forbid();
        }
    }

    [HttpPost("mark-all-read")]
    public async Task<ActionResult<int>> MarkAllAsRead(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var count = await notificationService.MarkAllAsReadAsync(userId, tenantId, cancellationToken);
        return Ok(count);
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
    }

    private Guid GetTenantId()
    {
        var tenantClaim = User.FindFirst("tenant_id")?.Value ?? throw new UnauthorizedAccessException();
        return Guid.Parse(tenantClaim);
    }
}

/// <summary>
/// AQ-43 — Admin-only notification audit log (mock email trace).
/// </summary>
[Route("api/admin/notifications")]
[ApiController]
[Authorize(Roles = RoleName.Administrator)]
public class AdminNotificationsController(INotificationService notificationService) : ControllerBase
{
    [HttpGet("log")]
    public async Task<ActionResult<IReadOnlyList<NotificationLogDto>>> GetLog(
        [FromQuery] int take = 50,
        CancellationToken cancellationToken = default)
    {
        var tenantClaim = User.FindFirst("tenant_id")?.Value ?? throw new UnauthorizedAccessException();
        var tenantId = Guid.Parse(tenantClaim);
        var result = await notificationService.GetLogsAsync(tenantId, take, cancellationToken);
        return Ok(result);
    }
}
