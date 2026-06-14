namespace AquaPlan.Application.Exceptions;

/// <summary>
/// Polish F-202 — thrown when a domain/business rule is violated and the message is curated
/// and safe to surface to the client (e.g. "An unplanned order must have an UnplannedReason.").
/// Mapped to HTTP 400 Bad Request by <see cref="AquaPlan.Api.Middleware.BusinessExceptionMiddleware"/>.
///
/// It derives from <see cref="InvalidOperationException"/> so existing
/// <c>catch (InvalidOperationException)</c> blocks keep working, while letting the middleware
/// tell an intentional business violation (safe message) apart from an unexpected framework
/// <see cref="InvalidOperationException"/> (returned as a generic 500, message logged only).
/// </summary>
public class BusinessRuleException : InvalidOperationException
{
    public BusinessRuleException(string message) : base(message)
    {
    }
}
