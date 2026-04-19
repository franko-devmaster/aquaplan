using System.Security.Claims;
using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.MockLims;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AquaPlan.Api.Tests.Controllers;

public class MockLimsControllerTest
{
    private readonly Mock<IMockLimsService> _mockLimsServiceMock = new();
    private readonly Mock<ILogger<MockLimsController>> _loggerMock = new();
    private readonly MockLimsController _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid LimsOrderId = Guid.Parse("00000000-0000-0000-0000-000000000900");

    public MockLimsControllerTest()
    {
        var options = Options.Create(new MockLimsOptions { Enabled = true });
        _sut = new MockLimsController(_mockLimsServiceMock.Object, options, _loggerMock.Object);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = CreateUser() }
        };
    }

    private static ClaimsPrincipal CreateUser(string tenantId = "00000000-0000-0000-0000-000000000001")
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, "admin-1"),
            new Claim("tenant_id", tenantId),
        ], "test"));
    }

    private MockLimsController CreateControllerWithFlag(bool enabled)
    {
        var options = Options.Create(new MockLimsOptions { Enabled = enabled });
        var controller = new MockLimsController(_mockLimsServiceMock.Object, options, _loggerMock.Object);
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = CreateUser() }
        };
        return controller;
    }

    [Fact]
    public async Task CreateOrder_WhenFeatureEnabled_ShouldReturn201WithCreatedDto()
    {
        var dto = new MockLimsOrderCreateDto("ORD-1", DateTime.UtcNow, new[] { "PH" });
        var response = new MockLimsOrderCreatedDto(LimsOrderId, DateTime.UtcNow);
        _mockLimsServiceMock
            .Setup(s => s.ReceiveOrderAsync(dto, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _sut.CreateOrder(dto, CancellationToken.None);

        var objectResult = result.Result.Should().BeOfType<ObjectResult>().Subject;
        objectResult.StatusCode.Should().Be(StatusCodes.Status201Created);
        objectResult.Value.Should().Be(response);
    }

    [Fact]
    public async Task CreateOrder_WhenFeatureDisabled_ShouldReturnNotFound()
    {
        var controller = CreateControllerWithFlag(enabled: false);
        var dto = new MockLimsOrderCreateDto("ORD-1", DateTime.UtcNow, new[] { "PH" });

        var result = await controller.CreateOrder(dto, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task CreateOrder_WhenServiceThrowsArgumentException_ShouldReturn400()
    {
        var dto = new MockLimsOrderCreateDto("", DateTime.UtcNow, new[] { "PH" });
        _mockLimsServiceMock
            .Setup(s => s.ReceiveOrderAsync(dto, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new ArgumentException("bad"));

        var result = await _sut.CreateOrder(dto, CancellationToken.None);

        result.Result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task GetOrder_WhenFoundAndEnabled_ShouldReturnOk()
    {
        var orderDto = new MockLimsOrderDto(Guid.NewGuid(), LimsOrderId, null, "ORD-1", DateTime.UtcNow, new[] { "PH" }, MockLimsOrderStatus.Received, DateTime.UtcNow, null);
        _mockLimsServiceMock
            .Setup(s => s.GetAsync(LimsOrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(orderDto);

        var result = await _sut.GetOrder(LimsOrderId, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(orderDto);
    }

    [Fact]
    public async Task GetOrder_WhenNotFound_ShouldReturnNotFound()
    {
        _mockLimsServiceMock
            .Setup(s => s.GetAsync(LimsOrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MockLimsOrderDto?)null);

        var result = await _sut.GetOrder(LimsOrderId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetOrder_WhenFeatureDisabled_ShouldReturnNotFound()
    {
        var controller = CreateControllerWithFlag(enabled: false);

        var result = await controller.GetOrder(LimsOrderId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetResults_WhenAvailable_ShouldReturnOk()
    {
        var resultsDto = new MockLimsResultListDto(
            LimsOrderId,
            new[] { new MockLimsResultDto("PH", 7.2m, "pH", 6.5m, 8.5m, true) });
        _mockLimsServiceMock
            .Setup(s => s.GetResultsAsync(LimsOrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(resultsDto);

        var result = await _sut.GetResults(LimsOrderId, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(resultsDto);
    }

    [Fact]
    public async Task GetResults_WhenFeatureDisabled_ShouldReturnNotFound()
    {
        var controller = CreateControllerWithFlag(enabled: false);

        var result = await controller.GetResults(LimsOrderId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetResults_WhenNotFound_ShouldReturnNotFound()
    {
        _mockLimsServiceMock
            .Setup(s => s.GetResultsAsync(LimsOrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MockLimsResultListDto?)null);

        var result = await _sut.GetResults(LimsOrderId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void Controller_ShouldHaveAdministratorRoleAuthorization()
    {
        var attributes = typeof(MockLimsController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var auth = attributes.OfType<AuthorizeAttribute>().First();
        auth.Roles.Should().Be("Administrator");
    }

    [Fact]
    public void Controller_ShouldHaveApiControllerAttribute()
    {
        typeof(MockLimsController).GetCustomAttributes(typeof(ApiControllerAttribute), true).Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveRouteAttribute()
    {
        var attr = typeof(MockLimsController).GetCustomAttributes(typeof(RouteAttribute), true)
            .OfType<RouteAttribute>().First();
        attr.Template.Should().Be("api/mock-lims");
    }

    [Fact]
    public void CreateOrder_ShouldHaveHttpPostAttributeWithOrdersRoute()
    {
        var method = typeof(MockLimsController).GetMethod(nameof(MockLimsController.CreateOrder));
        var attr = method!.GetCustomAttributes(typeof(HttpPostAttribute), true)
            .OfType<HttpPostAttribute>().First();
        attr.Template.Should().Be("orders");
    }

    [Fact]
    public void GetOrder_ShouldHaveHttpGetAttributeWithOrdersIdRoute()
    {
        var method = typeof(MockLimsController).GetMethod(nameof(MockLimsController.GetOrder));
        var attr = method!.GetCustomAttributes(typeof(HttpGetAttribute), true)
            .OfType<HttpGetAttribute>().First();
        attr.Template.Should().Be("orders/{limsOrderId:guid}");
    }

    [Fact]
    public void GetResults_ShouldHaveHttpGetAttributeWithResultsRoute()
    {
        var method = typeof(MockLimsController).GetMethod(nameof(MockLimsController.GetResults));
        var attr = method!.GetCustomAttributes(typeof(HttpGetAttribute), true)
            .OfType<HttpGetAttribute>().First();
        attr.Template.Should().Be("orders/{limsOrderId:guid}/results");
    }
}
