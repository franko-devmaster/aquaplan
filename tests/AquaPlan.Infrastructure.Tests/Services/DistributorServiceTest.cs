using AquaPlan.Application.DTOs.Distributors;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Services;

public class DistributorServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<IDelegationService> _delegationServiceMock = new();
    private readonly Mock<ILogger<DistributorService>> _loggerMock = new();
    private readonly DistributorService _sut;

    private const string UserId = "user-1";
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public DistributorServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AquaPlanDbContext(options);
        _sut = new DistributorService(_dbContext, _delegationServiceMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnDistributorsForTenant()
    {
        await SeedDistributors();

        var filter = new DistributorFilteringInputDto(null, null);
        var result = await _sut.GetAllAsync(filter, TenantId, UserId, isAdmin: true);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(d => d.Name.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public async Task GetAllAsync_ShouldNotReturnDistributorsFromOtherTenant()
    {
        await SeedDistributors();

        var filter = new DistributorFilteringInputDto(null, null);
        var result = await _sut.GetAllAsync(filter, OtherTenantId, UserId, isAdmin: true);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Autre Distributeur");
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByName()
    {
        await SeedDistributors();

        var filter = new DistributorFilteringInputDto("Fribourg", null);
        var result = await _sut.GetAllAsync(filter, TenantId, UserId, isAdmin: true);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Eau de Fribourg");
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByStatus()
    {
        await SeedDistributors();

        var filter = new DistributorFilteringInputDto(null, false);
        var result = await _sut.GetAllAsync(filter, TenantId, UserId, isAdmin: true);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Ancien Distributeur");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnOrderedByName()
    {
        await SeedDistributors();

        var filter = new DistributorFilteringInputDto(null, null);
        var result = await _sut.GetAllAsync(filter, TenantId, UserId, isAdmin: true);

        result.Should().BeInAscendingOrder(d => d.Name);
    }

    // AQ-419 — scoping by authorized distributors for non-admin users.

    [Fact]
    public async Task GetAllAsync_AsAdmin_ShouldIgnoreDelegationAndReturnAllTenantDistributors()
    {
        await SeedDistributors();

        var filter = new DistributorFilteringInputDto(null, null);
        var result = await _sut.GetAllAsync(filter, TenantId, UserId, isAdmin: true);

        result.Should().HaveCount(2);
        _delegationServiceMock.Verify(d => d.GetAuthorizedDistributorIdsForUserAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_AsNonAdmin_ShouldOnlyReturnAuthorizedDistributors()
    {
        var distributors = await SeedDistributors();
        _delegationServiceMock
            .Setup(d => d.GetAuthorizedDistributorIdsForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([distributors[0].Id]);

        var filter = new DistributorFilteringInputDto(null, null);
        var result = await _sut.GetAllAsync(filter, TenantId, UserId, isAdmin: false);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Eau de Fribourg");
    }

    [Fact]
    public async Task GetAllAsync_AsNonAdmin_ShouldIncludeDelegatedDistributor()
    {
        var distributors = await SeedDistributors();
        _delegationServiceMock
            .Setup(d => d.GetAuthorizedDistributorIdsForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([distributors[0].Id, distributors[1].Id]);

        var filter = new DistributorFilteringInputDto(null, null);
        var result = await _sut.GetAllAsync(filter, TenantId, UserId, isAdmin: false);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_AsNonAdmin_WithNoAuthorizedDistributor_ShouldReturnEmpty()
    {
        await SeedDistributors();
        _delegationServiceMock
            .Setup(d => d.GetAuthorizedDistributorIdsForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var filter = new DistributorFilteringInputDto(null, null);
        var result = await _sut.GetAllAsync(filter, TenantId, UserId, isAdmin: false);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnDistributor_WhenExists()
    {
        var distributors = await SeedDistributors();
        var distributorId = distributors[0].Id;

        var result = await _sut.GetByIdAsync(distributorId, TenantId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Eau de Fribourg");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotExists()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenDifferentTenant()
    {
        var distributors = await SeedDistributors();
        var distributorId = distributors[0].Id;

        var result = await _sut.GetByIdAsync(distributorId, OtherTenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateDistributor()
    {
        var dto = new DistributorAddDto("Nouveau Distributeur", null, "Gruyère", "Réseau B");

        var result = await _sut.CreateAsync(dto, TenantId);

        result.Should().NotBeNull();
        result.Name.Should().Be("Nouveau Distributeur");
        result.CantonRegion.Should().Be("Gruyère");
        result.DistributionNetwork.Should().Be("Réseau B");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenNameAlreadyExists()
    {
        await SeedDistributors();
        var dto = new DistributorAddDto("Eau de Fribourg", null, "Sarine", "Réseau C");

        await _sut.Invoking(x => x.CreateAsync(dto, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateAsync_ShouldAllowSameNameInDifferentTenant()
    {
        await SeedDistributors();
        var dto = new DistributorAddDto("Eau de Fribourg", null, "Sarine", "Réseau C");

        var result = await _sut.CreateAsync(dto, OtherTenantId);

        result.Should().NotBeNull();
        result.Name.Should().Be("Eau de Fribourg");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateDistributor()
    {
        var distributors = await SeedDistributors();
        var distributorId = distributors[0].Id;
        var dto = new DistributorUpdateDto("Nom Modifié", null, "Broye", "Réseau X");

        var result = await _sut.UpdateAsync(distributorId, dto, TenantId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Nom Modifié");
        result.CantonRegion.Should().Be("Broye");
        result.DistributionNetwork.Should().Be("Réseau X");
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNull_WhenNotExists()
    {
        var dto = new DistributorUpdateDto("Nom Modifié", null, "Broye", "Réseau X");

        var result = await _sut.UpdateAsync(Guid.NewGuid(), dto, TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenNameAlreadyExists()
    {
        var distributors = await SeedDistributors();
        var distributorId = distributors[1].Id;
        var dto = new DistributorUpdateDto("Eau de Fribourg", null, "Sarine", "Réseau A");

        await _sut.Invoking(x => x.UpdateAsync(distributorId, dto, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldAllowKeepingSameName()
    {
        var distributors = await SeedDistributors();
        var distributorId = distributors[0].Id;
        var dto = new DistributorUpdateDto("Eau de Fribourg", null, "Broye", "Réseau X");

        var result = await _sut.UpdateAsync(distributorId, dto, TenantId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Eau de Fribourg");
        result.CantonRegion.Should().Be("Broye");
    }

    [Fact]
    public async Task ToggleStatusAsync_ShouldDeactivateActiveDistributor()
    {
        var distributors = await SeedDistributors();
        var distributorId = distributors[0].Id;

        var result = await _sut.ToggleStatusAsync(distributorId, TenantId);

        result.Should().NotBeNull();
        result!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleStatusAsync_ShouldActivateInactiveDistributor()
    {
        var distributors = await SeedDistributors();
        var inactiveDistributorId = distributors[1].Id;

        var result = await _sut.ToggleStatusAsync(inactiveDistributorId, TenantId);

        result.Should().NotBeNull();
        result!.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task ToggleStatusAsync_ShouldReturnNull_WhenNotExists()
    {
        var result = await _sut.ToggleStatusAsync(Guid.NewGuid(), TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ToggleStatusAsync_ShouldReturnNull_WhenDifferentTenant()
    {
        var distributors = await SeedDistributors();
        var distributorId = distributors[0].Id;

        var result = await _sut.ToggleStatusAsync(distributorId, OtherTenantId);

        result.Should().BeNull();
    }

    private async Task<List<Distributor>> SeedDistributors()
    {
        var distributors = new List<Distributor>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Eau de Fribourg",
                CantonRegion = "Sarine",
                DistributionNetwork = "Réseau A",
                IsActive = true,
                TenantId = TenantId,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Ancien Distributeur",
                CantonRegion = "Broye",
                DistributionNetwork = "Réseau B",
                IsActive = false,
                TenantId = TenantId,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Name = "Autre Distributeur",
                CantonRegion = "Veveyse",
                DistributionNetwork = "Réseau C",
                IsActive = true,
                TenantId = OtherTenantId,
            },
        };

        _dbContext.Distributors.AddRange(distributors);
        await _dbContext.SaveChangesAsync();

        return distributors;
    }
}
