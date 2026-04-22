using AquaPlan.Application.DTOs.Sectors;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Services;

public class SectorServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<IDelegationService> _delegationServiceMock = new();
    private readonly Mock<ILogger<SectorService>> _loggerMock = new();
    private readonly SectorService _sut;

    private const string UserId = "user-1";
    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000099");
    private static readonly Guid SectorId = Guid.Parse("00000000-0000-0000-0000-000000000030");
    private static readonly Guid DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000050");
    private static readonly Guid OtherDistributorId = Guid.Parse("00000000-0000-0000-0000-000000000051");

    public SectorServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AquaPlanDbContext(options);

        // AQ-418 — by default the delegation service returns the user's primary distributor only.
        // Tests that need delegation behaviour override this setup explicitly.
        _delegationServiceMock
            .Setup(d => d.GetAuthorizedDistributorIdsForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([DistributorId]);

        _sut = new SectorService(_dbContext, _delegationServiceMock.Object, _loggerMock.Object);

        // Seed a distributor for FK references
        _dbContext.Distributors.Add(new Distributor
        {
            Id = DistributorId,
            Name = "Test Distributor",
            TenantId = TenantId,
        });
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnSectorsForTenant()
    {
        await SeedSector(SectorId, "Secteur Nord", "SN");
        await SeedSector(Guid.NewGuid(), "Secteur Sud", "SS");

        var result = await _sut.GetAllAsync(new SectorFilteringInputDto(null, null), TenantId, UserId, isAdmin: true);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByName()
    {
        await SeedSector(Guid.NewGuid(), "Secteur Nord", "SN");
        await SeedSector(Guid.NewGuid(), "Secteur Sud", "SS");
        await SeedSector(Guid.NewGuid(), "Zone Est", "ZE");

        var result = await _sut.GetAllAsync(new SectorFilteringInputDto("Secteur", null), TenantId, UserId, isAdmin: true);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(s => s.Name.Should().Contain("Secteur"));
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByIsActive()
    {
        await SeedSector(Guid.NewGuid(), "Secteur Nord", "SN", isActive: true);
        await SeedSector(Guid.NewGuid(), "Secteur Sud", "SS", isActive: false);

        var result = await _sut.GetAllAsync(new SectorFilteringInputDto(null, true), TenantId, UserId, isAdmin: true);

        result.Should().HaveCount(1);
        result.Single().Name.Should().Be("Secteur Nord");
    }

    [Fact]
    public async Task GetAllAsync_ShouldNotReturnOtherTenantSectors()
    {
        await SeedSector(SectorId, "Secteur Nord", "SN");
        await SeedSector(Guid.NewGuid(), "Secteur Autre", "SA", tenantId: OtherTenantId);

        var result = await _sut.GetAllAsync(new SectorFilteringInputDto(null, null), TenantId, UserId, isAdmin: true);

        result.Should().HaveCount(1);
        result.Single().Name.Should().Be("Secteur Nord");
    }

    // AQ-418 — scoping by authorized distributors for non-admin users.

    [Fact]
    public async Task GetAllAsync_AsAdmin_ShouldReturnEveryTenantSectorRegardlessOfDistributor()
    {
        await SeedOtherDistributor();
        await SeedSector(SectorId, "Secteur Nord", "SN");
        await SeedSector(Guid.NewGuid(), "Secteur Autre Dist", "SAD", distributorId: OtherDistributorId);

        var result = await _sut.GetAllAsync(new SectorFilteringInputDto(null, null), TenantId, UserId, isAdmin: true);

        result.Should().HaveCount(2);
        _delegationServiceMock.Verify(d => d.GetAuthorizedDistributorIdsForUserAsync(
            It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetAllAsync_AsNonAdmin_ShouldOnlyReturnSectorsOfAuthorizedDistributors()
    {
        await SeedOtherDistributor();
        await SeedSector(SectorId, "Secteur Fribourg", "SF");
        await SeedSector(Guid.NewGuid(), "Secteur Autre Dist", "SAD", distributorId: OtherDistributorId);

        var result = await _sut.GetAllAsync(new SectorFilteringInputDto(null, null), TenantId, UserId, isAdmin: false);

        result.Should().HaveCount(1);
        result.Single().Name.Should().Be("Secteur Fribourg");
    }

    [Fact]
    public async Task GetAllAsync_AsNonAdmin_ShouldIncludeSectorsOfDelegatedDistributor()
    {
        // Delegation: OtherDistributorId has delegated to DistributorId → user sees both.
        _delegationServiceMock
            .Setup(d => d.GetAuthorizedDistributorIdsForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([DistributorId, OtherDistributorId]);

        await SeedOtherDistributor();
        await SeedSector(SectorId, "Secteur Fribourg", "SF");
        await SeedSector(Guid.NewGuid(), "Secteur Morat", "SM", distributorId: OtherDistributorId);

        var result = await _sut.GetAllAsync(new SectorFilteringInputDto(null, null), TenantId, UserId, isAdmin: false);

        result.Should().HaveCount(2);
        result.Select(r => r.Name).Should().BeEquivalentTo(new[] { "Secteur Fribourg", "Secteur Morat" });
    }

    [Fact]
    public async Task GetAllAsync_AsNonAdmin_WithNoAuthorizedDistributor_ShouldReturnEmpty()
    {
        _delegationServiceMock
            .Setup(d => d.GetAuthorizedDistributorIdsForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        await SeedSector(SectorId, "Secteur Nord", "SN");

        var result = await _sut.GetAllAsync(new SectorFilteringInputDto(null, null), TenantId, UserId, isAdmin: false);

        result.Should().BeEmpty();
    }

    private async Task SeedOtherDistributor()
    {
        _dbContext.Distributors.Add(new Distributor
        {
            Id = OtherDistributorId,
            Name = "Autre Distributeur",
            TenantId = TenantId,
        });
        await _dbContext.SaveChangesAsync();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnSector_WhenExists()
    {
        await SeedSector(SectorId, "Secteur Nord", "SN");

        var result = await _sut.GetByIdAsync(SectorId, TenantId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(SectorId);
        result.Name.Should().Be("Secteur Nord");
        result.Code.Should().Be("SN");
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenNotFound()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnNull_WhenOtherTenant()
    {
        await SeedSector(SectorId, "Secteur Nord", "SN");

        var result = await _sut.GetByIdAsync(SectorId, OtherTenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ShouldCreateSector()
    {
        var addDto = new SectorAddDto("Nouveau Secteur", "NS", "Description test", DistributorId);

        var result = await _sut.CreateAsync(addDto, TenantId);

        result.Should().NotBeNull();
        result.Name.Should().Be("Nouveau Secteur");
        result.Code.Should().Be("NS");
        result.Description.Should().Be("Description test");
        result.IsActive.Should().BeTrue();

        var saved = await _dbContext.Sectors.FirstOrDefaultAsync(s => s.Name == "Nouveau Secteur");
        saved.Should().NotBeNull();
        saved!.TenantId.Should().Be(TenantId);
    }

    [Fact]
    public async Task CreateAsync_ShouldThrow_WhenDuplicateName()
    {
        await SeedSector(SectorId, "Secteur Nord", "SN");

        var addDto = new SectorAddDto("Secteur Nord", "SN2", null, DistributorId);

        await _sut.Awaiting(x => x.CreateAsync(addDto, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Secteur Nord*already exists*");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateSector()
    {
        await SeedSector(SectorId, "Secteur Nord", "SN");

        var updateDto = new SectorUpdateDto("Secteur Modifie", "SM", "Nouvelle description", DistributorId);

        var result = await _sut.UpdateAsync(SectorId, updateDto, TenantId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Secteur Modifie");
        result.Code.Should().Be("SM");
        result.Description.Should().Be("Nouvelle description");
    }

    [Fact]
    public async Task UpdateAsync_ShouldReturnNull_WhenNotFound()
    {
        var updateDto = new SectorUpdateDto("Secteur Modifie", "SM", null, DistributorId);

        var result = await _sut.UpdateAsync(Guid.NewGuid(), updateDto, TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_ShouldThrow_WhenDuplicateName()
    {
        await SeedSector(SectorId, "Secteur Nord", "SN");
        var otherId = Guid.NewGuid();
        await SeedSector(otherId, "Secteur Sud", "SS");

        var updateDto = new SectorUpdateDto("Secteur Nord", "SS", null, DistributorId);

        await _sut.Awaiting(x => x.UpdateAsync(otherId, updateDto, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Secteur Nord*already exists*");
    }

    [Fact]
    public async Task ToggleStatusAsync_ShouldToggleIsActive()
    {
        await SeedSector(SectorId, "Secteur Nord", "SN", isActive: true);

        var result = await _sut.ToggleStatusAsync(SectorId, TenantId);

        result.Should().NotBeNull();
        result!.IsActive.Should().BeFalse();

        var toggled = await _dbContext.Sectors.FindAsync(SectorId);
        toggled!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleStatusAsync_ShouldReturnNull_WhenNotFound()
    {
        var result = await _sut.ToggleStatusAsync(Guid.NewGuid(), TenantId);

        result.Should().BeNull();
    }

    private async Task SeedSector(Guid id, string name, string code, bool isActive = true, Guid? tenantId = null, Guid? distributorId = null)
    {
        _dbContext.Sectors.Add(new Sector
        {
            Id = id,
            Name = name,
            Code = code,
            IsActive = isActive,
            DistributorId = distributorId ?? DistributorId,
            TenantId = tenantId ?? TenantId,
        });
        await _dbContext.SaveChangesAsync();
    }
}
