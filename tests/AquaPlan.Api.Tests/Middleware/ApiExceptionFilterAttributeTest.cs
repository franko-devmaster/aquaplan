using AquaPlan.Api.Middleware;
using AquaPlan.Application.Exceptions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Api.Tests.Middleware;

public class ApiExceptionFilterAttributeTest
{
    private readonly Mock<ILogger<ApiExceptionFilterAttribute>> _loggerMock = new();
    private readonly Mock<IHostEnvironment> _environmentMock = new();
    private readonly ApiExceptionFilterAttribute _sut;

    public ApiExceptionFilterAttributeTest()
    {
        _environmentMock.Setup(e => e.EnvironmentName).Returns("Production");
        _sut = new ApiExceptionFilterAttribute(_loggerMock.Object, _environmentMock.Object);
    }

    private static ExceptionContext CreateContext(Exception exception)
    {
        var actionContext = new ActionContext(
            new DefaultHttpContext(),
            new RouteData(),
            new ActionDescriptor());
        return new ExceptionContext(actionContext, new List<IFilterMetadata>())
        {
            Exception = exception,
        };
    }

    [Fact]
    public void OnException_ForbiddenOperationException_ShouldReturn403()
    {
        // AQ-394 — préleveur not assigned to the round must receive 403.
        var context = CreateContext(new ForbiddenOperationException("Vous n'êtes pas assigné à cette tournée."));

        _sut.OnException(context);

        context.ExceptionHandled.Should().BeTrue();
        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public void OnException_ConflictOperationException_ShouldReturn409()
    {
        // AQ-394 — second /start on a round already InProgress must receive 409.
        var context = CreateContext(new ConflictOperationException("La tournée ne peut pas être démarrée dans son statut actuel (InProgress). Elle doit être Assignée."));

        _sut.OnException(context);

        context.ExceptionHandled.Should().BeTrue();
        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void OnException_RoundLockedException_ShouldReturn409()
    {
        // AQ-371 — structured 409 payload is still emitted for round.locked.
        var rlx = new RoundLockedException(Guid.NewGuid(), "user-1", "User One", DateTime.UtcNow);
        var context = CreateContext(rlx);

        _sut.OnException(context);

        context.ExceptionHandled.Should().BeTrue();
        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status409Conflict);
    }

    [Fact]
    public void OnException_InvalidOperationException_ShouldReturn400()
    {
        // Generic InvalidOperationException still maps to 400 (legacy behavior).
        var context = CreateContext(new InvalidOperationException("Bad state"));

        _sut.OnException(context);

        context.ExceptionHandled.Should().BeTrue();
        var result = context.Result.Should().BeOfType<ObjectResult>().Subject;
        result.StatusCode.Should().Be(StatusCodes.Status400BadRequest);
    }

    [Fact]
    public void OnException_KeyNotFoundException_ShouldReturn404()
    {
        var context = CreateContext(new KeyNotFoundException("Not found"));

        _sut.OnException(context);

        context.ExceptionHandled.Should().BeTrue();
        context.Result.Should().BeOfType<NotFoundResult>();
    }
}
