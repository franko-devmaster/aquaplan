using AquaPlan.Application.DTOs.Delegations;
using AquaPlan.Application.Exceptions;
using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Services;

public class DelegationServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<ILogger<DelegationService>> _loggerMock = new();
    private readonly DelegationService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid DistributorAId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid DistributorBId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private const string UserId = "user-1";

    public DelegationServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AquaPlanDbContext(options);
        _sut = new DelegationService(_dbContext, _loggerMock.Object);

        SeedData().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnDelegationsForTenant()
    {
        await SeedDelegation(DistributorAId, DistributorBId);

        var result = await _sut.GetAllAsync(TenantId);

        result.Should().HaveCount(1);
        result[0].DelegatingDistributorId.Should().Be(DistributorAId);
        result[0].DelegatedToDistributorId.Should().Be(DistributorBId);
    }

    [Fact]
    public async Task GetAllAsync_ShouldNotReturnOtherTenantDelegations()
    {
        var otherTenantId = Guid.NewGuid();
        _dbContext.DistributorDelegations.Add(new DistributorDelegation
        {
            Id = Guid.NewGuid(),
            DelegatingDistributorId = DistributorAId,
            DelegatedToDistributorId = DistributorBId,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            IsActive = true,
            TenantId = otherTenantId,
        });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetAllAsync(TenantId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateDelegation()
    {
        var dto = new DistributorDelegationCreateDto(
            DistributorAId, DistributorBId,
            DateTime.UtcNow.AddDays(-1), null);

        var result = await _sut.CreateAsync(dto, TenantId);

        result.DelegatingDistributorId.Should().Be(DistributorAId);
        result.DelegatedToDistributorId.Should().Be(DistributorBId);
        result.IsActive.Should().BeTrue();
    }

    // --- Polish F-219 — delegation creation validation ---

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenSelfDelegation()
    {
        var dto = new DistributorDelegationCreateDto(
            DistributorAId, DistributorAId, DateTime.UtcNow, null);

        await _sut.Awaiting(s => s.CreateAsync(dto, TenantId))
            .Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*itself*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenValidToBeforeValidFrom()
    {
        var dto = new DistributorDelegationCreateDto(
            DistributorAId, DistributorBId,
            DateTime.UtcNow, DateTime.UtcNow.AddDays(-5));

        await _sut.Awaiting(s => s.CreateAsync(dto, TenantId))
            .Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*end date*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenDistributorNotInTenant()
    {
        var dto = new DistributorDelegationCreateDto(
            DistributorAId, Guid.NewGuid(), DateTime.UtcNow, null);

        await _sut.Awaiting(s => s.CreateAsync(dto, TenantId))
            .Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*tenant*");
    }

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenActiveDuplicateExists()
    {
        await SeedDelegation(DistributorAId, DistributorBId);
        var dto = new DistributorDelegationCreateDto(
            DistributorAId, DistributorBId, DateTime.UtcNow, null);

        await _sut.Awaiting(s => s.CreateAsync(dto, TenantId))
            .Should().ThrowAsync<BusinessRuleException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveDelegation()
    {
        var delegation = await SeedDelegation(DistributorAId, DistributorBId);

        var deleted = await _sut.DeleteAsync(delegation.Id, TenantId);

        deleted.Should().BeTrue();
        var remaining = await _dbContext.DistributorDelegations.CountAsync();
        remaining.Should().Be(0);
    }

    [Fact]
    public async Task DeleteAsync_ShouldReturnFalse_WhenNotFound()
    {
        var deleted = await _sut.DeleteAsync(Guid.NewGuid(), TenantId);

        deleted.Should().BeFalse();
    }

    // --- Polish F-227 — UserHasDistributorAccessAsync (single source of truth) ---

    [Fact]
    public async Task UserHasDistributorAccessAsync_ShouldReturnTrue_ForOwnDistributor()
    {
        var result = await _sut.UserHasDistributorAccessAsync(UserId, DistributorBId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UserHasDistributorAccessAsync_ShouldReturnFalse_ForUnrelatedDistributor()
    {
        var result = await _sut.UserHasDistributorAccessAsync(UserId, DistributorAId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task UserHasDistributorAccessAsync_ShouldReturnTrue_ForDelegatingDistributor()
    {
        await SeedDelegation(DistributorAId, DistributorBId);

        var result = await _sut.UserHasDistributorAccessAsync(UserId, DistributorAId);

        result.Should().BeTrue();
    }

    // --- AQ-369 GetAuthorizedDistributorIdsForUserAsync ---

    [Fact]
    public async Task GetAuthorizedDistributorIdsForUserAsync_ForUserWithoutDelegation_ShouldReturnOnlyOwnDistributor()
    {
        var result = await _sut.GetAuthorizedDistributorIdsForUserAsync(UserId);

        result.Should().Contain(DistributorBId);
        result.Should().NotContain(DistributorAId);
    }

    [Fact]
    public async Task GetAuthorizedDistributorIdsForUserAsync_WithActiveDelegation_ShouldReturnOwnPlusDelegating()
    {
        await SeedDelegation(DistributorAId, DistributorBId);

        var result = await _sut.GetAuthorizedDistributorIdsForUserAsync(UserId);

        result.Should().Contain(DistributorBId);
        result.Should().Contain(DistributorAId);
    }

    [Fact]
    public async Task GetAuthorizedDistributorIdsForUserAsync_WithExpiredDelegation_ShouldReturnOnlyOwn()
    {
        _dbContext.DistributorDelegations.Add(new DistributorDelegation
        {
            Id = Guid.NewGuid(),
            DelegatingDistributorId = DistributorAId,
            DelegatedToDistributorId = DistributorBId,
            ValidFrom = DateTime.UtcNow.AddDays(-30),
            ValidTo = DateTime.UtcNow.AddDays(-1),
            IsActive = true,
            TenantId = TenantId,
        });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetAuthorizedDistributorIdsForUserAsync(UserId);

        result.Should().Contain(DistributorBId);
        result.Should().NotContain(DistributorAId);
    }

    [Fact]
    public async Task GetAuthorizedDistributorIdsForUserAsync_WithInactiveDelegation_ShouldReturnOnlyOwn()
    {
        _dbContext.DistributorDelegations.Add(new DistributorDelegation
        {
            Id = Guid.NewGuid(),
            DelegatingDistributorId = DistributorAId,
            DelegatedToDistributorId = DistributorBId,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            IsActive = false,
            TenantId = TenantId,
        });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetAuthorizedDistributorIdsForUserAsync(UserId);

        result.Should().Contain(DistributorBId);
        result.Should().NotContain(DistributorAId);
    }

    [Fact]
    public async Task GetAuthorizedDistributorsForUserAsync_WhenAdmin_ShouldReturnAllTenantDistributors()
    {
        var result = await _sut.GetAuthorizedDistributorsForUserAsync(UserId, TenantId, isAdmin: true);

        result.Should().HaveCount(2);
        result.Select(d => d.Id).Should().Contain([DistributorAId, DistributorBId]);
    }

    [Fact]
    public async Task GetAuthorizedDistributorsForUserAsync_WhenNotAdmin_ShouldReturnOnlyAuthorized()
    {
        var result = await _sut.GetAuthorizedDistributorsForUserAsync(UserId, TenantId, isAdmin: false);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(DistributorBId);
    }

    [Fact]
    public async Task GetAuthorizedDistributorsForUserAsync_WhenNotAdmin_WithDelegation_ShouldIncludeDelegating()
    {
        await SeedDelegation(DistributorAId, DistributorBId);

        var result = await _sut.GetAuthorizedDistributorsForUserAsync(UserId, TenantId, isAdmin: false);

        result.Should().HaveCount(2);
        result.Select(d => d.Id).Should().Contain([DistributorAId, DistributorBId]);
    }

    // --- AQ-395 AppUser.DistributorId primary link must also be considered ---

    [Fact]
    public async Task GetAuthorizedDistributorIdsForUserAsync_WhenUserOnlyHasPrimaryDistributorId_ShouldReturnIt()
    {
        // francisuster case: user has DistributorId on AppUser but no row in UserDistributors.
        const string userOnlyPrimaryId = "user-primary-only";
        _dbContext.Users.Add(new AppUser
        {
            Id = userOnlyPrimaryId,
            UserName = "primary@test.com",
            Email = "primary@test.com",
            FirstName = "Primary",
            LastName = "User",
            TenantId = TenantId,
            IsActive = true,
            DistributorId = DistributorAId,
        });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetAuthorizedDistributorIdsForUserAsync(userOnlyPrimaryId);

        result.Should().Contain(DistributorAId);
    }

    [Fact]
    public async Task GetAuthorizedDistributorIdsForUserAsync_WhenUserHasPrimaryAndLink_ShouldReturnDistinct()
    {
        // User has DistributorB via both AppUser.DistributorId and UserDistributors — no duplicate.
        var user = await _dbContext.Users.FirstAsync(u => u.Id == UserId);
        user.DistributorId = DistributorBId;
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetAuthorizedDistributorIdsForUserAsync(UserId);

        result.Should().ContainSingle(id => id == DistributorBId);
    }

    [Fact]
    public async Task GetAuthorizedDistributorIdsForUserAsync_WhenUserOnlyHasPrimaryDistributorId_ShouldIncludeDelegatingIds()
    {
        // User has only AppUser.DistributorId (no UserDistributors), a delegation targets that distributor.
        const string userOnlyPrimaryId = "user-primary-delegation";
        _dbContext.Users.Add(new AppUser
        {
            Id = userOnlyPrimaryId,
            UserName = "delegation@test.com",
            Email = "delegation@test.com",
            FirstName = "Delegation",
            LastName = "User",
            TenantId = TenantId,
            IsActive = true,
            DistributorId = DistributorBId,
        });
        await _dbContext.SaveChangesAsync();
        await SeedDelegation(DistributorAId, DistributorBId);

        var result = await _sut.GetAuthorizedDistributorIdsForUserAsync(userOnlyPrimaryId);

        result.Should().Contain(DistributorAId);
        result.Should().Contain(DistributorBId);
    }

    private async Task<DistributorDelegation> SeedDelegation(Guid delegatingId, Guid delegatedToId)
    {
        var delegation = new DistributorDelegation
        {
            Id = Guid.NewGuid(),
            DelegatingDistributorId = delegatingId,
            DelegatedToDistributorId = delegatedToId,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            IsActive = true,
            TenantId = TenantId,
        };
        _dbContext.DistributorDelegations.Add(delegation);
        await _dbContext.SaveChangesAsync();
        return delegation;
    }

    private async Task SeedData()
    {
        _dbContext.Distributors.Add(new Distributor
        {
            Id = DistributorAId,
            Name = "Distributor A",
            TenantId = TenantId,
            IsActive = true,
        });

        _dbContext.Distributors.Add(new Distributor
        {
            Id = DistributorBId,
            Name = "Distributor B",
            TenantId = TenantId,
            IsActive = true,
        });

        _dbContext.Users.Add(new AppUser
        {
            Id = UserId,
            UserName = "user1@test.com",
            Email = "user1@test.com",
            FirstName = "Test",
            LastName = "User",
            TenantId = TenantId,
            IsActive = true,
        });

        // User belongs to DistributorB
        _dbContext.UserDistributors.Add(new UserDistributor
        {
            UserId = UserId,
            DistributorId = DistributorBId,
        });

        await _dbContext.SaveChangesAsync();
    }
}
