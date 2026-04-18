using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.SamplingRounds;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System.Security.Claims;

namespace AquaPlan.Api.Tests.Controllers;

public class SamplingRoundsControllerTest
{
    private readonly Mock<ISamplingRoundService> _samplingRoundServiceMock = new();
    private readonly Mock<IPermissionService> _permissionServiceMock = new();
    private readonly Mock<IDelegationService> _delegationServiceMock = new();
    private readonly Mock<ILogger<SamplingRoundsController>> _loggerMock = new();
    private readonly SamplingRoundsController _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid RoundId = Guid.Parse("00000000-0000-0000-0000-000000000050");
    private static readonly Guid DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private const string UserId = "user-1";
    private const string PreleveurId = "preleveur-1";

    public SamplingRoundsControllerTest()
    {
        _sut = new SamplingRoundsController(
            _samplingRoundServiceMock.Object,
            _permissionServiceMock.Object,
            _delegationServiceMock.Object,
            _loggerMock.Object);
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

    private static SamplingRoundDetailDto CreateRoundDetailDto(
        SamplingRoundStatus status = SamplingRoundStatus.Draft,
        bool isLocked = false, string? lockedById = null, string? lockedByName = null, DateTime? lockedAt = null)
    {
        return new SamplingRoundDetailDto(
            RoundId, "Round 1", "Test round", null, status,
            PreleveurId, "Pierre Martin",
            DistributorId, "Test Distributor", "TD",
            null, UserId, "John Doe",
            DateTime.UtcNow, null, null,
            [],
            [],
            isLocked, lockedById, lockedByName, lockedAt);
    }

    #region Controller attributes

    [Fact]
    public void Controller_ShouldHaveAuthorizeAttribute()
    {
        var attributes = typeof(SamplingRoundsController).GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveApiControllerAttribute()
    {
        var attributes = typeof(SamplingRoundsController).GetCustomAttributes(typeof(ApiControllerAttribute), true);
        attributes.Should().NotBeEmpty();
    }

    [Fact]
    public void Controller_ShouldHaveRouteAttribute()
    {
        var attributes = typeof(SamplingRoundsController).GetCustomAttributes(typeof(RouteAttribute), true);
        attributes.Should().NotBeEmpty();
        var routeAttr = attributes.OfType<RouteAttribute>().First();
        routeAttr.Template.Should().Be("api/sampling-rounds");
    }

    #endregion

    #region GetRounds multi-status filter

    [Fact]
    public void GetRounds_ShouldAcceptStatusesArrayFromQuery()
    {
        // AQ-362 — the endpoint must bind the repeated statuses=A&statuses=B
        // query pattern emitted by the dashboard.
        var method = typeof(SamplingRoundsController).GetMethod(nameof(SamplingRoundsController.GetRounds));
        method.Should().NotBeNull();

        var statusesParam = method!.GetParameters()
            .FirstOrDefault(p => p.Name == "statuses");
        statusesParam.Should().NotBeNull("GetRounds must accept a 'statuses' query parameter");

        statusesParam!.ParameterType.Should().Be(typeof(SamplingRoundStatus[]));

        var fromQuery = statusesParam.GetCustomAttributes(typeof(FromQueryAttribute), true)
            .OfType<FromQueryAttribute>()
            .FirstOrDefault();
        fromQuery.Should().NotBeNull();
        fromQuery!.Name.Should().Be("statuses");
    }

    [Fact]
    public void GetRounds_ShouldHaveHttpGetAttribute()
    {
        var method = typeof(SamplingRoundsController).GetMethod(nameof(SamplingRoundsController.GetRounds));
        method!.GetCustomAttributes(typeof(HttpGetAttribute), true).Should().NotBeEmpty();
    }

    #endregion

    #region RevertToDraft

    [Fact]
    public void RevertToDraft_ShouldHaveAdministratorRoleAttribute()
    {
        var method = typeof(SamplingRoundsController).GetMethod(nameof(SamplingRoundsController.RevertToDraft));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be("Administrator");
    }

    [Fact]
    public void RevertToDraft_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(SamplingRoundsController).GetMethod(nameof(SamplingRoundsController.RevertToDraft));
        var postAttr = method!.GetCustomAttributes(typeof(HttpPostAttribute), true).OfType<HttpPostAttribute>().First();
        postAttr.Template.Should().Be("{id:guid}/revert-to-draft");
    }

    [Fact]
    public async Task RevertToDraft_ShouldReturnOk_WhenRoundExists()
    {
        var reverted = CreateRoundDetailDto(SamplingRoundStatus.Draft);
        _samplingRoundServiceMock
            .Setup(x => x.RevertToDraftAsync(RoundId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(reverted);

        var result = await _sut.RevertToDraft(RoundId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(reverted);
    }

    [Fact]
    public async Task RevertToDraft_ShouldReturnNotFound_WhenRoundDoesNotExist()
    {
        _samplingRoundServiceMock
            .Setup(x => x.RevertToDraftAsync(RoundId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SamplingRoundDetailDto?)null);

        var result = await _sut.RevertToDraft(RoundId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region AssignPreleveur

    [Fact]
    public void AssignPreleveur_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(SamplingRoundsController).GetMethod(nameof(SamplingRoundsController.AssignPreleveur));
        var postAttr = method!.GetCustomAttributes(typeof(HttpPostAttribute), true).OfType<HttpPostAttribute>().First();
        postAttr.Template.Should().Be("{id:guid}/assign");
    }

    #endregion

    #region CreateRound

    [Fact]
    public void CreateRound_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(SamplingRoundsController).GetMethod(nameof(SamplingRoundsController.CreateRound));
        method!.GetCustomAttributes(typeof(HttpPostAttribute), true).Should().NotBeEmpty();
    }

    [Fact]
    public void CreateRound_ShouldHaveAuthorizeAttributeWithCreatorRoles()
    {
        var method = typeof(SamplingRoundsController).GetMethod(nameof(SamplingRoundsController.CreateRound));
        var auth = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true).OfType<AuthorizeAttribute>().FirstOrDefault();
        auth.Should().NotBeNull();
        auth!.Roles.Should()
            .Contain(RoleName.Administrator)
            .And.Contain(RoleName.Requerant)
            .And.Contain(RoleName.RequerantPreleveur);
        auth.Roles.Should().NotContain(RoleName.Preleveur + ",");
    }

    [Fact]
    public void AddOrder_ShouldHaveAuthorizeAttributeWithCreatorRoles()
    {
        var method = typeof(SamplingRoundsController).GetMethod(nameof(SamplingRoundsController.AddOrder));
        var auth = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true).OfType<AuthorizeAttribute>().FirstOrDefault();
        auth.Should().NotBeNull();
        auth!.Roles.Should()
            .Contain(RoleName.Administrator)
            .And.Contain(RoleName.Requerant)
            .And.Contain(RoleName.RequerantPreleveur);
    }

    #endregion

    #region TransmitAll

    [Fact]
    public void TransmitAll_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(SamplingRoundsController).GetMethod(nameof(SamplingRoundsController.TransmitAll));
        var postAttr = method!.GetCustomAttributes(typeof(HttpPostAttribute), true).OfType<HttpPostAttribute>().First();
        postAttr.Template.Should().Be("{id:guid}/transmit-all");
    }

    #endregion

    #region StartRound (AQ-370)

    [Fact]
    public void StartRound_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(SamplingRoundsController).GetMethod(nameof(SamplingRoundsController.StartRound));
        var postAttr = method!.GetCustomAttributes(typeof(HttpPostAttribute), true).OfType<HttpPostAttribute>().First();
        postAttr.Template.Should().Be("{id:guid}/start");
    }

