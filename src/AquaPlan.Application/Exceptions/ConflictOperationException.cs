namespace AquaPlan.Application.Exceptions;

/// <summary>
/// AQ-394 — thrown when a business operation conflicts with the current
/// state of a resource (e.g. starting a sampling round that is already
/// InProgress).
/// Mapped to HTTP 409 Conflict by <c>ApiExceptionFilterAttribute</c>.
/// </summary>
public class ConflictOperationException : Exception
{
    public ConflictOperationException(string message) : base(message)
    {
    }
}
