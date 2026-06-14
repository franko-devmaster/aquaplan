using System.Text.Json;
using AquaPlan.Api.Middleware;
using AquaPlan.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Api.Tests.Middleware;

public class BusinessExceptionMiddlewareTest
{
    private readonly Mock<ILogger<BusinessExceptionMiddleware>> _loggerMock = new();

    private static async Task<(int status, string body)> InvokeAsync(
        BusinessExceptionMiddleware sut)
    {
        var context = new DefaultHttpContext();
        var responseStream = new MemoryStream();
        context.Response.Body = responseStream;

        await sut.InvokeAsync(context);

        responseStream.Position = 0;
        using var reader = new StreamReader(responseStream);
        var body = await reader.ReadToEndAsync();
        return (context.Response.StatusCode, body);
    }

    [Fact]
    public async Task InvokeAsync_ForbiddenOperationException_ShouldReturn403()
    {
        // AQ-394 — préleveur not assigned to round must get 403.
        var sut = new BusinessExceptionMiddleware(
            _ => throw new ForbiddenOperationException("Vous n'êtes pas assigné à cette tournée."),
            _loggerMock.Object);

        var (status, body) = await InvokeAsync(sut);

        status.Should().Be(StatusCodes.Status403Forbidden);
        // Body is JSON-escaped; the key part "pas assign" is ASCII-safe.
        body.Should().Contain("pas assign");
    }

    [Fact]
    public async Task InvokeAsync_ConflictOperationException_ShouldReturn409()
    {
        // AQ-394 — second /start on an InProgress round must get 409.
        var sut = new BusinessExceptionMiddleware(
            _ => throw new ConflictOperationException("La tournée ne peut pas être démarrée."),
            _loggerMock.Object);

        var (status, body) = await InvokeAsync(sut);

        status.Should().Be(StatusCodes.Status409Conflict);
        // Body is JSON-escaped; match the ASCII-safe prefix.
        body.Should().Contain("ne peut pas");
    }

    [Fact]
    public async Task InvokeAsync_RoundLockedException_ShouldReturn409WithStructuredPayload()
    {
        // AQ-371 — structured 409 payload preserved.
        var roundId = Guid.NewGuid();
        var sut = new BusinessExceptionMiddleware(
            _ => throw new RoundLockedException(roundId, "user-1", "User One", DateTime.UtcNow),
            _loggerMock.Object);

        var (status, body) = await InvokeAsync(sut);

        status.Should().Be(StatusCodes.Status409Conflict);
        using var doc = JsonDocument.Parse(body);
        doc.RootElement.GetProperty("error").GetString().Should().Be("round.locked");
        doc.RootElement.GetProperty("roundId").GetGuid().Should().Be(roundId);
    }

    // Polish F-202 — a curated BusinessRuleException maps to 400 and exposes its message.
    [Fact]
    public async Task InvokeAsync_BusinessRuleException_ShouldReturn400WithMessage()
    {
        var sut = new BusinessExceptionMiddleware(
            _ => throw new BusinessRuleException("An unplanned order must have an UnplannedReason."),
            _loggerMock.Object);

        var (status, body) = await InvokeAsync(sut);

        status.Should().Be(StatusCodes.Status400BadRequest);
        body.Should().Contain("UnplannedReason");
    }

    // Polish F-202 — an unexpected (framework) InvalidOperationException must NOT leak its raw
    // message; it returns a generic 500.
    [Fact]
    public async Task InvokeAsync_BareInvalidOperationException_ShouldReturn500Generic()
    {
        var sut = new BusinessExceptionMiddleware(
            _ => throw new InvalidOperationException("Sequence contains no elements"),
            _loggerMock.Object);

        var (status, body) = await InvokeAsync(sut);

        status.Should().Be(StatusCodes.Status500InternalServerError);
        body.Should().NotContain("Sequence contains no elements");
        body.Should().Contain("An error occurred");
    }

    // Polish F-203 — a 403 must not echo internal IDs; the body is a generic message and the
    // detail (with IDs) only goes to the log.
    [Fact]
    public async Task InvokeAsync_UnauthorizedAccessException_ShouldReturn403Generic()
    {
        var sut = new BusinessExceptionMiddleware(
            _ => throw new UnauthorizedAccessException("User abc-123 does not have access to distributor def-456."),
            _loggerMock.Object);

        var (status, body) = await InvokeAsync(sut);

        status.Should().Be(StatusCodes.Status403Forbidden);
        body.Should().NotContain("abc-123");
        body.Should().NotContain("def-456");
        body.Should().Contain("Acc");
    }

    [Fact]
    public async Task InvokeAsync_KeyNotFoundException_ShouldReturn404()
    {
        var sut = new BusinessExceptionMiddleware(
            _ => throw new KeyNotFoundException(),
            _loggerMock.Object);

        var (status, _) = await InvokeAsync(sut);

        status.Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task InvokeAsync_NoException_ShouldNotAlterResponse()
    {
        var sut = new BusinessExceptionMiddleware(
            ctx =>
            {
                ctx.Response.StatusCode = StatusCodes.Status200OK;
                return Task.CompletedTask;
            },
            _loggerMock.Object);

        var (status, _) = await InvokeAsync(sut);

        status.Should().Be(StatusCodes.Status200OK);
    }
}
