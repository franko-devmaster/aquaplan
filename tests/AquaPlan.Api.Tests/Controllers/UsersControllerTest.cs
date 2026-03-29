using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.Users;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AquaPlan.Api.Tests.Controllers;

public class UsersControllerTest
{
    private readonly Mock<IUserManagementService> _userManagementServiceMock = new();
    private readonly Mock<ILogger<UsersController>> _loggerMock = new();
    private readonly UsersController _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string UserId = "user-1";

    public UsersControllerTest()
    {
        _sut = new UsersController(_userManagementServiceMock.Object, _loggerMock.Object);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = CreateUser() }
        };
    }

    private static ClaimsPrincipal CreateUser(string userId = UserId, string tenantId = "00000000-0000-0000-0000-000000000001")
    {
        return new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim("tenant_id", tenantId),
        ], "test"));
    }

    [Fact]
    public async Task GetUsers_ShouldReturnOk_WhenUsersExist()
    {
        var users = new List<UserListDto>
        {
            new("u1", "a@b.ch", "John", "Doe", null, true, TenantId, new List<string> { "Administrator" }, DateTime.UtcNow),
            new("u2", "c@d.ch", "Jane", "Doe", "Org", true, TenantId, new List<string> { "User" }, DateTime.UtcNow),
        };
        _userManagementServiceMock
            .Setup(x => x.GetUsersAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(users);

        var result = await _sut.GetUsers(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(users);
    }

    [Fact]
    public async Task GetUser_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        _userManagementServiceMock
            .Setup(x => x.GetUserByIdAsync("unknown", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserDetailDto?)null);

        var result = await _sut.GetUser("unknown", CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetUser_ShouldReturnOk_WhenUserExists()
    {
        var user = new UserDetailDto("u1", "a@b.ch", "John", "Doe", null, true, TenantId,
            new List<string> { "Administrator" }, new List<DistributorSummaryDto>(), DateTime.UtcNow, null);
        _userManagementServiceMock
            .Setup(x => x.GetUserByIdAsync("u1", TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var result = await _sut.GetUser("u1", CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(user);
    }

    [Fact]
    public async Task CreateUser_ShouldReturnCreatedAtAction()
    {
        var createDto = new UserCreateDto("new@user.ch", "New", "User", null, "Password123", TenantId, new List<string> { "User" }, new List<Guid>());
        var createdUser = new UserDetailDto("u3", "new@user.ch", "New", "User", null, true, TenantId,
            new List<string> { "User" }, new List<DistributorSummaryDto>(), DateTime.UtcNow, null);
        _userManagementServiceMock
            .Setup(x => x.CreateUserAsync(createDto, UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(createdUser);

        var result = await _sut.CreateUser(createDto, CancellationToken.None);

        var createdResult = result.Result.Should().BeOfType<CreatedAtActionResult>().Subject;
        createdResult.ActionName.Should().Be(nameof(UsersController.GetUser));
        createdResult.Value.Should().Be(createdUser);
    }

    [Fact]
    public async Task DeactivateUser_ShouldReturnNoContent_WhenSuccess()
    {
        _userManagementServiceMock
            .Setup(x => x.DeactivateUserAsync("u1", UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.DeactivateUser("u1", CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task DeactivateUser_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        _userManagementServiceMock
            .Setup(x => x.DeactivateUserAsync("unknown", UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.DeactivateUser("unknown", CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void GetUsers_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(UsersController).GetMethod(nameof(UsersController.GetUsers));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    [Fact]
    public void GetCurrentUser_ShouldNotHaveAdministratorRoleAttribute()
    {
        var method = typeof(UsersController).GetMethod(nameof(UsersController.GetCurrentUser));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        var adminAttributes = attributes.OfType<AuthorizeAttribute>()
            .Where(a => a.Roles?.Contains("Administrator") == true);
        adminAttributes.Should().BeEmpty();
    }
}
