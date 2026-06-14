namespace AquaPlan.Application.Exceptions;

/// <summary>
/// AQ-394 — thrown when the authenticated caller does not have permission
/// to perform the requested operation (e.g. a préleveur trying to start a
/// sampling round that is not assigned to them).
/// Mapped to HTTP 403 Forbidden by <c>BusinessExceptionMiddleware</c>.
/// </summary>
public class ForbiddenOperationException : Exception
{
    public ForbiddenOperationException(string message) : base(message)
    {
    }
}
