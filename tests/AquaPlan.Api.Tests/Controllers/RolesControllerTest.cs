using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.Roles;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Api.Tests.Controllers;

public class RolesControllerTest
{
    private readonly Mock<IPermissionService> _permissionServiceMock = new();
    private readonly Mock<ILogger<RolesController>> _loggerMock = new();
    private readonly RolesController _sut;

    public RolesControllerTest()
    {
        _sut = new RolesController(_permissionServiceMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task GetRoles_ShouldReturnOk()
    {
        var roles = new List<RoleDto>
        {
            new("r1", "Administrator", "Admin role", DateTime.UtcNow),
            new("r2", "User", "Standard user", DateTime.UtcNow),
        };
        _permissionServiceMock
            .Setup(x => x.GetRolesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(roles);

        var result = await _sut.GetRoles(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(roles);
    }

    [Fact]
    public async Task GetRole_ShouldReturnNotFound_WhenRoleDoesNotExist()
    {
        _permissionServiceMock
            .Setup(x => x.GetRoleWithPermissionsAsync("unknown", It.IsAny<CancellationToken>()))
            .ReturnsAsync((RoleWithPermissionsDto?)null);

        var result = await _sut.GetRole("unknown", CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task GetRole_ShouldReturnOk_WhenRoleExists()
    {
        var role = new RoleWithPermissionsDto("r1", "Administrator", "Admin role",
            new List<PermissionDto> { new(Guid.NewGuid(), "ViewAllOrders", "View all orders") });
        _permissionServiceMock
            .Setup(x => x.GetRoleWithPermissionsAsync("r1", It.IsAny<CancellationToken>()))
            .ReturnsAsync(role);

        var result = await _sut.GetRole("r1", CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(role);
    }

    [Fact]
    public async Task GetPermissions_ShouldReturnOk()
    {
        var permissions = new List<PermissionDto>
        {
            new(Guid.NewGuid(), "ViewAllOrders", "View all orders"),
            new(Guid.NewGuid(), "ManageUsers", "Manage users"),
        };
        _permissionServiceMock
            .Setup(x => x.GetPermissionsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(permissions);

        var result = await _sut.GetPermissions(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(permissions);
    }

    [Fact]
    public async Task AssignRole_ShouldReturnNoContent_WhenSuccess()
    {
        var dto = new RoleAssignDto("Administrator");
        _permissionServiceMock
            .Setup(x => x.AssignRoleToUserAsync("user-1", "Administrator", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var result = await _sut.AssignRole("user-1", dto, CancellationToken.None);

        result.Should().BeOfType<NoContentResult>();
    }

    [Fact]
    public async Task AssignRole_ShouldReturnNotFound_WhenUserDoesNotExist()
    {
        var dto = new RoleAssignDto("Administrator");
        _permissionServiceMock
            .Setup(x => x.AssignRoleToUserAsync("unknown", "Administrator", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        var result = await _sut.AssignRole("unknown", dto, CancellationToken.None);

        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public void Controller_ShouldHaveAdministratorAuthorizeAttribute()
    {
        var attributes = typeof(RolesController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }
}
