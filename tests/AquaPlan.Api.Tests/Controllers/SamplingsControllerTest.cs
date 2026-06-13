using System.Security.Claims;
using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.DTOs.Samplings;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Api.Tests.Controllers;

public class SamplingsControllerTest
{
    private readonly Mock<ISamplingService> _samplingServiceMock = new();
    private readonly Mock<IOrderService> _orderServiceMock = new();
    private readonly Mock<IPermissionService> _permissionServiceMock = new();
    private readonly Mock<ILogger<SamplingsController>> _loggerMock = new();
    private readonly SamplingsController _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OrderId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private const string UserId = "user-1";

    public SamplingsControllerTest()
    {
        _sut = new SamplingsController(
            _samplingServiceMock.Object,
            _orderServiceMock.Object,
            _permissionServiceMock.Object,
            _loggerMock.Object);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = CreateUser() }
        };
    }

    private static ClaimsPrincipal CreateUser()
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, UserId),
            new Claim("tenant_id", TenantId.ToString()),
        ], "test"));
    }

    private void GrantViewAllOrders()
    {
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
    }

    [Fact]
    public async Task GetSampling_ShouldReturnNotFound_WhenMissing()
    {
        _samplingServiceMock
            .Setup(x => x.GetByOrderIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SamplingDto?)null);

        var result = await _sut.GetSampling(OrderId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ValidateSampling_ShouldReturnOk_WhenAdminAndSuccess()
    {
        GrantViewAllOrders();
        _samplingServiceMock
            .Setup(x => x.ValidateAsync(OrderId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.ValidateSampling(OrderId, CancellationToken.None);

        result.Should().BeOfType<OkResult>();
    }

    [Fact]
    public async Task ValidateSampling_ShouldReturnForbid_WhenNonAdminCannotAccessOrder()
    {
        // F-115 — a non-admin without access to the order cannot validate its sampling.
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.UserCanAccessOrderAsync(UserId, OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _orderServiceMock
            .Setup(x => x.GetOrderByIdAsync(OrderId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OrderDetailDto(
                OrderId, "ORD-001", OrderStatus.Completed, false, null, null,
                UserId, "John", null, null, Guid.NewGuid(), "Dist",
                null, null, null, null, null, false, [],
                TenantId, DateTime.UtcNow, null, null, null, false, null, null, ResultsStatus.NotReceived));

        var result = await _sut.ValidateSampling(OrderId, CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
        _samplingServiceMock.Verify(
            x => x.ValidateAsync(It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ValidateSampling_ShouldReturnNotFound_WhenServiceReturnsFalse()
    {
        GrantViewAllOrders();
        _samplingServiceMock
            .Setup(x => x.ValidateAsync(OrderId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.ValidateSampling(OrderId, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    #region Controller attributes

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttribute()
    {
        typeof(SamplingsController).GetCustomAttributes(typeof(AuthorizeAttribute), true).Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveApiControllerAttribute()
    {
        typeof(SamplingsController).GetCustomAttributes(typeof(ApiControllerAttribute), true).Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveRouteAttribute()
    {
        var route = typeof(SamplingsController).GetCustomAttributes(typeof(RouteAttribute), true)
            .OfType<RouteAttribute>().First();
        route.Template.Should().Be("api/orders/{orderId:guid}/sampling");
    }

    [Fact]
    public void ValidateSampling_ShouldHaveAuthorizeAttributeRestrictingPreleveurOnly()
    {
        // F-115 — validation reserved to admin + mandataire roles (préleveur-only excluded).
        var method = typeof(SamplingsController).GetMethod(nameof(SamplingsController.ValidateSampling));
        var auth = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true).OfType<AuthorizeAttribute>().FirstOrDefault();
        auth.Should().NotBeNull();
        var roles = (auth!.Roles ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        roles.Should().Contain("Administrator").And.Contain("Requérant").And.Contain("Requérant-Préleveur");
        roles.Should().NotContain("Préleveur");
    }

    [Fact]
    public void ValidateSampling_ShouldHaveHttpPostAttributeWithRoute()
    {
        var method = typeof(SamplingsController).GetMethod(nameof(SamplingsController.ValidateSampling));
        var post = method!.GetCustomAttributes(typeof(HttpPostAttribute), true).OfType<HttpPostAttribute>().First();
        post.Template.Should().Be("validate");
    }

    #endregion
}
