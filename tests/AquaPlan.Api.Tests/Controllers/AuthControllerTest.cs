using AquaPlan.Api.Controllers.Api;
using AquaPlan.Application.DTOs.Auth;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Api.Tests.Controllers;

public class AuthControllerTest
{
    private readonly Mock<IAuthService> _authServiceMock = new();
    private readonly Mock<ILogger<AuthController>> _loggerMock = new();
    private readonly AuthController _sut;

    public AuthControllerTest()
    {
        _sut = new AuthController(_authServiceMock.Object, _loggerMock.Object);
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
    public void Login_ShouldHaveAllowAnonymousAttribute()
    {
        var method = typeof(AuthController).GetMethod(nameof(AuthController.Login));
        var attribute = method!.GetCustomAttributes(typeof(Microsoft.AspNetCore.Authorization.AllowAnonymousAttribute), true);
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
}
