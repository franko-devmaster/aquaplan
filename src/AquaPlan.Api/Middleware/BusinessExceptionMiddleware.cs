using System.Text.Json;
using AquaPlan.Application.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace AquaPlan.Api.Middleware;

public class BusinessExceptionMiddleware(RequestDelegate next, ILogger<BusinessExceptionMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (RoundLockedException ex)
        {
            logger.LogWarning(ex, "Round {RoundId} locked by {LockedBy}", ex.RoundId, ex.LockedById);
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new
            {
                error = "round.locked",
                roundId = ex.RoundId,
                lockedById = ex.LockedById,
                lockedByName = ex.LockedByName,
                lockedAt = ex.LockedAt,
            }));
        }
        catch (ForbiddenOperationException ex)
        {
            // AQ-394 — forbidden operation → 403 Forbidden
            logger.LogWarning(ex, "Forbidden operation: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
        }
        catch (ConflictOperationException ex)
        {
            // AQ-394 — conflict with current state → 409 Conflict
            logger.LogWarning(ex, "Conflict operation: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
        }
        catch (DbUpdateConcurrencyException ex)
        {
            // Polish F-216 — a concurrent write lost the optimistic-concurrency check (e.g. an admin
            // force-unlock racing a préleveur start on the same round). Surface a 409 so the client
            // can reload and retry, rather than a silent last-write-wins or an opaque 500.
            logger.LogWarning(ex, "Concurrency conflict on update");
            context.Response.StatusCode = StatusCodes.Status409Conflict;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "concurrency.conflict" }));
        }
        catch (BusinessRuleException ex)
        {
            // Polish F-202 — intentional, curated business-rule violation: the message is safe to
            // expose and is consumed by the frontend (snackbars, Gherkin assertions).
            logger.LogWarning(ex, "Business rule violation: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = ex.Message }));
        }
        catch (InvalidOperationException ex)
        {
            // Polish F-202 — a bare InvalidOperationException reaching here is an *unexpected*
            // condition (framework: LINQ Single(), EF "connection disposed", DI…). Its raw message
            // must not leak to the client; surface a generic 500 and keep the detail in the log.
            logger.LogError(ex, "Unexpected InvalidOperationException: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "An error occurred while processing your request." }));
        }
        catch (UnauthorizedAccessException ex)
        {
            // Polish F-203 — keep the detail (which may embed internal user/distributor IDs) in the
            // server log only; return a generic message to the client to avoid leaking identifiers
            // useful for enumeration.
            logger.LogWarning(ex, "Unauthorized: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "Accès refusé." }));
        }
        catch (KeyNotFoundException)
        {
            context.Response.StatusCode = StatusCodes.Status404NotFound;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogError(ex, "Unhandled exception: {Message}", ex.Message);
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            context.Response.ContentType = "application/json";
            await context.Response.WriteAsync(JsonSerializer.Serialize(new { error = "An error occurred while processing your request." }));
        }
    }
}
