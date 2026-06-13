using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Services;

/// <summary>
/// Sprint Robustesse F-103 — role assignment/removal is tenant-scoped and validates the role
/// name, so a tenant-A administrator can neither modify a tenant-B user's roles nor assign an
/// unknown role.
/// </summary>
public class PermissionServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<RoleManager<ApplicationRole>> _roleManagerMock;
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<ILogger<PermissionService>> _loggerMock = new();
    private readonly PermissionService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public PermissionServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AquaPlanDbContext(options);

        var userStore = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            userStore.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        var roleStore = new Mock<IRoleStore<ApplicationRole>>();
        _roleManagerMock = new Mock<RoleManager<ApplicationRole>>(
            roleStore.Object, null!, null!, null!, null!);

        _sut = new PermissionService(
            _roleManagerMock.Object, _userManagerMock.Object, _dbContext, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task AssignRoleToUserAsync_ShouldAssign_WhenUserInCallerTenantAndRoleKnown()
    {
        var user = new AppUser { Id = "u1", TenantId = TenantId };
        _userManagerMock.Setup(m => m.FindByIdAsync("u1")).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.IsInRoleAsync(user, RoleName.Requerant)).ReturnsAsync(false);
        _userManagerMock.Setup(m => m.AddToRoleAsync(user, RoleName.Requerant)).ReturnsAsync(IdentityResult.Success);

        var result = await _sut.AssignRoleToUserAsync("u1", RoleName.Requerant, TenantId);

        result.Should().BeTrue();
        _userManagerMock.Verify(m => m.AddToRoleAsync(user, RoleName.Requerant), Times.Once);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_ShouldRefuse_WhenUserInOtherTenant()
    {
        // F-103 — cross-tenant role grant is rejected without touching the role membership.
        var user = new AppUser { Id = "u2", TenantId = OtherTenantId };
        _userManagerMock.Setup(m => m.FindByIdAsync("u2")).ReturnsAsync(user);

        var result = await _sut.AssignRoleToUserAsync("u2", RoleName.Administrator, TenantId);

        result.Should().BeFalse();
        _userManagerMock.Verify(m => m.AddToRoleAsync(It.IsAny<AppUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_ShouldRefuse_WhenRoleUnknown()
    {
        // F-103 — an unknown role name is rejected before any lookup.
        var result = await _sut.AssignRoleToUserAsync("u1", "SuperAdmin", TenantId);

        result.Should().BeFalse();
        _userManagerMock.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task AssignRoleToUserAsync_ShouldRefuse_WhenUserNotFound()
    {
        _userManagerMock.Setup(m => m.FindByIdAsync("ghost")).ReturnsAsync((AppUser?)null);

        var result = await _sut.AssignRoleToUserAsync("ghost", RoleName.Requerant, TenantId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveRoleFromUserAsync_ShouldRefuse_WhenUserInOtherTenant()
    {
        var user = new AppUser { Id = "u3", TenantId = OtherTenantId };
        _userManagerMock.Setup(m => m.FindByIdAsync("u3")).ReturnsAsync(user);

        var result = await _sut.RemoveRoleFromUserAsync("u3", RoleName.Administrator, TenantId);

        result.Should().BeFalse();
        _userManagerMock.Verify(m => m.RemoveFromRoleAsync(It.IsAny<AppUser>(), It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RemoveRoleFromUserAsync_ShouldRefuse_WhenRoleUnknown()
    {
        var result = await _sut.RemoveRoleFromUserAsync("u1", "Nope", TenantId);

        result.Should().BeFalse();
        _userManagerMock.Verify(m => m.FindByIdAsync(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task RemoveRoleFromUserAsync_ShouldRemove_WhenUserInCallerTenant()
    {
        var user = new AppUser { Id = "u4", TenantId = TenantId };
        _userManagerMock.Setup(m => m.FindByIdAsync("u4")).ReturnsAsync(user);
        _userManagerMock.Setup(m => m.IsInRoleAsync(user, RoleName.Preleveur)).ReturnsAsync(true);
        _userManagerMock.Setup(m => m.RemoveFromRoleAsync(user, RoleName.Preleveur)).ReturnsAsync(IdentityResult.Success);

        var result = await _sut.RemoveRoleFromUserAsync("u4", RoleName.Preleveur, TenantId);

        result.Should().BeTrue();
        _userManagerMock.Verify(m => m.RemoveFromRoleAsync(user, RoleName.Preleveur), Times.Once);
    }
}