    [Fact]
    public void StartRound_ShouldHaveAuthorizeAttributeWithPreleveurRoles()
    {
        var method = typeof(SamplingRoundsController).GetMethod(nameof(SamplingRoundsController.StartRound));
        var auth = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true).OfType<AuthorizeAttribute>().FirstOrDefault();
        auth.Should().NotBeNull();
        auth!.Roles.Should()
            .Contain(RoleName.Administrator)
            .And.Contain(RoleName.Preleveur)
            .And.Contain(RoleName.RequerantPreleveur);
    }

    [Fact]
    public async Task StartRound_ShouldReturnOk_WhenSuccess()
    {
        var started = CreateRoundDetailDto(SamplingRoundStatus.InProgress,
            isLocked: true, lockedById: PreleveurId, lockedByName: "Pierre Martin",
            lockedAt: DateTime.UtcNow);
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _samplingRoundServiceMock
            .Setup(x => x.StartAsync(RoundId, UserId, TenantId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync(started);

        var result = await _sut.StartRound(RoundId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(started);
    }

    [Fact]
    public async Task StartRound_ShouldReturnNotFound_WhenRoundDoesNotExist()
    {
        _permissionServiceMock
            .Setup(x => x.UserHasPermissionAsync(UserId, "ViewAllOrders", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);
        _samplingRoundServiceMock
            .Setup(x => x.StartAsync(RoundId, UserId, TenantId, false, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SamplingRoundDetailDto?)null);

        var result = await _sut.StartRound(RoundId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion

    #region ForceUnlock (AQ-372)

    [Fact]
    public void ForceUnlock_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(SamplingRoundsController).GetMethod(nameof(SamplingRoundsController.ForceUnlock));
        var postAttr = method!.GetCustomAttributes(typeof(HttpPostAttribute), true).OfType<HttpPostAttribute>().First();
        postAttr.Template.Should().Be("{id:guid}/force-unlock");
    }

    [Fact]
    public void ForceUnlock_ShouldHaveAuthorizeAttributeWithAdministrator()
    {
        var method = typeof(SamplingRoundsController).GetMethod(nameof(SamplingRoundsController.ForceUnlock));
        var attributes = method!.GetCustomAttributes(typeof(AuthorizeAttribute), true);
        attributes.Should().NotBeEmpty();
        var authorizeAttr = attributes.OfType<AuthorizeAttribute>().First();
        authorizeAttr.Roles.Should().Be(RoleName.Administrator);
    }

    [Fact]
    public async Task ForceUnlock_ShouldReturnOk_WhenSuccess()
    {
        var unlocked = CreateRoundDetailDto(SamplingRoundStatus.Assigned);
        _samplingRoundServiceMock
            .Setup(x => x.ForceUnlockAsync(RoundId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(unlocked);

        var result = await _sut.ForceUnlock(RoundId, CancellationToken.None);

        var okResult = result.Result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(unlocked);
    }

    [Fact]
    public async Task ForceUnlock_ShouldReturnNotFound_WhenRoundDoesNotExist()
    {
        _samplingRoundServiceMock
            .Setup(x => x.ForceUnlockAsync(RoundId, UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((SamplingRoundDetailDto?)null);

        var result = await _sut.ForceUnlock(RoundId, CancellationToken.None);

        result.Result.Should().BeOfType<NotFoundResult>();
    }

    #endregion
}
