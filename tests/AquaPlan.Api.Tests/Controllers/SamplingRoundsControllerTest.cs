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
        SamplingRoundStatus status = SamplingRoundStatus.Draft)
    {
        return new SamplingRoundDetailDto(
            RoundId, "Round 1", "Test round", null, status,
            PreleveurId, "Pierre Martin",
            DistributorId, "Test Distributor", "TD",
            null, UserId, "John Doe",
            DateTime.UtcNow, null, null,
            []);
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
}
