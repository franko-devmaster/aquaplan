using System.Reflection;
using System.Security.Claims;
using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.Results;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Tests.Controllers;

public class ResultsControllerTest
{
    private readonly Mock<IResultsService> _resultsServiceMock = new();
    private readonly Mock<IPermissionService> _permissionServiceMock = new();
    private readonly ResultsController _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private const string UserId = "user-1";

    public ResultsControllerTest()
    {
        _sut = new ResultsController(_resultsServiceMock.Object, _permissionServiceMock.Object);
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

        _permissionServiceMock
            .Setup(s => s.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
    }

    [Fact]
    public async Task GetRecent_ShouldReturnOk_WithPayload()
    {
        IReadOnlyList<RecentResultDto> payload =
        [
            new RecentResultDto(Guid.NewGuid(), "O-1", null, "Loc", "LC", "Prog", DateTime.UtcNow, ResultConformity.Green),
        ];
        _resultsServiceMock
            .Setup(s => s.GetRecentAsync(UserId, TenantId, false, 7, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);

        var result = await _sut.GetRecent();

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(payload);
    }

    [Fact]
    public async Task GetRecent_ShouldForwardAdminFlag_WhenUserHasPermission()
    {
        _permissionServiceMock
            .Setup(s => s.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);
        _resultsServiceMock
            .Setup(s => s.GetRecentAsync(UserId, TenantId, true, 7, 50, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetRecent();

        result.Result.Should().BeOfType<OkObjectResult>();
        _resultsServiceMock.Verify(
            s => s.GetRecentAsync(UserId, TenantId, true, 7, 50, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task GetMatrix_ShouldReturnOk_WithPayload()
    {
        var payload = new ResultsMatrixDto([], [], []);
        _resultsServiceMock
            .Setup(s => s.GetMatrixAsync(
                UserId, TenantId, false, null, null, false, null, null,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);

        var result = await _sut.GetMatrix();

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(payload);
    }

    [Fact]
    public async Task GetMatrix_ShouldForwardAllFilters()
    {
        var distributor = Guid.NewGuid();
        var sector = Guid.NewGuid();
        var from = DateTime.UtcNow.AddDays(-30);
        var to = DateTime.UtcNow;
        var payload = new ResultsMatrixDto([], [], []);
        _resultsServiceMock
            .Setup(s => s.GetMatrixAsync(
                UserId, TenantId, false, distributor, sector, true, from, to,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(payload);

        var result = await _sut.GetMatrix(distributor, sector, anomaliesOnly: true, from, to);

        result.Result.Should().BeOfType<OkObjectResult>();
        _resultsServiceMock.Verify(
            s => s.GetMatrixAsync(
                UserId, TenantId, false, distributor, sector, true, from, to,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // --- Attributes ---

    [Fact]
    public void Controller_ShouldHaveExpectedAuthorizeRoles()
    {
        var attr = typeof(ResultsController).GetCustomAttribute<AuthorizeAttribute>();
        attr.Should().NotBeNull();
        attr!.Roles.Should().Be(
            $"{RoleName.Administrator},{RoleName.Requerant},{RoleName.RequerantPreleveur}");
    }

    [Fact]
    public void Controller_ShouldHaveExpectedRouteTemplate()
    {
        var route = typeof(ResultsController).GetCustomAttribute<RouteAttribute>();
        route.Should().NotBeNull();
        route!.Template.Should().Be("api/results");
    }

    [Fact]
    public void Controller_ShouldBeAnApiController()
    {
        typeof(ResultsController)
            .GetCustomAttribute<ApiControllerAttribute>()
            .Should().NotBeNull();
    }

    [Theory]
    [InlineData(nameof(ResultsController.GetRecent), "recent")]
    [InlineData(nameof(ResultsController.GetMatrix), "matrix")]
    public void GetMethods_ShouldHaveExpectedHttpGetAttribute(string methodName, string expectedTemplate)
    {
        var method = typeof(ResultsController).GetMethod(methodName)!;
        var attr = method.GetCustomAttribute<HttpGetAttribute>();
        attr.Should().NotBeNull();
        attr!.Template.Should().Be(expectedTemplate);
    }
}
