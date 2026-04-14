using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.DTOs.SamplingPlans;
using AquaPlan.Application.Services.Interfaces;
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
    private readonly Mock<IOrderService> _orderServiceMock = new();
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
        _sut = new SamplingPlanService(_dbContext, _orderServiceMock.Object, _loggerMock.Object);

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

    private async Task<Guid> CreateValidatedPlanWithItems(List<int> plannedMonths)
    {
        var plan = new SamplingPlan
        {
            Id = Guid.NewGuid(),
            Year = 2026,
            Status = SamplingPlanStatus.Validated,
            DistributorId = DistributorId,
            CreatedById = UserId,
            TenantId = TenantId,
            StatusChangedAt = DateTime.UtcNow,
            StatusChangedBy = UserId,
        };

        plan.Items.Add(new SamplingPlanItem
        {
            Id = Guid.NewGuid(),
            SamplingPlanId = plan.Id,
            SamplingLocationId = SamplingLocationId,
            AnalysisProfileId = AnalysisProfileId,
            FrequencyPerYear = plannedMonths.Count,
            PlannedMonths = plannedMonths,
        });

        _dbContext.SamplingPlans.Add(plan);
        await _dbContext.SaveChangesAsync();
        return plan.Id;
    }

    [Fact]
    public async Task GenerateOrdersFromPlanAsync_ShouldCreateOrdersForEachPlannedMonth()
    {
        var planId = await CreateValidatedPlanWithItems([1, 4, 7, 10]);
        var orderCounter = 0;

        _orderServiceMock
            .Setup(s => s.CreateOrderAsync(It.IsAny<OrderCreateDto>(), UserId, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((OrderCreateDto dto, string _, Guid _, CancellationToken _) =>
            {
                orderCounter++;
                return CreateFakeOrderDetailDto(Guid.NewGuid(), $"ORD-{orderCounter:D4}", dto);
            });

        var result = await _sut.GenerateOrdersFromPlanAsync(planId, UserId, TenantId);

        result.OrdersCreated.Should().Be(4);
        result.Orders.Should().HaveCount(4);
        _orderServiceMock.Verify(s => s.CreateOrderAsync(It.IsAny<OrderCreateDto>(), UserId, TenantId, It.IsAny<CancellationToken>()), Times.Exactly(4));
    }

    [Fact]
    public async Task GenerateOrdersFromPlanAsync_ShouldSetCorrectPlannedDates()
    {
        var planId = await CreateValidatedPlanWithItems([3, 6]);
        var createdDtos = new List<OrderCreateDto>();

        _orderServiceMock
            .Setup(s => s.CreateOrderAsync(It.IsAny<OrderCreateDto>(), UserId, TenantId, It.IsAny<CancellationToken>()))
            .Callback<OrderCreateDto, string, Guid, CancellationToken>((dto, _, _, _) => createdDtos.Add(dto))
            .ReturnsAsync((OrderCreateDto dto, string _, Guid _, CancellationToken _) =>
                CreateFakeOrderDetailDto(Guid.NewGuid(), "ORD-0001", dto));

        await _sut.GenerateOrdersFromPlanAsync(planId, UserId, TenantId);

        createdDtos.Should().HaveCount(2);
        createdDtos[0].PlannedDate.Should().Be(new DateTime(2026, 3, 1, 0, 0, 0, DateTimeKind.Utc));
        createdDtos[1].PlannedDate.Should().Be(new DateTime(2026, 6, 1, 0, 0, 0, DateTimeKind.Utc));
        createdDtos.Should().AllSatisfy(d => d.IsUnplanned.Should().BeFalse());
    }

    [Fact]
    public async Task GenerateOrdersFromPlanAsync_WhenPlanNotValidated_ShouldThrow()
    {
        var dto = new SamplingPlanCreateDto(DistributorId, 2027, null,
            [new SamplingPlanItemCreateDto(SamplingLocationId, AnalysisProfileId, 1, [1])]);
        var plan = await _sut.CreatePlanAsync(dto, UserId, TenantId);

        await _sut.Invoking(s => s.GenerateOrdersFromPlanAsync(plan.Id, UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Validated*");
    }

    [Fact]
    public async Task GenerateOrdersFromPlanAsync_WhenPlanNotFound_ShouldThrow()
    {
        await _sut.Invoking(s => s.GenerateOrdersFromPlanAsync(Guid.NewGuid(), UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not found*");
    }

    private static OrderDetailDto CreateFakeOrderDetailDto(Guid id, string orderNumber, OrderCreateDto dto)
    {
        return new OrderDetailDto(
            Id: id,
            OrderNumber: orderNumber,
            Status: OrderStatus.New,
            IsUnplanned: false,
            UnplannedReason: null,
            UnplannedReasonDetails: null,
            CreatedById: UserId,
            CreatedByName: "User",
            PreleveurId: null,
            PreleveurName: null,
            DistributorId: dto.DistributorId,
            DistributorName: "Test Distributor",
            SamplingLocationId: dto.SamplingLocationId,
            SamplingLocationName: "Source A",
            PlannedDate: dto.PlannedDate,
            Notes: dto.Notes,
            IsDelegated: false,
            AnalysisProfiles: [],
            TenantId: TenantId,
            CreatedAt: DateTime.UtcNow,
            UpdatedAt: null,
            Sampling: null,
            SamplingRoundId: null);
    }
}
