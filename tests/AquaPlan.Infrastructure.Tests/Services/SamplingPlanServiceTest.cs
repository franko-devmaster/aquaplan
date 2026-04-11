using AquaPlan.Application.DTOs.SamplingPlans;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Services;

public class SamplingPlanServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<ILogger<SamplingPlanService>> _loggerMock = new();
    private readonly SamplingPlanService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid SamplingLocationId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private static readonly Guid AnalysisProfileId = Guid.Parse("00000000-0000-0000-0000-000000000030");
    private const string UserId = "user-1";

    public SamplingPlanServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AquaPlanDbContext(options);
        _sut = new SamplingPlanService(_dbContext, _loggerMock.Object);

        SeedData().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CreatePlanAsync_ShouldCreateDraftPlan()
    {
        var dto = new SamplingPlanCreateDto(
            DistributorId: DistributorId,
            Year: 2026,
            Notes: "Annual plan",
            Items: [
                new SamplingPlanItemCreateDto(SamplingLocationId, AnalysisProfileId, 4, [1, 4, 7, 10])
            ]);

        var result = await _sut.CreatePlanAsync(dto, UserId, TenantId);

        result.Status.Should().Be(SamplingPlanStatus.Draft);
        result.Year.Should().Be(2026);
        result.Notes.Should().Be("Annual plan");
        result.Items.Should().HaveCount(1);
        result.Items[0].FrequencyPerYear.Should().Be(4);
        result.Items[0].PlannedMonths.Should().BeEquivalentTo([1, 4, 7, 10]);
    }

    [Fact]
    public async Task CreatePlanAsync_ShouldThrow_WhenDuplicateDistributorYear()
    {
        var dto = new SamplingPlanCreateDto(DistributorId, 2026, null, []);
        await _sut.CreatePlanAsync(dto, UserId, TenantId);

        await _sut.Awaiting(s => s.CreatePlanAsync(dto, UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task UpdatePlanAsync_ShouldReplaceItems()
    {
        var plan = await CreateSeedPlan();

        var updateDto = new SamplingPlanUpdateDto(
            Notes: "Updated notes",
            Items: [
                new SamplingPlanItemCreateDto(SamplingLocationId, AnalysisProfileId, 12, [1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12])
            ]);

        var result = await _sut.UpdatePlanAsync(plan.Id, updateDto, UserId, TenantId);

        result.Should().NotBeNull();
        result!.Notes.Should().Be("Updated notes");
        result.Items.Should().HaveCount(1);
        result.Items[0].FrequencyPerYear.Should().Be(12);
    }

    [Fact]
    public async Task UpdatePlanAsync_ShouldThrow_WhenStatusNotDraft()
    {
        var plan = await CreateSeedPlan();
        await _sut.SubmitPlanAsync(plan.Id, UserId, TenantId);

        var updateDto = new SamplingPlanUpdateDto("notes", []);

        await _sut.Awaiting(s => s.UpdatePlanAsync(plan.Id, updateDto, UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Draft*");
    }

    [Fact]
    public async Task DeletePlanAsync_ShouldSucceed_WhenDraft()
    {
        var plan = await CreateSeedPlan();

        var result = await _sut.DeletePlanAsync(plan.Id, UserId, TenantId);

        result.Should().BeTrue();
        var deleted = await _sut.GetPlanByIdAsync(plan.Id, TenantId);
        deleted.Should().BeNull();
    }

    [Fact]
    public async Task DeletePlanAsync_ShouldThrow_WhenSubmitted()
    {
        var plan = await CreateSeedPlan();
        await _sut.SubmitPlanAsync(plan.Id, UserId, TenantId);

        await _sut.Awaiting(s => s.DeletePlanAsync(plan.Id, UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Draft*");
    }

    [Fact]
    public async Task SubmitPlanAsync_ShouldTransitionToSubmitted()
    {
        var plan = await CreateSeedPlan();

        var result = await _sut.SubmitPlanAsync(plan.Id, UserId, TenantId);

        result.Should().NotBeNull();
        result!.Status.Should().Be(SamplingPlanStatus.Submitted);
    }

    [Fact]
    public async Task SubmitPlanAsync_ShouldThrow_WhenNotDraft()
    {
        var plan = await CreateSeedPlan();
        await _sut.SubmitPlanAsync(plan.Id, UserId, TenantId);

        await _sut.Awaiting(s => s.SubmitPlanAsync(plan.Id, UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Draft*");
    }

    [Fact]
    public async Task SubmitPlanAsync_ShouldThrow_WhenEmpty()
    {
        var dto = new SamplingPlanCreateDto(DistributorId, 2027, null, []);
        var plan = await _sut.CreatePlanAsync(dto, UserId, TenantId);

        await _sut.Awaiting(s => s.SubmitPlanAsync(plan.Id, UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*empty*");
    }

    [Fact]
    public async Task ValidatePlanAsync_ShouldTransitionToValidated()
    {
        var plan = await CreateSeedPlan();
        await _sut.SubmitPlanAsync(plan.Id, UserId, TenantId);

        var result = await _sut.ValidatePlanAsync(plan.Id, UserId, TenantId);

        result.Should().NotBeNull();
        result!.Status.Should().Be(SamplingPlanStatus.Validated);
    }

    [Fact]
    public async Task ValidatePlanAsync_ShouldThrow_WhenNotSubmitted()
    {
        var plan = await CreateSeedPlan();

        await _sut.Awaiting(s => s.ValidatePlanAsync(plan.Id, UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Submitted*");
    }

    [Fact]
    public async Task RejectPlanAsync_ShouldTransitionToRejected_WithReason()
    {
        var plan = await CreateSeedPlan();
        await _sut.SubmitPlanAsync(plan.Id, UserId, TenantId);

        var result = await _sut.RejectPlanAsync(plan.Id, "Missing locations", UserId, TenantId);

        result.Should().NotBeNull();
        result!.Status.Should().Be(SamplingPlanStatus.Rejected);
        result.RejectionReason.Should().Be("Missing locations");
    }

    [Fact]
    public async Task RejectPlanAsync_ShouldThrow_WhenNotSubmitted()
    {
        var plan = await CreateSeedPlan();

        await _sut.Awaiting(s => s.RejectPlanAsync(plan.Id, "reason", UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Submitted*");
    }

    [Fact]
    public async Task GetPlansFilteredAsync_ShouldFilterByYear()
    {
        await _sut.CreatePlanAsync(new SamplingPlanCreateDto(DistributorId, 2025, null, []), UserId, TenantId);
        await _sut.CreatePlanAsync(new SamplingPlanCreateDto(DistributorId, 2026, null, []), UserId, TenantId);

        var filter = new SamplingPlanFilterDto(Year: 2026);
        var result = await _sut.GetPlansFilteredAsync(UserId, TenantId, filter, isAdmin: true);

        result.Items.Should().HaveCount(1);
        result.Items[0].Year.Should().Be(2026);
    }

    [Fact]
    public async Task GetPlansFilteredAsync_ShouldFilterByStatus()
    {
        var plan = await CreateSeedPlan();
        await _sut.SubmitPlanAsync(plan.Id, UserId, TenantId);

        var filter = new SamplingPlanFilterDto(Statuses: [SamplingPlanStatus.Submitted]);
        var result = await _sut.GetPlansFilteredAsync(UserId, TenantId, filter, isAdmin: true);

        result.Items.Should().AllSatisfy(p => p.Status.Should().Be(SamplingPlanStatus.Submitted));
    }

    [Fact]
    public async Task UserHasDistributorAccessAsync_ShouldReturnTrue_WhenUserHasAccess()
    {
        var result = await _sut.UserHasDistributorAccessAsync(UserId, DistributorId);

        result.Should().BeTrue();
    }

    [Fact]
    public async Task UserHasDistributorAccessAsync_ShouldReturnFalse_WhenNoAccess()
    {
        var result = await _sut.UserHasDistributorAccessAsync(UserId, Guid.NewGuid());

        result.Should().BeFalse();
    }

    private async Task<SamplingPlanDetailDto> CreateSeedPlan()
    {
        var dto = new SamplingPlanCreateDto(
            DistributorId: DistributorId,
            Year: 2026,
            Notes: null,
            Items: [
                new SamplingPlanItemCreateDto(SamplingLocationId, AnalysisProfileId, 4, [1, 4, 7, 10])
            ]);

        return await _sut.CreatePlanAsync(dto, UserId, TenantId);
    }

    private async Task SeedData()
    {
        _dbContext.Users.Add(new AppUser
        {
            Id = UserId,
            UserName = "user1@test.com",
            Email = "user1@test.com",
            FirstName = "John",
            LastName = "Doe",
            TenantId = TenantId,
            IsActive = true,
        });

        _dbContext.Distributors.Add(new Distributor
        {
            Id = DistributorId,
            Name = "Test Distributor",
            TenantId = TenantId,
            IsActive = true,
        });

        _dbContext.UserDistributors.Add(new UserDistributor
        {
            UserId = UserId,
            DistributorId = DistributorId,
        });

        _dbContext.SamplingLocations.Add(new SamplingLocation
        {
            Id = SamplingLocationId,
            Name = "Source A",
            LocationCode = "SRC-A",
            DistributorId = DistributorId,
            IsActive = true,
        });

        _dbContext.AnalysisProfiles.Add(new AnalysisProfile
        {
            Id = AnalysisProfileId,
            Code = "MICRO-01",
            Name = "Microbiologie standard",
            IsActive = true,
        });

        await _dbContext.SaveChangesAsync();
    }
}
