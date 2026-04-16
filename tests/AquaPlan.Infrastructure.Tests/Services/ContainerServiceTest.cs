using AquaPlan.Application.DTOs.Containers;
using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Services;

public class ContainerServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<ILogger<ContainerService>> _loggerMock = new();
    private readonly ContainerService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public ContainerServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AquaPlanDbContext(options);
        _sut = new ContainerService(_dbContext, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnContainersForTenant()
    {
        await SeedContainers();

        var result = await _sut.GetAllAsync(TenantId, null);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(c => c.Name.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public async Task GetAllAsync_ShouldNotReturnContainersFromOtherTenant()
    {
        await SeedContainers();

        var result = await _sut.GetAllAsync(OtherTenantId, null);

        result.Should().HaveCount(1);
        result[0].Code.Should().Be("CHEM-PET500");
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterBySearch()
    {
        await SeedContainers();

        var filter = new ContainerFilteringInputDto("bact", null);
        var result = await _sut.GetAllAsync(TenantId, filter);

        result.Should().HaveCount(1);
        result[0].Code.Should().Be("BACT-V250");
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByIsActive()
    {
        await SeedContainers();
        var inactive = new Container
        {
            Id = Guid.NewGuid(),
            Code = "OLD-C",
            Name = "Old container",
            Material = "Verre",
            VolumeMl = 100,
            Color = "Transparent",
            IsActive = false,
            TenantId = TenantId,
        };
        _dbContext.Containers.Add(inactive);
        await _dbContext.SaveChangesAsync();

        var filter = new ContainerFilteringInputDto(null, false);
        var result = await _sut.GetAllAsync(TenantId, filter);

        result.Should().HaveCount(1);
        result[0].Code.Should().Be("OLD-C");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnOrderedByCode()
    {
        await SeedContainers();

        var result = await _sut.GetAllAsync(TenantId, null);

        result.Should().BeInAscendingOrder(c => c.Code);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnContainer()
    {
        var containers = await SeedContainers();
        var id = containers[0].Id;

        var result = await _sut.GetByIdAsync(id, TenantId);

        result.Should().NotBeNull();
        result!.Code.Should().Be("BACT-V250");
        result.Material.Should().Be("Verre borosilicaté");
        result.VolumeMl.Should().Be(250);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ShouldReturnNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WhenDifferentTenant_ShouldReturnNull()
    {
        var containers = await SeedContainers();
        var id = containers[0].Id;

        var result = await _sut.GetByIdAsync(id, OtherTenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ShouldAddAndReturnContainer()
    {
        var dto = new ContainerAddDto("NEW-C", "Nouveau contenant", "Verre", 500, "Transparent");

        var result = await _sut.CreateAsync(dto, TenantId);

        result.Should().NotBeNull();
        result.Code.Should().Be("NEW-C");
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistContainer()
    {
        var dto = new ContainerAddDto("NEW-C", "Nouveau contenant", "Verre", 500, "Transparent");

        var result = await _sut.CreateAsync(dto, TenantId);

        var persisted = await _dbContext.Containers.FindAsync(result.Id);
        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(TenantId);
    }

    [Fact]
    public async Task CreateAsync_WhenCodeAlreadyExistsForTenant_ShouldThrow()
    {
        await SeedContainers();
        var dto = new ContainerAddDto("BACT-V250", "Dupe", "Verre", 250, "Transparent");

        await _sut.Awaiting(s => s.CreateAsync(dto, TenantId))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task CreateAsync_WhenCodeExistsForOtherTenant_ShouldSucceed()
    {
        // Seed only for OtherTenantId, so TenantId is free for the same code.
        _dbContext.Containers.Add(new Container
        {
            Id = Guid.NewGuid(),
            Code = "CHEM-PET500",
            Name = "Bouteille PET chimie (autre tenant)",
            Material = "PET",
            VolumeMl = 500,
            Color = "Transparent",
            IsActive = true,
            TenantId = OtherTenantId,
        });
        await _dbContext.SaveChangesAsync();
        var dto = new ContainerAddDto("CHEM-PET500", "Same code other tenant", "PET", 500, "Transparent");

        var result = await _sut.CreateAsync(dto, TenantId);

        result.Code.Should().Be("CHEM-PET500");
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateAndReturnContainer()
    {
        var containers = await SeedContainers();
        var id = containers[0].Id;
        var dto = new ContainerUpdateDto("BACT-V250", "Nouveau nom", "Verre", 300, "Ambré", false);

        var result = await _sut.UpdateAsync(id, dto, TenantId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Nouveau nom");
        result.VolumeMl.Should().Be(300);
        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_WhenCodeChangeConflictsWithOtherContainer_ShouldThrow()
    {
        var containers = await SeedContainers();
        var firstId = containers.First(c => c.TenantId == TenantId && c.Code == "BACT-V250").Id;
        var dto = new ContainerUpdateDto("CHEM-PET500", "Renamed", "Verre", 250, "Transparent", true);

        await _sut.Awaiting(s => s.UpdateAsync(firstId, dto, TenantId))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task UpdateAsync_WhenNotFound_ShouldReturnNull()
    {
        var dto = new ContainerUpdateDto("ANY", "Any", "Verre", 100, "Transparent", true);

        var result = await _sut.UpdateAsync(Guid.NewGuid(), dto, TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WhenDifferentTenant_ShouldReturnNull()
    {
        var containers = await SeedContainers();
        var id = containers[0].Id;
        var dto = new ContainerUpdateDto("BACT-V250", "Renamed", "Verre", 250, "Transparent", true);

        var result = await _sut.UpdateAsync(id, dto, OtherTenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ToggleStatusAsync_ShouldToggleAndReturn()
    {
        var containers = await SeedContainers();
        var id = containers[0].Id;

        var result = await _sut.ToggleStatusAsync(id, TenantId);

        result.Should().NotBeNull();
        result!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleStatusAsync_WhenNotFound_ShouldReturnNull()
    {
        var result = await _sut.ToggleStatusAsync(Guid.NewGuid(), TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ToggleStatusAsync_WhenDifferentTenant_ShouldReturnNull()
    {
        var containers = await SeedContainers();
        var id = containers[0].Id;

        var result = await _sut.ToggleStatusAsync(id, OtherTenantId);

        result.Should().BeNull();
    }

    private async Task<List<Container>> SeedContainers()
    {
        var containers = new List<Container>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Code = "BACT-V250",
                Name = "Bouteille verre stérile microbiologie",
                Material = "Verre borosilicaté",
                VolumeMl = 250,
                Color = "Transparent",
                IsActive = true,
                TenantId = TenantId,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "CHEM-PET500",
                Name = "Bouteille PET chimie",
                Material = "PET",
                VolumeMl = 500,
                Color = "Transparent",
                IsActive = true,
                TenantId = TenantId,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "CHEM-PET500",
                Name = "Bouteille PET chimie (autre tenant)",
                Material = "PET",
                VolumeMl = 500,
                Color = "Transparent",
                IsActive = true,
                TenantId = OtherTenantId,
            },
        };

        _dbContext.Containers.AddRange(containers);
        await _dbContext.SaveChangesAsync();

        return containers;
    }
}
