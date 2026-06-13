using AquaPlan.Application.DTOs.Auth;
using AquaPlan.Application.Services;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MockQueryable;

namespace AquaPlan.Application.Tests.Services;

public class AuthServiceTest
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<SignInManager<AppUser>> _signInManagerMock;
    private readonly Mock<ITokenService> _tokenServiceMock = new();
    private readonly Mock<ILogger<AuthService>> _loggerMock = new();
    private readonly IConfiguration _configuration;
    private readonly AuthService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public AuthServiceTest()
    {
        var store = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _signInManagerMock = new Mock<SignInManager<AppUser>>(
            _userManagerMock.Object,
            Mock.Of<IHttpContextAccessor>(),
            Mock.Of<IUserClaimsPrincipalFactory<AppUser>>(),
            Options.Create(new IdentityOptions()),
            Mock.Of<ILogger<SignInManager<AppUser>>>(),
            Mock.Of<Microsoft.AspNetCore.Authentication.IAuthenticationSchemeProvider>(),
            Mock.Of<IUserConfirmation<AppUser>>());

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:AccessTokenExpirationMinutes"] = "60",
                ["Jwt:RefreshTokenExpirationDays"] = "7",
            })
            .Build();

        _tokenServiceMock.Setup(x => x.GenerateAccessToken(It.IsAny<AppUser>(), It.IsAny<IList<string>>()))
            .Returns("test-access-token");
        _tokenServiceMock.Setup(x => x.GenerateRefreshToken())
            .Returns("test-refresh-token");

        _userManagerMock.Setup(x => x.UpdateAsync(It.IsAny<AppUser>()))
            .ReturnsAsync(IdentityResult.Success);

        _sut = new AuthService(_userManagerMock.Object, _signInManagerMock.Object, _tokenServiceMock.Object, _configuration, _loggerMock.Object);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnTokens_WhenCredentialsAreValid()
    {
        var user = CreateTestUser();
        var loginDto = new LoginDto("admin@aquaplan.ch", "Password123");
        _userManagerMock.Setup(x => x.FindByEmailAsync(loginDto.Email)).ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, loginDto.Password, true))
            .ReturnsAsync(SignInResult.Success);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });

        var result = await _sut.LoginAsync(loginDto);

        result.Should().NotBeNull();
        result!.AccessToken.Should().Be("test-access-token");
        result.RefreshToken.Should().Be("test-refresh-token");
        result.ExpiresIn.Should().Be(3600);
    }

    [Fact]
    public async Task LoginAsync_ShouldStoreRefreshTokenOnUser()
    {
        var user = CreateTestUser();
        var loginDto = new LoginDto("admin@aquaplan.ch", "Password123");
        _userManagerMock.Setup(x => x.FindByEmailAsync(loginDto.Email)).ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, loginDto.Password, true))
            .ReturnsAsync(SignInResult.Success);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });

        await _sut.LoginAsync(loginDto);

        user.RefreshToken.Should().Be("test-refresh-token");
        user.RefreshTokenExpiryTime.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
        _userManagerMock.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnNull_WhenUserNotFound()
    {
        var loginDto = new LoginDto("unknown@aquaplan.ch", "Password123");
        _userManagerMock.Setup(x => x.FindByEmailAsync(loginDto.Email)).ReturnsAsync((AppUser?)null);

        var result = await _sut.LoginAsync(loginDto);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnNull_WhenUserIsInactive()
    {
        var user = CreateTestUser();
        user.IsActive = false;
        var loginDto = new LoginDto("admin@aquaplan.ch", "Password123");
        _userManagerMock.Setup(x => x.FindByEmailAsync(loginDto.Email)).ReturnsAsync(user);

        var result = await _sut.LoginAsync(loginDto);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnNull_WhenPasswordIsInvalid()
    {
        var user = CreateTestUser();
        var loginDto = new LoginDto("admin@aquaplan.ch", "WrongPassword");
        _userManagerMock.Setup(x => x.FindByEmailAsync(loginDto.Email)).ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, loginDto.Password, true))
            .ReturnsAsync(SignInResult.Failed);

        var result = await _sut.LoginAsync(loginDto);

        result.Should().BeNull();
    }

    [Fact]
    public async Task LoginAsync_ShouldReturnNull_WhenAccountIsLockedOut()
    {
        // F-111 — once the account is locked out, login is refused even before the
        // password is (re)checked successfully.
        var user = CreateTestUser();
        var loginDto = new LoginDto("admin@aquaplan.ch", "Password123");
        _userManagerMock.Setup(x => x.FindByEmailAsync(loginDto.Email)).ReturnsAsync(user);
        _signInManagerMock.Setup(x => x.CheckPasswordSignInAsync(user, loginDto.Password, true))
            .ReturnsAsync(SignInResult.LockedOut);

        var result = await _sut.LoginAsync(loginDto);

        result.Should().BeNull();
        _tokenServiceMock.Verify(x => x.GenerateAccessToken(It.IsAny<AppUser>(), It.IsAny<IList<string>>()), Times.Never);
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldReturnNewTokens_WhenRefreshTokenIsValid()
    {
        var user = CreateTestUser();
        user.RefreshToken = "valid-refresh-token";
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1);
        var users = new List<AppUser> { user }.BuildMock();
        _userManagerMock.Setup(x => x.Users).Returns(users);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });

        var result = await _sut.RefreshTokenAsync("valid-refresh-token");

        result.Should().NotBeNull();
        result!.AccessToken.Should().Be("test-access-token");
        result.RefreshToken.Should().Be("test-refresh-token");
        result.ExpiresIn.Should().Be(3600);
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldRotateRefreshToken()
    {
        var user = CreateTestUser();
        user.RefreshToken = "old-refresh-token";
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1);
        var users = new List<AppUser> { user }.BuildMock();
        _userManagerMock.Setup(x => x.Users).Returns(users);
        _userManagerMock.Setup(x => x.GetRolesAsync(user)).ReturnsAsync(new List<string> { "Admin" });

        await _sut.RefreshTokenAsync("old-refresh-token");

        user.RefreshToken.Should().Be("test-refresh-token");
        user.RefreshTokenExpiryTime.Should().BeCloseTo(DateTime.UtcNow.AddDays(7), TimeSpan.FromSeconds(5));
        _userManagerMock.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldReturnNull_WhenTokenNotFound()
    {
        var users = new List<AppUser>().BuildMock();
        _userManagerMock.Setup(x => x.Users).Returns(users);

        var result = await _sut.RefreshTokenAsync("nonexistent-token");

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldReturnNull_WhenTokenIsExpired()
    {
        var user = CreateTestUser();
        user.RefreshToken = "expired-refresh-token";
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(-1);
        var users = new List<AppUser> { user }.BuildMock();
        _userManagerMock.Setup(x => x.Users).Returns(users);

        var result = await _sut.RefreshTokenAsync("expired-refresh-token");

        result.Should().BeNull();
    }

    [Fact]
    public async Task RefreshTokenAsync_ShouldReturnNull_WhenUserIsInactive()
    {
        var user = CreateTestUser();
        user.IsActive = false;
        user.RefreshToken = "valid-refresh-token";
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1);
        var users = new List<AppUser> { user }.BuildMock();
        _userManagerMock.Setup(x => x.Users).Returns(users);

        var result = await _sut.RefreshTokenAsync("valid-refresh-token");

        result.Should().BeNull();
    }

    [Fact]
    public async Task LogoutAsync_ShouldClearRefreshToken()
    {
        var user = CreateTestUser();
        user.RefreshToken = "some-token";
        user.RefreshTokenExpiryTime = DateTime.UtcNow.AddDays(1);
        _userManagerMock.Setup(x => x.FindByIdAsync(user.Id)).ReturnsAsync(user);

        await _sut.LogoutAsync(user.Id);

        user.RefreshToken.Should().BeNull();
        user.RefreshTokenExpiryTime.Should().BeNull();
        _userManagerMock.Verify(x => x.UpdateAsync(user), Times.Once);
    }

    [Fact]
    public async Task LogoutAsync_ShouldNotThrow_WhenUserNotFound()
    {
        _userManagerMock.Setup(x => x.FindByIdAsync("unknown-id")).ReturnsAsync((AppUser?)null);

        await _sut.Invoking(x => x.LogoutAsync("unknown-id")).Should().NotThrowAsync();
    }

    private static AppUser CreateTestUser()
    {
        return new AppUser
        {
            Id = "user-1",
            Email = "admin@aquaplan.ch",
            FirstName = "Admin",
            LastName = "User",
            Organization = "Canton de Fribourg",
            TenantId = TenantId,
            IsActive = true,
        };
    }
}
