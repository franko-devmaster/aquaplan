using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Services;

public class SamplingLocationServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<ILogger<SamplingLocationService>> _loggerMock = new();
    private readonly Mock<IDelegationService> _delegationServiceMock = new();
    private readonly SamplingLocationService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid OtherDistributorId = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly Guid SectorId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private static readonly Guid LocationId = Guid.Parse("00000000-0000-0000-0000-000000000030");
    private static readonly Guid OtherLocationId = Guid.Parse("00000000-0000-0000-0000-000000000031");
    private const string UserId = "user-1";

    public SamplingLocationServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AquaPlanDbContext(options);
        _sut = new SamplingLocationService(_dbContext, _delegationServiceMock.Object, _loggerMock.Object);

        SeedDataAsync().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetByIdAsync_WhenLocationNotLinkedToOrder_ShouldReturnCanDeleteTrue()
    {
        var result = await _sut.GetByIdAsync(LocationId, TenantId);

        result.Should().NotBeNull();
        result!.CanDelete.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_WhenLocationLinkedToOrder_ShouldReturnCanDeleteFalse()
    {
        await SeedOrderLinkedToLocationAsync();

        var result = await _sut.GetByIdAsync(LocationId, TenantId);

        result.Should().NotBeNull();
        result!.CanDelete.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_WhenLocationNotFound_ShouldReturnNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenOtherTenant_ShouldReturnNull()
    {
        var result = await _sut.GetByIdAsync(LocationId, Guid.NewGuid());

        result.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_WhenLocationLinkedToOrder_ShouldThrowInvalidOperationException()
    {
        await SeedOrderLinkedToLocationAsync();

        await _sut.Awaiting(x => x.DeleteAsync(LocationId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task DeleteAsync_WhenLocationNotLinkedToOrder_ShouldReturnTrue()
    {
        var deleted = await _sut.DeleteAsync(LocationId, TenantId);

        deleted.Should().BeTrue();
        var remaining = await _dbContext.SamplingLocations.FindAsync(LocationId);
        remaining.Should().BeNull();
    }

    [Fact]
    public async Task ValidateAsync_ShouldSetIsValidatedTrueAndIsActiveTrue()
    {
        var location = await _dbContext.SamplingLocations.FindAsync(LocationId);
        location!.IsValidated = false;
        location.IsActive = false;
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ValidateAsync(LocationId, TenantId);

        result.Should().NotBeNull();
        result!.IsValidated.Should().BeTrue();
        result.IsActive.Should().BeTrue();
    }

    // --- AQ-395 GetForUserAsync honours AppUser.DistributorId + delegations ---

    [Fact]
    public async Task GetForUserAsync_ForUserWithDistributor_ShouldIncludeHisDistributorLocations()
    {
        // francisuster case: user has a primary DistributorId and no UserDistributors row.
        // The service must still return the LDP of his primary distributor.
        _delegationServiceMock
            .Setup(x => x.GetAuthorizedDistributorIdsForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([DistributorId]);

        var result = await _sut.GetForUserAsync(UserId, TenantId);

        result.Should().HaveCount(1);
        result[0].Id.Should().Be(LocationId);
    }

    [Fact]
    public async Task GetForUserAsync_ForUserWithActiveDelegation_ShouldIncludeDelegatingDistributorLocations()
    {
        await SeedOtherDistributorAndLocationAsync();

        _delegationServiceMock
            .Setup(x => x.GetAuthorizedDistributorIdsForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([DistributorId, OtherDistributorId]);

        var result = await _sut.GetForUserAsync(UserId, TenantId);

        result.Should().HaveCount(2);
        result.Select(l => l.Id).Should().Contain([LocationId, OtherLocationId]);
    }

    [Fact]
    public async Task GetForUserAsync_ForUserWithNoAuthorizedDistributor_ShouldReturnEmpty()
    {
        _delegationServiceMock
            .Setup(x => x.GetAuthorizedDistributorIdsForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var result = await _sut.GetForUserAsync(UserId, TenantId);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetForUserAsync_ShouldNotReturnOtherTenantLocations()
    {
        var otherTenantId = Guid.NewGuid();
        _delegationServiceMock
            .Setup(x => x.GetAuthorizedDistributorIdsForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([DistributorId]);

        var result = await _sut.GetForUserAsync(UserId, otherTenantId);

        result.Should().BeEmpty();
    }

    private async Task SeedOtherDistributorAndLocationAsync()
    {
        _dbContext.Distributors.Add(new Distributor
        {
            Id = OtherDistributorId,
            Name = "Other distributor",
            TenantId = TenantId,
            IsActive = true,
        });
        _dbContext.SamplingLocations.Add(new SamplingLocation
        {
            Id = OtherLocationId,
            Name = "Source B",
            LocationCode = "LOC-002",
            DistributorId = OtherDistributorId,
            SectorId = SectorId,
            IsActive = true,
            IsValidated = true,
        });
        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedDataAsync()
    {
        _dbContext.Tenants.Add(new Tenant
        {
            Id = TenantId,
            Name = "Tenant Fribourg",
        });

        _dbContext.Distributors.Add(new Distributor
        {
            Id = DistributorId,
            Name = "Eau de Fribourg",
            TenantId = TenantId,
            IsActive = true,
        });

        _dbContext.Sectors.Add(new Sector
        {
            Id = SectorId,
            Name = "Secteur Nord",
            DistributorId = DistributorId,
            IsActive = true,
        });

        _dbContext.SamplingLocations.Add(new SamplingLocation
        {
            Id = LocationId,
            Name = "Source A",
            LocationCode = "LOC-001",
            DistributorId = DistributorId,
            SectorId = SectorId,
            IsActive = true,
            IsValidated = true,
        });

        await _dbContext.SaveChangesAsync();
    }

    private async Task SeedOrderLinkedToLocationAsync()
    {
        _dbContext.Orders.Add(new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-0001",
            Status = OrderStatus.New,
            DistributorId = DistributorId,
            SamplingLocationId = LocationId,
            CreatedById = "user-1",
            TenantId = TenantId,
        });
        await _dbContext.SaveChangesAsync();
    }
}
