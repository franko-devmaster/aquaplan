using AquaPlan.Application.DTOs.AnalysisPrograms;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Services;

public class AnalysisProgramServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<ILogger<AnalysisProgramService>> _loggerMock = new();
    private readonly AnalysisProgramService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public AnalysisProgramServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AquaPlanDbContext(options);
        _sut = new AnalysisProgramService(_dbContext, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnFilteredPrograms()
    {
        await SeedPrograms();

        var result = await _sut.GetAllAsync(TenantId, null);

        result.Should().HaveCount(2);
        result.Should().AllSatisfy(p => p.Name.Should().NotBeNullOrEmpty());
    }

    [Fact]
    public async Task GetAllAsync_ShouldNotReturnProgramsFromOtherTenant()
    {
        await SeedPrograms();

        var result = await _sut.GetAllAsync(OtherTenantId, null);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Programme Autre");
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterBySearch()
    {
        await SeedPrograms();

        var filter = new AnalysisProgramFilteringInputDto("Potable", null);
        var result = await _sut.GetAllAsync(TenantId, filter);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Eau Potable Standard");
    }

    [Fact]
    public async Task GetAllAsync_ShouldFilterByIsActive()
    {
        await SeedPrograms();

        var filter = new AnalysisProgramFilteringInputDto(null, false);
        var result = await _sut.GetAllAsync(TenantId, filter);

        result.Should().HaveCount(1);
        result[0].Name.Should().Be("Programme Inactif");
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnOrderedByCode()
    {
        await SeedPrograms();

        var result = await _sut.GetAllAsync(TenantId, null);

        result.Should().BeInAscendingOrder(p => p.Code);
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnProfileCount()
    {
        var (programs, _) = await SeedProgramsWithProfiles();

        var result = await _sut.GetAllAsync(TenantId, null);

        var programWithProfiles = result.Single(p => p.Code == "EP-01");
        programWithProfiles.ProfileCount.Should().Be(2);
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnProgram()
    {
        var programs = await SeedPrograms();
        var programId = programs[0].Id;

        var result = await _sut.GetByIdAsync(programId, TenantId);

        result.Should().NotBeNull();
        result!.Code.Should().Be("EP-01");
        result.Name.Should().Be("Eau Potable Standard");
        result.IsActive.Should().BeTrue();
        result.Profiles.Should().NotBeNull();
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
        var programs = await SeedPrograms();
        var programId = programs[0].Id;

        var result = await _sut.GetByIdAsync(programId, OtherTenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_ShouldReturnProfilesOrderedByCode()
    {
        var (programs, _) = await SeedProgramsWithProfiles();
        var programId = programs[0].Id;

        var result = await _sut.GetByIdAsync(programId, TenantId);

        result.Should().NotBeNull();
        result!.Profiles.Should().HaveCount(2);
        result.Profiles.Should().BeInAscendingOrder(p => p.Code);
    }

    [Fact]
    public async Task CreateAsync_ShouldAddAndReturnProgram()
    {
        var dto = new AnalysisProgramAddDto("BAIG-01", "Programme Baignade", "Description baignade");

        var result = await _sut.CreateAsync(dto, TenantId);

        result.Should().NotBeNull();
        result.Code.Should().Be("BAIG-01");
        result.Name.Should().Be("Programme Baignade");
        result.Description.Should().Be("Description baignade");
        result.IsActive.Should().BeTrue();
        result.Profiles.Should().BeEmpty();
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistProgram()
    {
        var dto = new AnalysisProgramAddDto("BAIG-01", "Programme Baignade", null);

        var result = await _sut.CreateAsync(dto, TenantId);

        var persisted = await _dbContext.AnalysisPrograms.FindAsync(result.Id);
        persisted.Should().NotBeNull();
        persisted!.TenantId.Should().Be(TenantId);
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateAndReturnProgram()
    {
        var programs = await SeedPrograms();
        var programId = programs[0].Id;
        var dto = new AnalysisProgramUpdateDto("EP-UPD", "Nom Modifié", "Nouvelle description", false);

        var result = await _sut.UpdateAsync(programId, dto, TenantId);

        result.Should().NotBeNull();
        result!.Code.Should().Be("EP-UPD");
        result.Name.Should().Be("Nom Modifié");
        result.Description.Should().Be("Nouvelle description");
        result.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task UpdateAsync_WhenNotFound_ShouldReturnNull()
    {
        var dto = new AnalysisProgramUpdateDto("CODE", "Name", null, true);

        var result = await _sut.UpdateAsync(Guid.NewGuid(), dto, TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WhenDifferentTenant_ShouldReturnNull()
    {
        var programs = await SeedPrograms();
        var programId = programs[0].Id;
        var dto = new AnalysisProgramUpdateDto("CODE", "Name", null, true);

        var result = await _sut.UpdateAsync(programId, dto, OtherTenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task ToggleStatusAsync_ShouldToggleAndReturnProgram()
    {
        var programs = await SeedPrograms();
        var programId = programs[0].Id;

        var result = await _sut.ToggleStatusAsync(programId, TenantId);

        result.Should().NotBeNull();
        result!.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task ToggleStatusAsync_ShouldActivateInactiveProgram()
    {
        var programs = await SeedPrograms();
        var inactiveProgramId = programs[1].Id;

        var result = await _sut.ToggleStatusAsync(inactiveProgramId, TenantId);

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
        var programs = await SeedPrograms();
        var programId = programs[0].Id;

        var result = await _sut.ToggleStatusAsync(programId, OtherTenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AddProfilesAsync_ShouldAddAndReturnProgram()
    {
        var (programs, profiles) = await SeedProgramsWithProfiles();
        var emptyProgram = programs[1];

        var profileIds = profiles.Select(p => p.Id).ToList();
        var result = await _sut.AddProfilesAsync(emptyProgram.Id, profileIds, TenantId);

        result.Should().NotBeNull();
        result!.Profiles.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddProfilesAsync_ShouldNotDuplicateExistingProfiles()
    {
        var (programs, profiles) = await SeedProgramsWithProfiles();
        var programWithProfiles = programs[0];

        var profileIds = profiles.Select(p => p.Id).ToList();
        var result = await _sut.AddProfilesAsync(programWithProfiles.Id, profileIds, TenantId);

        result.Should().NotBeNull();
        result!.Profiles.Should().HaveCount(2);
    }

    [Fact]
    public async Task AddProfilesAsync_ShouldIgnoreProfilesFromOtherTenant()
    {
        var (programs, _) = await SeedProgramsWithProfiles();
        var emptyProgram = programs[1];

        var otherTenantProfile = new AnalysisProfile
        {
            Id = Guid.NewGuid(),
            Code = "OTHER-PROF",
            Name = "Profil Autre Tenant",
            Category = AnalysisCategory.Other,
            IsActive = true,
            TenantId = OtherTenantId,
        };
        _dbContext.AnalysisProfiles.Add(otherTenantProfile);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.AddProfilesAsync(emptyProgram.Id, [otherTenantProfile.Id], TenantId);

        result.Should().NotBeNull();
        result!.Profiles.Should().BeEmpty();
    }

    [Fact]
    public async Task AddProfilesAsync_WhenNotFound_ShouldReturnNull()
    {
        var result = await _sut.AddProfilesAsync(Guid.NewGuid(), [Guid.NewGuid()], TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task AddProfilesAsync_WhenDifferentTenant_ShouldReturnNull()
    {
        var (programs, profiles) = await SeedProgramsWithProfiles();
        var programId = programs[0].Id;

        var profileIds = profiles.Select(p => p.Id).ToList();
        var result = await _sut.AddProfilesAsync(programId, profileIds, OtherTenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task RemoveProfileAsync_ShouldRemoveAndReturnTrue()
    {
        var (programs, profiles) = await SeedProgramsWithProfiles();
        var programId = programs[0].Id;
        var profileId = profiles[0].Id;

        var result = await _sut.RemoveProfileAsync(programId, profileId, TenantId);

        result.Should().BeTrue();

        var link = await _dbContext.AnalysisProgramProfiles
            .Where(pp => pp.AnalysisProgramId == programId && pp.AnalysisProfileId == profileId)
            .FirstOrDefaultAsync();
        link.Should().BeNull();
    }

    [Fact]
    public async Task RemoveProfileAsync_WhenProgramNotFound_ShouldReturnFalse()
    {
        var result = await _sut.RemoveProfileAsync(Guid.NewGuid(), Guid.NewGuid(), TenantId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveProfileAsync_WhenProfileLinkNotFound_ShouldReturnFalse()
    {
        var programs = await SeedPrograms();
        var programId = programs[0].Id;

        var result = await _sut.RemoveProfileAsync(programId, Guid.NewGuid(), TenantId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveProfileAsync_WhenDifferentTenant_ShouldReturnFalse()
    {
        var (programs, profiles) = await SeedProgramsWithProfiles();
        var programId = programs[0].Id;
        var profileId = profiles[0].Id;

        var result = await _sut.RemoveProfileAsync(programId, profileId, OtherTenantId);

        result.Should().BeFalse();
    }

    private async Task<List<AnalysisProgram>> SeedPrograms()
    {
        var programs = new List<AnalysisProgram>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Code = "EP-01",
                Name = "Eau Potable Standard",
                Description = "Programme standard eau potable",
                IsActive = true,
                TenantId = TenantId,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "EP-02",
                Name = "Programme Inactif",
                Description = null,
                IsActive = false,
                TenantId = TenantId,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "OTHER-01",
                Name = "Programme Autre",
                Description = null,
                IsActive = true,
                TenantId = OtherTenantId,
            },
        };

        _dbContext.AnalysisPrograms.AddRange(programs);
        await _dbContext.SaveChangesAsync();

        return programs;
    }

    private async Task<(List<AnalysisProgram> Programs, List<AnalysisProfile> Profiles)> SeedProgramsWithProfiles()
    {
        var profiles = new List<AnalysisProfile>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Code = "BACT-01",
                Name = "Analyse Bactériologique",
                Category = AnalysisCategory.Bacteriology,
                IsActive = true,
                TenantId = TenantId,
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "CHIM-01",
                Name = "Analyse Chimique",
                Category = AnalysisCategory.Chemistry,
                IsActive = true,
                TenantId = TenantId,
            },
        };

        _dbContext.AnalysisProfiles.AddRange(profiles);
        await _dbContext.SaveChangesAsync();

        var programs = new List<AnalysisProgram>
        {
            new()
            {
                Id = Guid.NewGuid(),
                Code = "EP-01",
                Name = "Eau Potable Standard",
                Description = "Programme standard eau potable",
                IsActive = true,
                TenantId = TenantId,
                AnalysisProgramProfiles = new List<AnalysisProgramProfile>
                {
                    new() { AnalysisProfileId = profiles[0].Id },
                    new() { AnalysisProfileId = profiles[1].Id },
                },
            },
            new()
            {
                Id = Guid.NewGuid(),
                Code = "EP-02",
                Name = "Programme Vide",
                Description = null,
                IsActive = true,
                TenantId = TenantId,
            },
        };

        _dbContext.AnalysisPrograms.AddRange(programs);
        await _dbContext.SaveChangesAsync();

        return (programs, profiles);
    }
}
