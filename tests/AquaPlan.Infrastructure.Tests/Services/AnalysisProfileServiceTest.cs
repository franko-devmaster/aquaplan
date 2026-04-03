using AquaPlan.Application.DTOs.AnalysisProfiles;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Services;

public class AnalysisProfileServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<ILogger<AnalysisProfileService>> _loggerMock = new();
    private readonly AnalysisProfileService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public AnalysisProfileServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AquaPlanDbContext(options);
        _sut = new AnalysisProfileService(_dbContext, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnFilteredProfiles()
    {
        await SeedProfiles();

        var result = await _sut.GetAllAsync(TenantId, null);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(p => p.Name.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public async Task GetAllAsync_ShouldNotReturnProfilesFromOtherTenant()
    {
        await SeedProfiles();

        var result = await _sut.GetAllAsync(OtherTenantId, null);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Profil Autre");
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterBySearch()
    {
        await SeedProfiles();

        var filter = new AnalysisProfileFilteringInputDto("Bact", null, null);
        var result = await _sut.GetAllAsync(TenantId, filter);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Analyse Bactériologique");
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByCategory()
    {
        await SeedProfiles();

        var filter = new AnalysisProfileFilteringInputDto(null, AnalysisCategory.Chemistry, null);
        var result = await _sut.GetAllAsync(TenantId, filter);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Analyse Chimique");
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByIsActive()
    {
        await SeedProfiles();

        var filter = new AnalysisProfileFilteringInputDto(null, null, false);
        var result = await _sut.GetAllAsync(TenantId, filter);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnOrderedByCode()
    {
        await SeedProfiles();

        var result = await _sut.GetAllAsync(TenantId, null);

        result.Should().BeInAscendingOrder(p => p.Code);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnProfile()
    {
        var profiles = await SeedProfiles();
        var profileId = profiles[0].Id;

        var result = await _sut.GetByIdAsync(profileId, TenantId);

        result.Should().NotBeNull();
        result!.Code.Should().Be("BACT-01");
        result.Name.Should().Be("Analyse Bactériologique");
        result.Category.Should().Be(AnalysisCategory.Bacteriology);
        result.IsActive.Should().BeTrue();
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
        var profiles = await SeedProfiles();
        var profileId = profiles[0].Id;

        var result = await _sut.GetByIdAsync(profileId, OtherTenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task CreateAsync_ShouldAddAndReturnProfile()
    {
        var dto = new AnalysisProfileAddDto("PHYS-01", "Analyse Physique", "Description physique", AnalysisCategory.Physical);

        var result = await _sut.CreateAsync(dto, TenantId);

        result.Should().NotBeNull();
        result.Code.Should().Be("PHYS-01");
        result.Name.Should().Be("Analyse Physique");
        result.Description.Should().Be("Description physique");
        result.Category.Should().Be(AnalysisCategory.Physical);
        result.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistProfile()
    {
        var dto = new AnalysisProfileAddDto("PHYS-01", "Analyse Physique", null, AnalysisCategory.Physical);

        var result = await _sut.CreateAsync(dto, TenantId);

        var persisted = await _dbContext.AnalysisProfiles.FindAsync(result.Id);
        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(TenantId);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateAndReturnProfile()
    {
        var profiles = await SeedProfiles();
        var profileId = profiles[0].Id;
        var dto = new AnalysisProfileUpdateDto("BACT-UPD", "Nom Modifié", "Nouvelle description", AnalysisCategory.Chemistry, false);

        var result = await _sut.UpdateAsync(profileId, dto, TenantId);

        result.Should().NotBeNull();
        result!.Code.Should().Be("BACT-UPD");
        result.Name.Should().Be("Nom Modifié");
        result.Description.Should().Be("Nouvelle description");
        result.Category.Should().Be(AnalysisCategory.Chemistry);
        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_WhenNotFound_ShouldReturnNull()
    {
        var dto = new AnalysisProfileUpdateDto("CODE", "Name", null, AnalysisCategory.Other, true);

        var result = await _sut.UpdateAsync(Guid.NewGuid(), dto, TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WhenDifferentTenant_ShouldReturnNull()
    {
        var profiles = await SeedProfiles();
        var profileId = profiles[0].Id;
        var dto = new AnalysisProfileUpdateDto("CODE", "Name", null, AnalysisCategory.Other, true);

        var result = await _sut.UpdateAsync(profileId, dto, OtherTenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ToggleStatusAsync_ShouldToggleAndReturnProfile()
    {
        var profiles = await SeedProfiles();
        var profileId = profiles[0].Id;

        var result = await _sut.ToggleStatusAsync(profileId, TenantId);

        result.Should().NotBeNull();
        result!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleStatusAsync_ShouldActivateInactiveProfile()
    {
        var profile = new AnalysisProfile
        {
            Id = Guid.NewGuid(),
            Code = "INACT-01",
            Name = "Inactif",
            Category = AnalysisCategory.Other,
            IsActive = false,
            TenantId = TenantId,
        };
        _dbContext.AnalysisProfiles.Add(profile);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.ToggleStatusAsync(profile.Id, TenantId);

        result.Should().NotBeNull();
        result!.IsActive.Should().BeTrue();
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
        var profiles = await SeedProfiles();
        var profileId = profiles[0].Id;

        var result = await _sut.ToggleStatusAsync(profileId, OtherTenantId);

        result.Should().BeNull();
    }

    private async Task<List<AnalysisProfile>> SeedProfiles()
    {
        var profiles = new List<AnalysisProfile>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Code = "BACT-01",
                Name = "Analyse Bactériologique",
                Description = "Analyse bactériologique standard",
                Category = AnalysisCategory.Bacteriology,
                IsActive = true,
                TenantId = TenantId,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "CHIM-01",
                Name = "Analyse Chimique",
                Description = "Analyse chimique complète",
                Category = AnalysisCategory.Chemistry,
                IsActive = true,
                TenantId = TenantId,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "OTHER-01",
                Name = "Profil Autre",
                Description = null,
                Category = AnalysisCategory.Other,
                IsActive = true,
                TenantId = OtherTenantId,
            },
        };

        _dbContext.AnalysisProfiles.AddRange(profiles);
        await _dbContext.SaveChangesAsync();

        return profiles;
    }
}
