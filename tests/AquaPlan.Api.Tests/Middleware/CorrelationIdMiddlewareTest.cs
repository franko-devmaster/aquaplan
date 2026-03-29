using AquaPlan.Api.Middleware;
using Microsoft.AspNetCore.Http;

namespace AquaPlan.Api.Tests.Middleware;

public class CorrelationIdMiddlewareTest
{
    [Fact]
    public async Task InvokeAsync_ShouldAddCorrelationIdHeader_WhenNotPresent()
    {
        var context = new DefaultHttpContext();
        var nextCalled = false;
        var sut = new CorrelationIdMiddleware(_ =>
        {
            nextCalled = true;
            return Task.CompletedTask;
        });

        await sut.InvokeAsync(context);

        nextCalled.Should().BeTrue();
        context.Response.Headers["X-Correlation-Id"].ToString().Should().NotBeNullOrEmpty();
    }

    [Fact]
    public async Task InvokeAsync_ShouldPreserveCorrelationId_WhenAlreadyPresent()
    {
        var context = new DefaultHttpContext();
        var expectedId = "test-correlation-id";
        context.Request.Headers["X-Correlation-Id"] = expectedId;
        var sut = new CorrelationIdMiddleware(_ => Task.CompletedTask);

        await sut.InvokeAsync(context);

        context.Response.Headers["X-Correlation-Id"].ToString().Should().Be(expectedId);
    }
}
