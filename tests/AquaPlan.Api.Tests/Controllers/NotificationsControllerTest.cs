using System.Reflection;
using System.Security.Claims;
using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.Notifications;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Tests.Controllers;

public class NotificationsControllerTest
{
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly NotificationsController _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string UserId = "user-1";

    public NotificationsControllerTest()
    {
        _sut = new NotificationsController(_notificationServiceMock.Object);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, UserId),
                    new Claim("tenant_id", TenantId.ToString()),
                ], "test")),
            },
        };
    }

    [Fact]
    public async Task GetForCurrentUser_ShouldReturnOkWithList()
    {
        var dto = new NotificationListDto([], 0, 0, 0);
        _notificationServiceMock
            .Setup(s => s.GetForUserAsync(UserId, TenantId, false, 20, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _sut.GetForCurrentUser(unreadOnly: false, take: 20, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetUnreadCount_ShouldReturnCount()
    {
        _notificationServiceMock
            .Setup(s => s.GetUnreadCountAsync(UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(5);

        var result = await _sut.GetUnreadCount(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(5);
    }

    [Fact]
    public async Task MarkAsRead_WhenNotFound_ShouldReturnNotFound()
    {
        _notificationServiceMock
            .Setup(s => s.MarkAsReadAsync(It.IsAny<Guid>(), UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.MarkAsRead(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task MarkAsRead_WhenOk_ShouldReturnNoContent()
    {
        _notificationServiceMock
            .Setup(s => s.MarkAsReadAsync(It.IsAny<Guid>(), UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.MarkAsRead(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task MarkAsRead_WhenOtherUserOwnsTheNotification_ShouldReturnForbid()
    {
        _notificationServiceMock
            .Setup(s => s.MarkAsReadAsync(It.IsAny<Guid>(), UserId, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new UnauthorizedAccessException());

        var result = await _sut.MarkAsRead(Guid.NewGuid(), CancellationToken.None);

        result.Should().BeOfType<ForbidResult>();
    }

    [Fact]
    public async Task MarkAllAsRead_ShouldReturnOkWithCount()
    {
        _notificationServiceMock
            .Setup(s => s.MarkAllAsReadAsync(UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(3);

        var result = await _sut.MarkAllAsRead(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().Be(3);
    }

    [Fact]
    public void Controller_ShouldRequireAuthorize()
    {
        var attr = typeof(NotificationsController).GetCustomAttribute<AuthorizeAttribute>();
        attr.Should().NotBeNull();
    }

    [Fact]
    public void Controller_ShouldHaveExpectedRouteTemplate()
    {
        var route = typeof(NotificationsController).GetCustomAttribute<RouteAttribute>();
        route.Should().NotBeNull();
        route!.Template.Should().Be("api/notifications");
    }

    [Theory]
    [InlineData(nameof(NotificationsController.GetForCurrentUser), null)]
    [InlineData(nameof(NotificationsController.GetUnreadCount), "unread-count")]
    public void GetMethods_ShouldHaveExpectedHttpGetAttribute(string methodName, string? expectedTemplate)
    {
        var method = typeof(NotificationsController).GetMethod(methodName)!;
        var attr = method.GetCustomAttribute<HttpGetAttribute>();
        attr.Should().NotBeNull();
        attr!.Template.Should().Be(expectedTemplate);
    }

    [Theory]
    [InlineData(nameof(NotificationsController.MarkAsRead), "{id:guid}/mark-read")]
    [InlineData(nameof(NotificationsController.MarkAllAsRead), "mark-all-read")]
    public void PostMethods_ShouldHaveExpectedHttpPostAttribute(string methodName, string expectedTemplate)
    {
        var method = typeof(NotificationsController).GetMethod(methodName)!;
        var attr = method.GetCustomAttribute<HttpPostAttribute>();
        attr.Should().NotBeNull();
        attr!.Template.Should().Be(expectedTemplate);
    }

    [Fact]
    public void AdminController_ShouldRequireAdministratorRole()
    {
        var attr = typeof(AdminNotificationsController).GetCustomAttribute<AuthorizeAttribute>();
        attr.Should().NotBeNull();
        attr!.Roles.Should().Be(RoleName.Administrator);
    }

    [Fact]
    public void AdminController_ShouldHaveExpectedRouteTemplate()
    {
        var route = typeof(AdminNotificationsController).GetCustomAttribute<RouteAttribute>();
        route.Should().NotBeNull();
        route!.Template.Should().Be("api/admin/notifications");
    }
}
