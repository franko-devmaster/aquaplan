using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.Logging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Api.Tests.Controllers;

public class LogsControllerTest
{
    private readonly Mock<ILogger<LogsController>> _loggerMock = new();
    private readonly LogsController _sut;

    public LogsControllerTest()
    {
        _sut = new LogsController(_loggerMock.Object);
    }

    [Fact]
    public void Post_ShouldReturnOk()
    {
        var dto = new ClientLogDto("error", "Something went wrong", "AuthCallback", DateTime.UtcNow);

        var result = _sut.Post(dto);

        result.Should().BeOfType<OkResult>();
    }

    [Theory]
    [InlineData("error")]
    [InlineData("warn")]
    [InlineData("info")]
    [InlineData("debug")]
    [InlineData("unknown-level")]
    public void Post_ShouldReturnOk_ForAnyLevel(string level)
    {
        var dto = new ClientLogDto(level, "msg", null, null);

        var result = _sut.Post(dto);

        result.Should().BeOfType<OkResult>();
    }

    #region Controller attributes

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttribute_AndNotAllowAnonymous()
    {
        // F-114 — the endpoint must NOT be anonymous anymore.
        var authorize = typeof(LogsController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        var anonymous = typeof(LogsController).GetCustomAttributes(typeof(AllowAnonymousAttribute), true);

        authorize.Should().NotBeEmpty();
        anonymous.Should().BeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveApiControllerAttribute()
    {
        var attributes = typeof(LogsController).GetCustomAttributes(typeof(ApiControllerAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Post_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(LogsController).GetMethod(nameof(LogsController.Post));
        var attributes = method!.GetCustomAttributes(typeof(HttpPostAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    #endregion
}
