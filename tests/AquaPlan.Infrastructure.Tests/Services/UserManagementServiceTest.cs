using AquaPlan.Application.DTOs.Users;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Services;

/// <summary>
/// Sprint Sec F-004 — user creation is always bound to the caller's tenant; the role and
/// distributor are validated server-side.
/// </summary>
public class UserManagementServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<ILogger<UserManagementService>> _loggerMock = new();
    private readonly UserManagementService _sut;

    private const string CreatedBy = "admin-1";
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid OwnDistributorId = Guid.Parse("10000000-0000-0000-0000-000000000001");
    private static readonly Guid ForeignDistributorId = Guid.Parse("10000000-0000-0000-0000-000000000002");

    public UserManagementServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AquaPlanDbContext(options);

        _dbContext.Distributors.AddRange(
            new Distributor { Id = OwnDistributorId, Name = "SIE", TenantId = TenantId },
            new Distributor { Id = ForeignDistributorId, Name = "Autre canton", TenantId = OtherTenantId });
        _dbContext.SaveChanges();

        var store = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);

        // CreateAsync persists the user in the InMemory context so GetUserByIdAsync finds it.
        _userManagerMock
            .Setup(m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success)
            .Callback<AppUser, string>((user, _) =>
            {
                _dbContext.Users.Add(user);
                _dbContext.SaveChanges();
            });
        _userManagerMock
            .Setup(m => m.AddToRoleAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock
            .Setup(m => m.RemoveFromRolesAsync(It.IsAny<AppUser>(), It.IsAny<IEnumerable<string>>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock
            .Setup(m => m.SetEmailAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock
            .Setup(m => m.SetUserNameAsync(It.IsAny<AppUser>(), It.IsAny<string>()))
            .ReturnsAsync(IdentityResult.Success);
        _userManagerMock
            .Setup(m => m.GetRolesAsync(It.IsAny<AppUser>()))
            .ReturnsAsync(new List<string> { RoleName.Requerant });

        _sut = new UserManagementService(_userManagerMock.Object, _dbContext, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CreateUserAsync_ShouldAttachUserToCallerTenant()
    {
        var dto = new UserCreateDto("new@user.ch", "New", "User", "Password123!", RoleName.Requerant, null);

        var result = await _sut.CreateUserAsync(dto, CreatedBy, TenantId);

        result.TenantId.Should().Be(TenantId);
        _userManagerMock.Verify(
            m => m.CreateAsync(It.Is<AppUser>(u => u.TenantId == TenantId), "Password123!"),
            Times.Once);
    }

    [Fact]
    public async Task CreateUserAsync_WithDistributorOfCallerTenant_ShouldSucceed()
    {
        var dto = new UserCreateDto("new@user.ch", "New", "User", "Password123!", RoleName.Requerant, OwnDistributorId);

        var result = await _sut.CreateUserAsync(dto, CreatedBy, TenantId);

        result.DistributorId.Should().Be(OwnDistributorId);
    }

    [Fact]
    public async Task CreateUserAsync_WhenDistributorBelongsToAnotherTenant_ShouldThrow()
    {
        var dto = new UserCreateDto("new@user.ch", "New", "User", "Password123!", RoleName.Requerant, ForeignDistributorId);

        await _sut.Awaiting(s => s.CreateUserAsync(dto, CreatedBy, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*distributor*");

        _userManagerMock.Verify(
            m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Fact]
    public async Task CreateUserAsync_WhenRoleIsUnknown_ShouldThrow()
    {
        var dto = new UserCreateDto("new@user.ch", "New", "User", "Password123!", "SuperAdmin", null);

        await _sut.Awaiting(s => s.CreateUserAsync(dto, CreatedBy, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Unknown role*");

        _userManagerMock.Verify(
            m => m.CreateAsync(It.IsAny<AppUser>(), It.IsAny<string>()),
            Times.Never);
    }

    [Theory]
    [InlineData(RoleName.Requerant)]
    [InlineData(RoleName.Preleveur)]
    [InlineData(RoleName.RequerantPreleveur)]
    [InlineData(RoleName.Administrator)]
    public async Task CreateUserAsync_WithKnownRole_ShouldAssignIt(string role)
    {
        var dto = new UserCreateDto("new@user.ch", "New", "User", "Password123!", role, null);

        await _sut.CreateUserAsync(dto, CreatedBy, TenantId);

        _userManagerMock.Verify(
            m => m.AddToRoleAsync(It.Is<AppUser>(u => u.Email == "new@user.ch"), role),
            Times.Once);
    }

    // --- Polish F-217 — UpdateUserAsync role validation + Identity result checking ---

    [Fact]
    public async Task UpdateUserAsync_WhenRoleIsUnknown_ShouldThrowAndNotStripRoles()
    {
        var userId = await SeedExistingUser();
        var dto = new UserUpdateDto("u@test.ch", "First", "Last", "SuperAdmin");

        await _sut.Awaiting(s => s.UpdateUserAsync(userId, dto, CreatedBy, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Unknown role*");

        // The unknown role is rejected BEFORE removing the existing roles.
        _userManagerMock.Verify(
            m => m.RemoveFromRolesAsync(It.IsAny<AppUser>(), It.IsAny<IEnumerable<string>>()),
            Times.Never);
    }

    [Fact]
    public async Task UpdateUserAsync_WhenAddRoleFails_ShouldThrow()
    {
        var userId = await SeedExistingUser();
        _userManagerMock
            .Setup(m => m.AddToRoleAsync(It.IsAny<AppUser>(), RoleName.Administrator))
            .ReturnsAsync(IdentityResult.Failed(new IdentityError { Description = "boom" }));
        var dto = new UserUpdateDto(null, "First", "Last", RoleName.Administrator);

        await _sut.Awaiting(s => s.UpdateUserAsync(userId, dto, CreatedBy, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*assign role*");
    }

    // --- AQ-429 — GetUsersAsync must resolve the role via the Identity UserRoles/Roles tables
    // (no correlated subquery in the projection), stay tenant-scoped, and order by LastName
    // then FirstName. ---

    private void SeedUserWithRole(string id, string firstName, string lastName, string roleName, Guid tenantId)
    {
        var roleId = $"role-{roleName}";
        if (!_dbContext.Roles.Any(r => r.Id == roleId))
        {
            _dbContext.Roles.Add(new ApplicationRole { Id = roleId, Name = roleName });
        }
        _dbContext.Users.Add(new AppUser
        {
            Id = id,
            UserName = $"{id}@test.ch",
            Email = $"{id}@test.ch",
            FirstName = firstName,
            LastName = lastName,
            TenantId = tenantId,
            IsActive = true,
        });
        _dbContext.UserRoles.Add(new IdentityUserRole<string> { UserId = id, RoleId = roleId });
        _dbContext.SaveChanges();
    }

    [Fact]
    public async Task GetUsersAsync_ShouldReturnTenantUsersWithRoleOrderedByLastName()
    {
        SeedUserWithRole("u-zulu", "Anna", "Zulu", RoleName.Preleveur, TenantId);
        SeedUserWithRole("u-alpha", "Bob", "Alpha", RoleName.Administrator, TenantId);
        SeedUserWithRole("u-foreign", "Carl", "Other", RoleName.Requerant, OtherTenantId);

        var result = await _sut.GetUsersAsync(TenantId, role: null, distributorId: null, isActive: null, CancellationToken.None);

        result.Select(u => u.LastName).Should().Equal("Alpha", "Zulu");
        result.Single(u => u.LastName == "Alpha").Role.Should().Be(RoleName.Administrator);
        result.Single(u => u.LastName == "Zulu").Role.Should().Be(RoleName.Preleveur);
    }

    [Fact]
    public async Task GetUsersAsync_WhenFilteredByRole_ShouldReturnOnlyMatchingUsers()
    {
        SeedUserWithRole("u-a", "A", "AA", RoleName.Administrator, TenantId);
        SeedUserWithRole("u-p", "P", "PP", RoleName.Preleveur, TenantId);

        var result = await _sut.GetUsersAsync(TenantId, role: RoleName.Preleveur, distributorId: null, isActive: null, CancellationToken.None);

        result.Should().ContainSingle();
        result[0].Role.Should().Be(RoleName.Preleveur);
    }

    private async Task<string> SeedExistingUser()
    {
        var userId = Guid.NewGuid().ToString();
        _dbContext.Users.Add(new AppUser
        {
            Id = userId,
            UserName = "existing@test.ch",
            Email = "existing@test.ch",
            FirstName = "Old",
            LastName = "Name",
            TenantId = TenantId,
            IsActive = true,
        });
        await _dbContext.SaveChangesAsync();
        return userId;
    }
}
