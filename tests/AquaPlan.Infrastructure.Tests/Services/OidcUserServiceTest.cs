using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Logging;
using MockQueryable;

namespace AquaPlan.Infrastructure.Tests.Services;

public class OidcUserServiceTest
{
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<ILogger<OidcUserService>> _loggerMock = new();
    private readonly OidcUserService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public OidcUserServiceTest()
    {
        var store = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        _sut = new OidcUserService(_userManagerMock.Object, _loggerMock.Object);
    }

    [Fact]
    public async Task FindByExternalIdAsync_ShouldReturnUser_WhenExists()
    {
        var user = new AppUser { ExternalId = "ext-123", Email = "test@aquaplan.ch" };
        var users = new List<AppUser> { user }.BuildMock();
        _userManagerMock.Setup(x => x.Users).Returns(users);

        var result = await _sut.FindByExternalIdAsync("ext-123");

        result.Should().NotBeNull();
        result!.ExternalId.Should().Be("ext-123");
    }

    [Fact]
    public async Task FindByExternalIdAsync_ShouldReturnNull_WhenNotExists()
    {
        var users = new List<AppUser>().BuildMock();
        _userManagerMock.Setup(x => x.Users).Returns(users);

        var result = await _sut.FindByExternalIdAsync("nonexistent");

        result.Should().BeNull();
    }

    [Fact]
    public async Task FindOrCreateFromExternalLoginAsync_ShouldReturnExistingUser_WhenExternalIdMatches()
    {
        var existingUser = new AppUser { ExternalId = "ext-123", Email = "existing@aquaplan.ch" };
        var users = new List<AppUser> { existingUser }.BuildMock();
        _userManagerMock.Setup(x => x.Users).Returns(users);

        var result = await _sut.FindOrCreateFromExternalLoginAsync(
            "ext-123", "existing@aquaplan.ch", "John", "Doe", TenantId);

        result.Should().Be(existingUser);
        _userManagerMock.Verify(x => x.CreateAsync(It.IsAny<AppUser>()), Times.Never);
    }

    [Fact]
    public async Task FindOrCreateFromExternalLoginAsync_ShouldLinkExternalId_WhenEmailUserExists()
    {
        var users = new List<AppUser>().BuildMock();
        _userManagerMock.Setup(x => x.Users).Returns(users);

        var emailUser = new AppUser { Email = "john@aquaplan.ch", FirstName = "John", LastName = "Doe" };
        _userManagerMock.Setup(x => x.FindByEmailAsync("john@aquaplan.ch"))
            .ReturnsAsync(emailUser);
        _userManagerMock.Setup(x => x.UpdateAsync(It.IsAny<AppUser>()))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _sut.FindOrCreateFromExternalLoginAsync(
            "ext-456", "john@aquaplan.ch", "John", "Doe", TenantId);

        result.Should().Be(emailUser);
        result.ExternalId.Should().Be("ext-456");
        _userManagerMock.Verify(x => x.UpdateAsync(emailUser), Times.Once);
    }

    [Fact]
    public async Task FindOrCreateFromExternalLoginAsync_ShouldCreateNewUser_WhenNoMatchFound()
    {
        var users = new List<AppUser>().BuildMock();
        _userManagerMock.Setup(x => x.Users).Returns(users);
        _userManagerMock.Setup(x => x.FindByEmailAsync("new@aquaplan.ch"))
            .ReturnsAsync((AppUser?)null);
        _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<AppUser>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock.Setup(x => x.AddToRoleAsync(It.IsAny<AppUser>(), RoleName.Requerant))
            .ReturnsAsync(IdentityResult.Success);

        var result = await _sut.FindOrCreateFromExternalLoginAsync(
            "ext-789", "new@aquaplan.ch", "Jane", "Smith", TenantId);

        result.Should().NotBeNull();
        result.Email.Should().Be("new@aquaplan.ch");
        result.FirstName.Should().Be("Jane");
        result.LastName.Should().Be("Smith");
        result.ExternalId.Should().Be("ext-789");
        result.TenantId.Should().Be(TenantId);
        _userManagerMock.Verify(x => x.CreateAsync(It.IsAny<AppUser>()), Times.Once);
        _userManagerMock.Verify(x => x.AddToRoleAsync(It.IsAny<AppUser>(), RoleName.Requerant), Times.Once);
    }

    [Fact]
    public async Task FindOrCreateFromExternalLoginAsync_ShouldThrow_WhenCreateFails()
    {
        var users = new List<AppUser>().BuildMock();
        _userManagerMock.Setup(x => x.Users).Returns(users);
        _userManagerMock.Setup(x => x.FindByEmailAsync(It.IsAny<string>()))
            .ReturnsAsync((AppUser?)null);
        _userManagerMock.Setup(x => x.CreateAsync(It.IsAny<AppUser>()))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "Duplicate email" }));

        await _sut.Awaiting(x => x.FindOrCreateFromExternalLoginAsync(
                "ext-fail", "fail@aquaplan.ch", "Fail", "User", TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Duplicate email*");
    }
}
