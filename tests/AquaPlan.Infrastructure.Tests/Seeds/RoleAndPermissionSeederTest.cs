using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data.Seeds;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Seeds;

/// <summary>
/// Sprint Sec F-002 — the initial production admin is created from the
/// INITIAL_ADMIN_EMAIL / INITIAL_ADMIN_PASSWORD environment variables (one-shot),
/// never from the hardcoded dev credentials.
/// </summary>
public class RoleAndPermissionSeederTest
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<IConfiguration> _configurationMock = new();
    private readonly Mock<ILogger> _loggerMock = new();

    public RoleAndPermissionSeederTest()
    {
        var store = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
    }

    private void SetupConfig(string? email, string? password)
    {
        _configurationMock
            .Setup(c => c[RoleAndPermissionSeeder.InitialAdminEmailKey])
            .Returns(email);
        _configurationMock
            .Setup(c => c[RoleAndPermissionSeeder.InitialAdminPasswordKey])
            .Returns(password);
    }

    [Fact]
    public async Task SeedInitialAdminFromConfiguration_WhenBothVariablesSetAndUserMissing_ShouldCreateAdministrator()
    {
        SetupConfig("ops-admin@fr.ch", "A-Very-Strong-Initial-Password-2026!");
        _userManagerMock
            .Setup(m => m.FindByEmailAsync("ops-admin@fr.ch"))
            .ReturnsAsync((AppUser?)null);
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<AppUser>(), "A-Very-Strong-Initial-Password-2026!"))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock
            .Setup(m => m.AddToRoleAsync(It.IsAny<AppUser>(), RoleName.Administrator))
            .ReturnsAsync(IdentityResult.Success);

        await RoleAndPermissionSeeder.SeedInitialAdminFromConfigurationAsync(
            _userManagerMock.Object, _configurationMock.Object, _loggerMock.Object);

        _userManagerMock.Verify(
            m => m.CreateAsync(
                It.Is<AppUser>(u => u.Email == "ops-admin@fr.ch" && u.EmailConfirmed),
                "A-Very-Strong-Initial-Password-2026!"),
            Times.Once);
        _userManagerMock.Verify(
            m => m.AddToRoleAsync(It.Is<AppUser>(u => u.Email == "ops-admin@fr.ch"), RoleName.Administrator),
            Times.Once);
    }

    [Fact]
    public async Task SeedInitialAdminFromConfiguration_WhenUserAlreadyExists_ShouldNotCreate()
    {
        SetupConfig("ops-admin@fr.ch", "A-Very-Strong-Initial-Password-2026!");
        _userManagerMock
            .Setup(m => m.FindByEmailAsync("ops-admin@fr.ch"))
            .ReturnsAsync(new AppUser { Email = "ops-admin@fr.ch" });

        await RoleAndPermissionSeeder.SeedInitialAdminFromConfigurationAsync(
            _userManagerMock.Object, _configurationMock.Object, _loggerMock.Object);

        _userManagerMock.Verify(
            m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Theory]
    [InlineData(null, null)]
    [InlineData("ops-admin@fr.ch", null)]
    [InlineData(null, "A-Very-Strong-Initial-Password-2026!")]
    [InlineData("", "")]
    public async Task SeedInitialAdminFromConfiguration_WhenVariablesMissing_ShouldNotCreate(string? email, string? password)
    {
        SetupConfig(email, password);
        _userManagerMock
            .Setup(m => m.GetUsersInRoleAsync(RoleName.Administrator))
            .ReturnsAsync(new List<AppUser>());

        await RoleAndPermissionSeeder.SeedInitialAdminFromConfigurationAsync(
            _userManagerMock.Object, _configurationMock.Object, _loggerMock.Object);

        _userManagerMock.Verify(
            m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task SeedInitialAdminFromConfiguration_WhenVariablesMissingAndNoAdminExists_ShouldLogWarning()
    {
        SetupConfig(null, null);
        _userManagerMock
            .Setup(m => m.GetUsersInRoleAsync(RoleName.Administrator))
            .ReturnsAsync(new List<AppUser>());

        await RoleAndPermissionSeeder.SeedInitialAdminFromConfigurationAsync(
            _userManagerMock.Object, _configurationMock.Object, _loggerMock.Object);

        _loggerMock.Verify(
            l => l.Log(
                LogLevel.Warning,
                It.IsAny<EventId>(),
                It.IsAny<It.IsAnyType>(),
                It.IsAny<Exception?>(),
                It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
            Times.Once);
    }
}
