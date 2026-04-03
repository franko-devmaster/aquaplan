using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.Auth;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Api.Tests.Controllers;

public class AuthControllerTest
{
    private readonly Mock<IAuthService> _authServiceMock = new();
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<IOidcUserService> _oidcUserServiceMock = new();
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<ILogger<AuthController>> _loggerMock = new();
    private readonly IConfiguration _configuration;
    private readonly AuthController _sut;

    public AuthControllerTest()
    {
        var store = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Oidc:Enabled"] = "false",
                ["Oidc:DefaultTenantId"] = "00000000-0000-0000-0000-000000000001",
            })
            .Build();

        _sut = new AuthController(
            _authServiceMock.Object,
            _tokenServiceMock.Object,
            _oidcUserServiceMock.Object,
            _userManagerMock.Object,
            _configuration,
            _loggerMock.Object);
    }

    [Fact]
    public async Task Login_ShouldReturnOk_WhenCredentialsAreValid()
    {
        var loginDto = new LoginDto("admin@aquaplan.ch", "Password123");
        var response = new LoginResponseDto("jwt-token", "refresh-token", 3600);
        _authServiceMock.Setup(x => x.LoginAsync(loginDto, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _sut.Login(loginDto, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(response);
    }

    [Fact]
    public async Task Login_ShouldReturnUnauthorized_WhenCredentialsAreInvalid()
    {
        var loginDto = new LoginDto("bad@email.com", "wrong");
        _authServiceMock.Setup(x => x.LoginAsync(loginDto, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoginResponseDto?)null);

        var result = await _sut.Login(loginDto, CancellationToken.None);

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task Refresh_ShouldReturnOk_WhenRefreshTokenIsValid()
    {
        var refreshDto = new RefreshTokenDto("valid-refresh-token");
        var response = new LoginResponseDto("new-jwt-token", "new-refresh-token", 3600);
        _authServiceMock.Setup(x => x.RefreshTokenAsync(refreshDto.RefreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync(response);

        var result = await _sut.Refresh(refreshDto, CancellationToken.None);

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        okResult.Value.Should().Be(response);
    }

    [Fact]
    public async Task Refresh_ShouldReturnUnauthorized_WhenRefreshTokenIsInvalid()
    {
        var refreshDto = new RefreshTokenDto("invalid-refresh-token");
        _authServiceMock.Setup(x => x.RefreshTokenAsync(refreshDto.RefreshToken, It.IsAny<CancellationToken>()))
            .ReturnsAsync((LoginResponseDto?)null);

        var result = await _sut.Refresh(refreshDto, CancellationToken.None);

        var unauthorizedResult = result.Should().BeOfType<UnauthorizedObjectResult>().Subject;
        var problemDetails = unauthorizedResult.Value.Should().BeOfType<ProblemDetails>().Subject;
        problemDetails.Detail.Should().Be("Invalid or expired refresh token");
    }

    [Fact]
    public void Login_ShouldHaveAllowAnonymousAttribute()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Login));
        var attribute = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true);
        attribute.Should().NotBeEmpty();
    }

    [Fact]
    public void Refresh_ShouldHaveAllowAnonymousAttribute()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Refresh));
        var attribute = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true);
        attribute.Should().NotBeEmpty();
    }

    [Fact]
    public void Refresh_ShouldHaveHttpPostAttribute()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Refresh));
        var attribute = method!.GetCustomAttributes(typeof(HttpPostAttribute), true);
        attribute.Should().NotBeEmpty();
    }

    [Fact]
    public void Me_ShouldHaveAuthorizeAttribute()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Me));
        var attribute = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
        attribute.Should().NotBeEmpty();
    }

    [Fact]
    public void Logout_ShouldHaveAuthorizeAttribute()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Logout));
        var attribute = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AuthorizeAttribute), true);
        attribute.Should().NotBeEmpty();
    }

    [Fact]
    public void OidcLogin_ShouldReturnBadRequest_WhenOidcDisabled()
    {
        var result = _sut.OidcLogin();

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public void OidcLogin_ShouldReturnChallenge_WhenOidcEnabled()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Oidc:Enabled"] = "true",
            })
            .Build();

        var controller = new AuthController(
            _authServiceMock.Object,
            _tokenServiceMock.Object,
            _oidcUserServiceMock.Object,
            _userManagerMock.Object,
            config,
            _loggerMock.Object);

        var result = controller.OidcLogin();

        result.Should().BeOfType<ChallengeResult>();
        var challengeResult = (ChallengeResult)result;
        challengeResult.AuthenticationSchemes.Should().Contain("oidc");
    }

    [Fact]
    public void OidcConfig_ShouldReturnEnabledFalse_WhenOidcDisabled()
    {
        var result = _sut.OidcConfig();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        value.Should().BeEquivalentTo(new { enabled = false });
    }

    [Fact]
    public void OidcConfig_ShouldReturnEnabledTrue_WhenOidcEnabled()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Oidc:Enabled"] = "true",
            })
            .Build();

        var controller = new AuthController(
            _authServiceMock.Object,
            _tokenServiceMock.Object,
            _oidcUserServiceMock.Object,
            _userManagerMock.Object,
            config,
            _loggerMock.Object);

        var result = controller.OidcConfig();

        var okResult = result.Should().BeOfType<OkObjectResult>().Subject;
        var value = okResult.Value;
        value.Should().BeEquivalentTo(new { enabled = true });
    }

    [Fact]
    public void OidcLogin_ShouldHaveAllowAnonymousAttribute()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.OidcLogin));
        var attribute = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true);
        attribute.Should().NotBeEmpty();
    }

    [Fact]
    public void OidcCallback_ShouldHaveAllowAnonymousAttribute()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.OidcCallback));
        var attribute = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true);
        attribute.Should().NotBeEmpty();
    }

    [Fact]
    public void OidcConfig_ShouldHaveAllowAnonymousAttribute()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.OidcConfig));
        var attribute = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true);
        attribute.Should().NotBeEmpty();
    }
}
