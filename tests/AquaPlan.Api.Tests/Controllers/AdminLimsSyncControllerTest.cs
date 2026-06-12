using System.Reflection;
using System.Security.Claims;
using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.LimsSync;
using AquaPlan.Application.DTOs.MockLims;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Tests.Controllers;

public class AdminLimsSyncControllerTest
{
    private readonly Mock<ILimsSyncService> _limsSyncServiceMock = new();
    private readonly Mock<IMockLimsBackfillService> _backfillServiceMock = new();
    private readonly AdminLimsSyncController _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public AdminLimsSyncControllerTest()
    {
        _sut = new AdminLimsSyncController(_limsSyncServiceMock.Object, _backfillServiceMock.Object);
        _sut.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext
            {
                User = new ClaimsPrincipal(new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.NameIdentifier, "admin-1"),
                    new Claim("tenant_id", TenantId.ToString()),
                ], "test")),
            },
        };
    }

    [Fact]
    public async Task GetStatus_ShouldReturnOkWithDto()
    {
        var dto = new LimsSyncStatusDto(true, 5, DateTime.UtcNow, DateTime.UtcNow.AddMinutes(5), 2, 10);
        _limsSyncServiceMock
            .Setup(s => s.GetStatusAsync(TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(dto);

        var result = await _sut.GetStatus(CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(dto);
    }

    [Fact]
    public async Task GetLogs_ShouldPassFiltersAndReturnOk()
    {
        var logs = new List<LimsSyncLogDto>
        {
            new(Guid.NewGuid(), Guid.NewGuid(), null, "PullResults", LimsSyncStatus.Error, "fail",
                DateTime.UtcNow.AddSeconds(-1), DateTime.UtcNow, 1000),
        };
        _limsSyncServiceMock
            .Setup(s => s.GetLogsAsync(TenantId, 25, LimsSyncStatus.Error, It.IsAny<CancellationToken>()))
            .ReturnsAsync(logs);

        var result = await _sut.GetLogs(25, LimsSyncStatus.Error, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeEquivalentTo(logs);
    }

    [Fact]
    public async Task BackfillResults_WhenBodyOmitted_ShouldUseCallerTenantAndDefaultMax()
    {
        var expected = new MockLimsBackfillResultDto(3, 3, 3, 0, Array.Empty<MockLimsBackfillFailureDto>());
        _backfillServiceMock
            .Setup(s => s.BackfillAsync(TenantId, 100, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.BackfillResults(null, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(expected);
        _backfillServiceMock.Verify(
            s => s.BackfillAsync(TenantId, 100, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BackfillResults_WithMaxOrders_ShouldUseCallerTenantAndPassMax()
    {
        // Sprint Sec F-003 — the request body cannot target another tenant anymore:
        // the backfill is always scoped to the caller's tenant_id claim.
        var dto = new MockLimsBackfillRequestDto(MaxOrders: 25);
        var expected = new MockLimsBackfillResultDto(25, 25, 24, 1, new[]
        {
            new MockLimsBackfillFailureDto(Guid.NewGuid(), "ORD-XYZ", "boom"),
        });
        _backfillServiceMock
            .Setup(s => s.BackfillAsync(TenantId, 25, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var result = await _sut.BackfillResults(dto, CancellationToken.None);

        var ok = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        ok.Value.Should().BeSameAs(expected);
        _backfillServiceMock.Verify(
            s => s.BackfillAsync(It.Is<Guid>(t => t != TenantId), It.IsAny<int>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BackfillResults_WhenMaxOrdersIsZeroOrNegative_ShouldClampToOne()
    {
        var dto = new MockLimsBackfillRequestDto(MaxOrders: 0);
        _backfillServiceMock
            .Setup(s => s.BackfillAsync(TenantId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MockLimsBackfillResultDto(0, 0, 0, 0, Array.Empty<MockLimsBackfillFailureDto>()));

        await _sut.BackfillResults(dto, CancellationToken.None);

        _backfillServiceMock.Verify(
            s => s.BackfillAsync(TenantId, 1, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BackfillResults_WhenMaxOrdersExceedsHardCap_ShouldClampTo500()
    {
        var dto = new MockLimsBackfillRequestDto(MaxOrders: 10_000);
        _backfillServiceMock
            .Setup(s => s.BackfillAsync(TenantId, 500, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MockLimsBackfillResultDto(0, 0, 0, 0, Array.Empty<MockLimsBackfillFailureDto>()));

        await _sut.BackfillResults(dto, CancellationToken.None);

        _backfillServiceMock.Verify(
            s => s.BackfillAsync(TenantId, 500, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public void Controller_ShouldRequireAdministratorRole()
    {
        var attr = typeof(AdminLimsSyncController)
            .GetCustomAttribute<AuthorizeAttribute>();
        attr.Should().NotBeNull();
        attr!.Roles.Should().Be(RoleName.Administrator);
    }

    [Fact]
    public void Controller_ShouldHaveExpectedRouteTemplate()
    {
        var route = typeof(AdminLimsSyncController).GetCustomAttribute<RouteAttribute>();
        route.Should().NotBeNull();
        route!.Template.Should().Be("api/admin/lims-sync");
    }

    [Theory]
    [InlineData(nameof(AdminLimsSyncController.GetStatus), "status")]
    [InlineData(nameof(AdminLimsSyncController.GetLogs), "logs")]
    public void Methods_ShouldHaveExpectedHttpGetAttribute(string methodName, string expectedTemplate)
    {
        var method = typeof(AdminLimsSyncController).GetMethod(methodName)!;
        var attr = method.GetCustomAttribute<HttpGetAttribute>();
        attr.Should().NotBeNull();
        attr!.Template.Should().Be(expectedTemplate);
    }

    [Fact]
    public void BackfillResults_ShouldHaveHttpPostAttributeWithBackfillRoute()
    {
        var method = typeof(AdminLimsSyncController).GetMethod(nameof(AdminLimsSyncController.BackfillResults))!;
        var attr = method.GetCustomAttribute<HttpPostAttribute>();
        attr.Should().NotBeNull();
        attr!.Template.Should().Be("backfill-results");
    }
}
