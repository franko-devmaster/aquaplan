using AquaPlan.Application.DTOs.Delegations;
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

    [Fact]
    public async Task GetDelegatedDistributorIdsAsync_ShouldReturnDelegatingIds()
    {
        await SeedDelegation(DistributorAId, DistributorBId);

        // User belongs to DistributorB, so DistributorA delegating to B means user sees A
        var result = await _sut.GetDelegatedDistributorIdsAsync(UserId);

        result.Should().Contain(DistributorAId);
    }

    [Fact]
    public async Task GetDelegatedDistributorIdsAsync_ShouldNotReturnExpiredDelegations()
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

        var result = await _sut.GetDelegatedDistributorIdsAsync(UserId);

        result.Should().NotContain(DistributorAId);
    }

    [Fact]
    public async Task GetDelegatedDistributorIdsAsync_ShouldNotReturnInactiveDelegations()
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

        var result = await _sut.GetDelegatedDistributorIdsAsync(UserId);

        result.Should().NotContain(DistributorAId);
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
