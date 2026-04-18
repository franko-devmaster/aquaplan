using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.Distributors;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AquaPlan.Api.Tests.Controllers;

public class DelegationsControllerTest
{
    private readonly Mock<IDelegationService> _delegationServiceMock = new();
    private readonly Mock<IPermissionService> _permissionServiceMock = new();
    private readonly Mock<ILogger<DelegationsController>> _loggerMock = new();
    private readonly DelegationsController _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string UserId = "user-1";

    public DelegationsControllerTest()
    {
        _sut = new DelegationsController(
            _delegationServiceMock.Object,
            _permissionServiceMock.Object,
            _loggerMock.Object);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = CreateUser() },
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
    public void Controller_ShouldHaveAuthorizeAttribute()
    {
        var attributes = typeof(DelegationsController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void GetAll_ShouldHaveAuthorizeAttributeWithAdministrator()
    {
        var method = typeof(DelegationsController).GetMethod(nameof(DelegationsController.GetAll));
        var auth = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true).OfType<AuthorizeAttribute>().First();
        auth.Roles.Should().Be("Administrator");
    }

    [Fact]
    public void Create_ShouldHaveAuthorizeAttributeWithAdministrator()
    {
        var method = typeof(DelegationsController).GetMethod(nameof(DelegationsController.Create));
        var auth = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true).OfType<AuthorizeAttribute>().First();
        auth.Roles.Should().Be("Administrator");
    }

    [Fact]
    public void Delete_ShouldHaveAuthorizeAttributeWithAdministrator()
    {
        var method = typeof(DelegationsController).GetMethod(nameof(DelegationsController.Delete));
        var auth = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true).OfType<AuthorizeAttribute>().First();
        auth.Roles.Should().Be("Administrator");
    }

    // AQ-369 — my-authorized-distributors endpoint

    [Fact]
    public void GetMyAuthorizedDistributors_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(DelegationsController).GetMethod(nameof(DelegationsController.GetMyAuthorizedDistributors));
        var attr = method!.GetCustomAttributes(typeof(HttpGetAttribute), true).OfType<HttpGetAttribute>().First();
        attr.Template.Should().Be("my-authorized-distributors");
    }

    [Fact]
    public void GetMyAuthorizedDistributors_ShouldNotHaveSpecificRoleRestriction()
    {
        // AQ-369 — endpoint is reachable by any authenticated user (uses controller-level [Authorize])
        var method = typeof(DelegationsController).GetMethod(nameof(DelegationsController.GetMyAuthorizedDistributors));
        var methodAuth = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true).OfType<AuthorizeAttribute>().FirstOrDefault();
        methodAuth.Should().BeNull();
    }

    [Fact]
    public async Task GetMyAuthorizedDistributors_WhenAdmin_ShouldReturnAllTenantDistributors()
    {
        var distributors = new List<DistributorDto>
        {
            new(Guid.NewGuid(), "Dist A", null, null, null, true, DateTime.UtcNow),
            new(Guid.NewGuid(), "Dist B", null, null, null, true, DateTime.UtcNow),
        };
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _delegationServiceMock
            .Setup(x => x.GetAuthorizedDistributorsForUserAsync(UserId, TenantId, true, It.IsAny<CancellationToken>()))
            .ReturnsAsync(distributors);

        var result = await _sut.GetMyAuthorizedDistributors(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(distributors);
    }

    [Fact]
    public async Task GetMyAuthorizedDistributors_WhenNotAdmin_ShouldReturnAuthorizedOnly()
    {
        var distributors = new List<DistributorDto>
        {
            new(Guid.NewGuid(), "Own Dist", null, null, null, true, DateTime.UtcNow),
        };
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _delegationServiceMock
            .Setup(x => x.GetAuthorizedDistributorsForUserAsync(UserId, TenantId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(distributors);

        var result = await _sut.GetMyAuthorizedDistributors(CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(distributors);
    }
}
