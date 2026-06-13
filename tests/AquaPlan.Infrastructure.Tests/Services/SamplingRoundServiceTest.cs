using AquaPlan.Application.DTOs.MockLims;
using AquaPlan.Application.DTOs.SamplingRounds;
using AquaPlan.Application.Exceptions;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AquaPlan.Infrastructure.Tests.Services;

public class SamplingRoundServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<IDelegationService> _delegationServiceMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<IMockLimsService> _mockLimsServiceMock = new();
    private readonly Mock<ILogger<SamplingRoundService>> _loggerMock = new();
    private readonly Mock<ILogger<OrderTransmissionService>> _transmissionLoggerMock = new();
    private readonly Mock<IOrderAuditService> _auditServiceMock = new();
    private readonly SamplingRoundService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid OtherDistributorId = Guid.Parse("00000000-0000-0000-0000-000000000011");
    private static readonly Guid SamplingLocationId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private static readonly Guid SamplingLocation2Id = Guid.Parse("00000000-0000-0000-0000-000000000021");
    private static readonly Guid OtherDistributorLocationId = Guid.Parse("00000000-0000-0000-0000-000000000022");
    private const string UserId = "user-1";
    private const string PreleveurId = "preleveur-1";

    public SamplingRoundServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AquaPlanDbContext(options);

        // AQ-398 — by default the delegation service returns the user's primary distributor only.
        // Tests that need delegation behaviour override this setup explicitly.
        _delegationServiceMock
            .Setup(d => d.GetAuthorizedDistributorIdsForUserAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync([DistributorId]);

        // Sprint Robustesse F-108 — TransmitAll now delegates the Completed → Transmitted
        // transition to the shared transmission service (Mock LIMS enabled here to prove the
        // round path forwards to the LIMS, which it previously skipped).
        _mockLimsServiceMock
            .Setup(s => s.ReceiveOrderAsync(It.IsAny<MockLimsOrderCreateDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MockLimsOrderCreatedDto(Guid.NewGuid(), DateTime.UtcNow));
        var mockLimsOptions = Options.Create(new MockLimsOptions { Enabled = true });
        var transmissionService = new OrderTransmissionService(
            _dbContext, _auditServiceMock.Object, _mockLimsServiceMock.Object, mockLimsOptions, _transmissionLoggerMock.Object);

        _sut = new SamplingRoundService(_dbContext, _delegationServiceMock.Object, _notificationServiceMock.Object, transmissionService, _loggerMock.Object);

        SeedData().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    // --- CreateAsync ---

    [Fact]
    public async Task CreateAsync_ShouldCreateRoundWithDraftStatus()
    {
        var dto = new SamplingRoundCreateDto(
            Name: "Round 1",
            Description: "Test description",
            Deadline: new DateTime(2026, 6, 30, 0, 0, 0, DateTimeKind.Utc),
            Notes: "Some notes",
            DistributorId: DistributorId);

        var result = await _sut.CreateAsync(dto, UserId, TenantId);

        result.Status.Should().Be(SamplingRoundStatus.Draft);
        result.Name.Should().Be("Round 1");
        result.Description.Should().Be("Test description");
        result.Notes.Should().Be("Some notes");
        result.DistributorId.Should().Be(DistributorId);
        result.CreatedById.Should().Be(UserId);
        result.Orders.Should().BeEmpty();
    }

    // --- GetByIdAsync ---

    [Fact]
    public async Task GetByIdAsync_ShouldReturnRoundWithOrders()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 2);

        var result = await _sut.GetByIdAsync(round.Id, TenantId);

        result.Should().NotBeNull();
        result!.Id.Should().Be(round.Id);
        result.Name.Should().Be(round.Name);
        result.Orders.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetByIdAsync_WhenNotFound_ShouldReturnNull()
    {
        var result = await _sut.GetByIdAsync(Guid.NewGuid(), TenantId);

        result.Should().BeNull();
    }

    // --- GetFilteredAsync ---

    [Fact]
    public async Task GetFilteredAsync_ShouldFilterByStatus()
    {
        await CreateDraftRound("Draft Round");
        var round2 = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round2.Id);

        var filter = new SamplingRoundFilterDto(Statuses: new[] { SamplingRoundStatus.Assigned });
        var result = await _sut.GetFilteredAsync(UserId, TenantId, filter, isAdmin: true, isPreleveurOnly: false);

        result.Items.Should().AllSatisfy(r => r.Status.Should().Be(SamplingRoundStatus.Assigned));
    }

    [Fact]
    public async Task GetFilteredAsync_WithMultipleStatuses_ShouldReturnAllMatching()
    {
        // AQ-362 — dashboard filter sends statuses=Draft&statuses=Assigned&statuses=InProgress
        // and every matching round must come back.
        var draft = await CreateDraftRound("Draft round");
        var roundToAssign = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(roundToAssign.Id);
        var roundToComplete = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(roundToComplete.Id);
        await TransitionToCompleted(roundToComplete.Id);

        var filter = new SamplingRoundFilterDto(Statuses: new[]
        {
            SamplingRoundStatus.Draft,
            SamplingRoundStatus.Assigned,
            SamplingRoundStatus.InProgress,
        });
        var result = await _sut.GetFilteredAsync(UserId, TenantId, filter, isAdmin: true, isPreleveurOnly: false);

        var ids = result.Items.Select(r => r.Id).ToList();
        ids.Should().Contain(draft.Id);
        ids.Should().Contain(roundToAssign.Id);
        ids.Should().NotContain(roundToComplete.Id);
        result.Items.Should().AllSatisfy(r => r.Status.Should().BeOneOf(
            SamplingRoundStatus.Draft,
            SamplingRoundStatus.Assigned,
            SamplingRoundStatus.InProgress));
    }

    [Fact]
    public async Task GetFilteredAsync_WhenNotAdmin_ShouldFilterByDistributor()
    {
        await CreateDraftRound("Round for user distributor");

        // Create a round for a different distributor
        var otherRound = new SamplingRound
        {
            Id = Guid.NewGuid(),
            Name = "Other distributor round",
            DistributorId = OtherDistributorId,
            Status = SamplingRoundStatus.Draft,
            TenantId = TenantId,
            CreatedById = UserId,
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.SamplingRounds.Add(otherRound);
        await _dbContext.SaveChangesAsync();

        var filter = new SamplingRoundFilterDto();
        var result = await _sut.GetFilteredAsync(UserId, TenantId, filter, isAdmin: false, isPreleveurOnly: false);

        result.Items.Should().AllSatisfy(r => r.DistributorId.Should().Be(DistributorId));
    }

    // --- AQ-398: Role-based visibility ---

    [Fact]
    public async Task GetFilteredAsync_WhenUserIsAdmin_ShouldReturnAllTenantRounds()
    {
        // AQ-398 — administrators see every round of the tenant, regardless of distributor or assignment.
        await CreateDraftRound("Round on user's distributor");
        var otherDistributorRound = new SamplingRound
        {
            Id = Guid.NewGuid(),
            Name = "Round on other distributor",
            DistributorId = OtherDistributorId,
            Status = SamplingRoundStatus.Draft,
            TenantId = TenantId,
            CreatedById = UserId,
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.SamplingRounds.Add(otherDistributorRound);
        await _dbContext.SaveChangesAsync();

        var filter = new SamplingRoundFilterDto();
        var result = await _sut.GetFilteredAsync(UserId, TenantId, filter, isAdmin: true, isPreleveurOnly: false);

        result.Items.Should().HaveCount(2);
        result.Items.Select(r => r.DistributorId).Should().Contain([DistributorId, OtherDistributorId]);
    }

    [Fact]
    public async Task GetFilteredAsync_WhenUserIsPreleveur_ShouldOnlyReturnOwnAssignedRounds()
    {
        // AQ-398 — a sole-Préleveur user only sees the rounds where PreleveurId matches.
        // Even rounds on his distributor that are assigned to someone else must be hidden.
        var ownRound = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(ownRound.Id); // assigned to PreleveurId

        // Round on the same distributor but assigned to someone else.
        var otherPreleveurRound = new SamplingRound
        {
            Id = Guid.NewGuid(),
            Name = "Assigned to another préleveur",
            DistributorId = DistributorId,
            PreleveurId = "other-preleveur",
            Status = SamplingRoundStatus.Assigned,
            TenantId = TenantId,
            CreatedById = UserId,
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.SamplingRounds.Add(otherPreleveurRound);
        await _dbContext.SaveChangesAsync();

        var filter = new SamplingRoundFilterDto();
        var result = await _sut.GetFilteredAsync(PreleveurId, TenantId, filter, isAdmin: false, isPreleveurOnly: true);

        result.Items.Should().HaveCount(1);
        result.Items[0].Id.Should().Be(ownRound.Id);
        result.Items[0].PreleveurId.Should().Be(PreleveurId);
    }

    [Fact]
    public async Task GetFilteredAsync_WhenUserIsRequerant_ShouldReturnAllRoundsOfAuthorizedDistributors()
    {
        // AQ-398 — a Mandataire / Requérant sees every round of his authorized distributors,
        // regardless of who created it or who is the assigned préleveur.
        var roundCreatedByMe = await CreateDraftRound("Created by me");

        // A round on the same distributor, but created by someone else.
        var roundCreatedByOther = new SamplingRound
        {
            Id = Guid.NewGuid(),
            Name = "Created by other requérant",
            DistributorId = DistributorId,
            Status = SamplingRoundStatus.Draft,
            TenantId = TenantId,
            CreatedById = "another-requerant",
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.SamplingRounds.Add(roundCreatedByOther);

        // A round on a distributor the user is NOT authorized on must be hidden.
        var roundOnForbiddenDistributor = new SamplingRound
        {
            Id = Guid.NewGuid(),
            Name = "Forbidden distributor",
            DistributorId = OtherDistributorId,
            Status = SamplingRoundStatus.Draft,
            TenantId = TenantId,
            CreatedById = UserId,
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.SamplingRounds.Add(roundOnForbiddenDistributor);
        await _dbContext.SaveChangesAsync();

        var filter = new SamplingRoundFilterDto();
        var result = await _sut.GetFilteredAsync(UserId, TenantId, filter, isAdmin: false, isPreleveurOnly: false);

        result.Items.Should().HaveCount(2);
        result.Items.Select(r => r.Id).Should().Contain([roundCreatedByMe.Id, roundCreatedByOther.Id]);
        result.Items.Should().AllSatisfy(r => r.DistributorId.Should().Be(DistributorId));
    }

    [Fact]
    public async Task GetFilteredAsync_WhenUserIsRequerantPreleveur_ShouldReturnAllRoundsOfAuthorizedDistributors()
    {
        // AQ-398 — a Requérant-Préleveur is treated like a Requérant for visibility:
        // he sees every round of his authorized distributors, NOT only the ones assigned to him.
        var assignedToMe = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(assignedToMe.Id); // assigned to PreleveurId

        // Round on the same distributor, never assigned to him.
        var notAssignedToMe = new SamplingRound
        {
            Id = Guid.NewGuid(),
            Name = "Not mine",
            DistributorId = DistributorId,
            Status = SamplingRoundStatus.Draft,
            TenantId = TenantId,
            CreatedById = "someone-else",
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.SamplingRounds.Add(notAssignedToMe);
        await _dbContext.SaveChangesAsync();

        // The user is a Requérant-Préleveur — isPreleveurOnly = false.
        var filter = new SamplingRoundFilterDto();
        var result = await _sut.GetFilteredAsync(PreleveurId, TenantId, filter, isAdmin: false, isPreleveurOnly: false);

        result.Items.Should().HaveCount(2);
        result.Items.Select(r => r.Id).Should().Contain([assignedToMe.Id, notAssignedToMe.Id]);
    }

    [Fact]
    public async Task GetFilteredAsync_WithDelegatedDistributor_ShouldIncludeDelegatedRounds()
    {
        // AQ-398 — a Mandataire whose distributor is the target of an active delegation
        // must also see the rounds of the delegating distributor.
        await CreateDraftRound("Round on own distributor");

        var delegatedRound = new SamplingRound
        {
            Id = Guid.NewGuid(),
            Name = "Round on delegated distributor",
            DistributorId = OtherDistributorId,
            Status = SamplingRoundStatus.Draft,
            TenantId = TenantId,
            CreatedById = UserId,
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.SamplingRounds.Add(delegatedRound);
        await _dbContext.SaveChangesAsync();

        // Delegation: OtherDistributor → user's primary DistributorId.
        _delegationServiceMock
            .Setup(d => d.GetAuthorizedDistributorIdsForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([DistributorId, OtherDistributorId]);

        var filter = new SamplingRoundFilterDto();
        var result = await _sut.GetFilteredAsync(UserId, TenantId, filter, isAdmin: false, isPreleveurOnly: false);

        result.Items.Should().HaveCount(2);
        result.Items.Select(r => r.DistributorId).Should().Contain([DistributorId, OtherDistributorId]);
    }

    // --- UpdateAsync ---

    [Fact]
    public async Task UpdateAsync_ShouldUpdateFields()
    {
        var round = await CreateDraftRound("Original name");

        var updateDto = new SamplingRoundUpdateDto(
            Name: "Updated name",
            Description: "Updated desc",
            Deadline: new DateTime(2026, 12, 31, 0, 0, 0, DateTimeKind.Utc),
            Notes: "Updated notes");

        var result = await _sut.UpdateAsync(round.Id, updateDto, UserId, TenantId);

        result.Should().NotBeNull();
        result!.Name.Should().Be("Updated name");
        result.Description.Should().Be("Updated desc");
        result.Notes.Should().Be("Updated notes");
    }

    [Fact]
    public async Task UpdateAsync_WhenInProgress_ShouldThrow()
    {
        var round = await CreateRoundInStatus(SamplingRoundStatus.InProgress);

        var updateDto = new SamplingRoundUpdateDto("New name", null, null, null);

        await _sut.Awaiting(s => s.UpdateAsync(round.Id, updateDto, UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot update*");
    }

    // --- DeleteAsync (F-107 soft-cancel) ---

    [Fact]
    public async Task DeleteAsync_ShouldSoftCancelRound_AndDetachOrdersWithoutDeleting()
    {
        // F-107 — the round is marked Cancelled (not removed) and its orders are detached
        // (SamplingRoundId = null) but preserved, instead of being hard-deleted.
        var round = await CreateDraftRoundWithOrders(orderCount: 2);
        var orderIds = (await _dbContext.Orders
            .Where(o => o.SamplingRoundId == round.Id)
            .Select(o => o.Id)
            .ToListAsync());

        var result = await _sut.DeleteAsync(round.Id, UserId, TenantId);

        result.Should().BeTrue();

        var cancelled = await _dbContext.SamplingRounds.FindAsync(round.Id);
        cancelled.Should().NotBeNull();
        cancelled!.Status.Should().Be(SamplingRoundStatus.Cancelled);

        // Orders are kept and detached — never deleted.
        var orders = await _dbContext.Orders.Where(o => orderIds.Contains(o.Id)).ToListAsync();
        orders.Should().HaveCount(2);
        orders.Should().AllSatisfy(o => o.SamplingRoundId.Should().BeNull());
    }

    [Fact]
    public async Task DeleteAsync_WhenRoundLocked_ShouldThrowRoundLocked()
    {
        // F-107 — a locked (active préleveur session) round cannot be deleted.
        var round = await CreateRoundInStatus(SamplingRoundStatus.InProgress);
        var entity = await _dbContext.SamplingRounds.FindAsync(round.Id);
        entity!.IsLocked = true;
        entity.LockedById = PreleveurId;
        entity.LockedAt = DateTime.UtcNow;
        await _dbContext.SaveChangesAsync();

        await _sut.Awaiting(s => s.DeleteAsync(round.Id, UserId, TenantId))
            .Should().ThrowAsync<RoundLockedException>();
    }

    [Fact]
    public async Task DeleteAsync_WhenInProgressUnlocked_ShouldThrowConflict()
    {
        // F-107 — an InProgress round (even if not locked) is a conflict, not deletable.
        var round = await CreateRoundInStatus(SamplingRoundStatus.InProgress);

        await _sut.Awaiting(s => s.DeleteAsync(round.Id, UserId, TenantId))
            .Should().ThrowAsync<ConflictOperationException>();
    }

    // --- AssignPreleveurAsync ---

    [Fact]
    public async Task AssignPreleveurAsync_ShouldTransitionToAssigned()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);

        var dto = new SamplingRoundAssignDto(PreleveurId: PreleveurId);
        var result = await _sut.AssignPreleveurAsync(round.Id, dto, UserId, TenantId);

        result.Should().NotBeNull();
        result!.Status.Should().Be(SamplingRoundStatus.Assigned);
        result.PreleveurId.Should().Be(PreleveurId);
    }

    [Fact]
    public async Task AssignPreleveurAsync_WhenNoOrders_ShouldThrow()
    {
        var round = await CreateDraftRound("Empty round");

        var dto = new SamplingRoundAssignDto(PreleveurId: PreleveurId);

        await _sut.Awaiting(s => s.AssignPreleveurAsync(round.Id, dto, UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no orders*");
    }

    [Fact]
    public async Task AssignPreleveurAsync_ShouldCascadePreleveurToOrders()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 2);

        var dto = new SamplingRoundAssignDto(PreleveurId: PreleveurId);
        await _sut.AssignPreleveurAsync(round.Id, dto, UserId, TenantId);

        var orders = await _dbContext.Orders
            .Where(o => o.SamplingRoundId == round.Id)
            .ToListAsync();

        orders.Should().AllSatisfy(o =>
        {
            o.PreleveurId.Should().Be(PreleveurId);
        });
    }

    // AQ-43 — notifications trigger on round assignment
    [Fact]
    public async Task AssignPreleveurAsync_ShouldCreateRoundAssignedNotificationForPreleveur()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 3);

        var dto = new SamplingRoundAssignDto(PreleveurId: PreleveurId);
        await _sut.AssignPreleveurAsync(round.Id, dto, UserId, TenantId);

        _notificationServiceMock.Verify(s => s.CreateAsync(
            PreleveurId,
            NotificationType.RoundAssigned,
            It.IsAny<string>(),
            It.Is<string>(m => m.Contains("3 mandats")),
            TenantId,
            "SamplingRound",
            round.Id,
            false,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // --- RevertToDraftAsync ---

    [Fact]
    public async Task RevertToDraftAsync_ShouldTransitionToDraft()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round.Id);

        var result = await _sut.RevertToDraftAsync(round.Id, UserId, TenantId);

        result.Should().NotBeNull();
        result!.Status.Should().Be(SamplingRoundStatus.Draft);
        result.PreleveurId.Should().BeNull();
    }

    [Fact]
    public async Task RevertToDraftAsync_ShouldClearPreleveurFromOrders()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 2);
        await TransitionToAssigned(round.Id);

        await _sut.RevertToDraftAsync(round.Id, UserId, TenantId);

        var orders = await _dbContext.Orders
            .Where(o => o.SamplingRoundId == round.Id)
            .ToListAsync();

        orders.Should().AllSatisfy(o =>
        {
            o.PreleveurId.Should().BeNull();
        });
    }

    [Fact]
    public async Task RevertToDraftAsync_WhenNotAssigned_ShouldThrow()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);

        await _sut.Awaiting(s => s.RevertToDraftAsync(round.Id, UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*assigned*");
    }

    // --- CancelAsync ---

    [Fact]
    public async Task CancelAsync_ShouldCascadeCancelOrders()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 2);

        var result = await _sut.CancelAsync(round.Id, UserId, TenantId);

        result.Should().NotBeNull();
        result!.Status.Should().Be(SamplingRoundStatus.Cancelled);

        var orders = await _dbContext.Orders
            .Where(o => o.SamplingRoundId == round.Id)
            .ToListAsync();

        orders.Should().AllSatisfy(o => o.Status.Should().Be(OrderStatus.Cancelled));
    }

    [Fact]
    public async Task CancelAsync_WhenInProgress_ShouldThrow()
    {
        var round = await CreateRoundInStatus(SamplingRoundStatus.InProgress);

        await _sut.Awaiting(s => s.CancelAsync(round.Id, UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Cannot cancel*");
    }

    // --- AddOrderAsync ---

    [Fact]
    public async Task AddOrderAsync_ShouldAddWithCorrectSortOrder()
    {
        var round = await CreateDraftRound("Round");
        var order1Id = await CreateStandaloneOrder(DistributorId);
        var order2Id = await CreateStandaloneOrder(DistributorId);

        await _sut.AddOrderAsync(round.Id, order1Id, TenantId);
        await _sut.AddOrderAsync(round.Id, order2Id, TenantId);

        var orders = await _dbContext.Orders
            .Where(o => o.SamplingRoundId == round.Id)
            .OrderBy(o => o.SortOrder)
            .ToListAsync();

        orders.Should().HaveCount(2);
        orders[0].SortOrder.Should().Be(0);
        orders[1].SortOrder.Should().Be(1);
    }

    [Fact]
    public async Task AddOrderAsync_WhenDifferentDistributor_ShouldThrow()
    {
        var round = await CreateDraftRound("Round");
        var orderId = await CreateStandaloneOrder(OtherDistributorId);

        await _sut.Awaiting(s => s.AddOrderAsync(round.Id, orderId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*same distributor*");
    }

    // --- RemoveOrderAsync (AQ-413) ---

    [Fact]
    public async Task RemoveOrderAsync_OnDraft_ShouldDetachOrderWithoutDeleting()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 2);
        var orderId = (await _dbContext.Orders
            .Where(o => o.SamplingRoundId == round.Id)
            .FirstAsync()).Id;

        var result = await _sut.RemoveOrderAsync(round.Id, orderId, TenantId);

        result.Should().BeTrue();
        var remainingInRound = await _dbContext.Orders
            .Where(o => o.SamplingRoundId == round.Id)
            .ToListAsync();
        remainingInRound.Should().HaveCount(1);

        // AQ-413 — the detached order MUST still exist (only SamplingRoundId is cleared).
        var detached = await _dbContext.Orders.FindAsync(orderId);
        detached.Should().NotBeNull();
        detached!.SamplingRoundId.Should().BeNull();
    }

    [Fact]
    public async Task RemoveOrderAsync_OnAssigned_ShouldDetachOrderAndClearPreleveur()
    {
        // AQ-413 — Assigned rounds are still modifiable (round not yet locked).
        var round = await CreateDraftRoundWithOrders(orderCount: 2);
        await TransitionToAssigned(round.Id);
        var orderId = (await _dbContext.Orders
            .Where(o => o.SamplingRoundId == round.Id)
            .FirstAsync()).Id;

        var result = await _sut.RemoveOrderAsync(round.Id, orderId, TenantId);

        result.Should().BeTrue();
        var detached = await _dbContext.Orders.FindAsync(orderId);
        detached.Should().NotBeNull();
        detached!.SamplingRoundId.Should().BeNull();
        detached.PreleveurId.Should().BeNull();
    }

    [Fact]
    public async Task RemoveOrderAsync_WhenRoundInProgress_ShouldThrowConflict()
    {
        // AQ-413 — once the round is InProgress (locked), orders can no longer be detached.
        var round = await CreateRoundInStatus(SamplingRoundStatus.InProgress);
        var orderId = (await _dbContext.Orders
            .Where(o => o.SamplingRoundId == round.Id)
            .FirstAsync()).Id;

        await _sut.Awaiting(s => s.RemoveOrderAsync(round.Id, orderId, TenantId))
            .Should().ThrowAsync<ConflictOperationException>()
            .WithMessage("*InProgress*");
    }

    [Fact]
    public async Task RemoveOrderAsync_WhenRoundNotFound_ShouldReturnFalse()
    {
        var result = await _sut.RemoveOrderAsync(Guid.NewGuid(), Guid.NewGuid(), TenantId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task RemoveOrderAsync_WhenOrderNotInRound_ShouldReturnFalse()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);

        var result = await _sut.RemoveOrderAsync(round.Id, Guid.NewGuid(), TenantId);

        result.Should().BeFalse();
    }

    // --- ReorderAsync ---

    [Fact]
    public async Task ReorderAsync_ShouldUpdateSortOrders()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 3);
        var orders = await _dbContext.Orders
            .Where(o => o.SamplingRoundId == round.Id)
            .OrderBy(o => o.SortOrder)
            .ToListAsync();

        // Reverse the order
        var dto = new SamplingRoundReorderDto([
            new OrderPositionDto(orders[0].Id, 2),
            new OrderPositionDto(orders[1].Id, 1),
            new OrderPositionDto(orders[2].Id, 0),
        ]);

        var result = await _sut.ReorderAsync(round.Id, dto, TenantId);

        result.Should().NotBeNull();
        result!.Orders[0].Id.Should().Be(orders[2].Id);
        result.Orders[1].Id.Should().Be(orders[1].Id);
        result.Orders[2].Id.Should().Be(orders[0].Id);
    }

    // --- ReplaceLocationAsync ---

    [Fact]
    public async Task ReplaceLocationAsync_ShouldReplaceAndTrackOriginal()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round.Id);

        var order = await _dbContext.Orders
            .Where(o => o.SamplingRoundId == round.Id)
            .FirstAsync();

        var originalLocationId = order.SamplingLocationId;

        var dto = new LocationReplacementDto(
            NewSamplingLocationId: SamplingLocation2Id,
            Reason: "Location inaccessible");

        var result = await _sut.ReplaceLocationAsync(order.Id, dto, PreleveurId, TenantId);

        result.Should().BeTrue();

        var updatedOrder = await _dbContext.Orders.FindAsync(order.Id);
        updatedOrder!.SamplingLocationId.Should().Be(SamplingLocation2Id);
        updatedOrder.OriginalSamplingLocationId.Should().Be(originalLocationId);
        updatedOrder.LocationReplacementReason.Should().Be("Location inaccessible");
    }

    [Fact]
    public async Task ReplaceLocationAsync_WhenDifferentDistributor_ShouldThrow()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round.Id);

        var order = await _dbContext.Orders
            .Where(o => o.SamplingRoundId == round.Id)
            .FirstAsync();

        var dto = new LocationReplacementDto(
            NewSamplingLocationId: OtherDistributorLocationId,
            Reason: "Some reason here");

        await _sut.Awaiting(s => s.ReplaceLocationAsync(order.Id, dto, PreleveurId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*same distributor*");
    }

    // --- StartOrderAsync ---

    [Fact]
    public async Task StartOrderAsync_ShouldTransitionToInProgress()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round.Id);

        var order = await _dbContext.Orders
            .Where(o => o.SamplingRoundId == round.Id)
            .FirstAsync();

        var result = await _sut.StartOrderAsync(order.Id, PreleveurId, TenantId);

        result.Should().BeTrue();

        var updatedOrder = await _dbContext.Orders.FindAsync(order.Id);
        updatedOrder!.Status.Should().Be(OrderStatus.InProgress);
    }

    [Fact]
    public async Task StartOrderAsync_ShouldTransitionRoundFromAssignedToInProgress()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round.Id);

        var order = await _dbContext.Orders
            .Where(o => o.SamplingRoundId == round.Id)
            .FirstAsync();

        await _sut.StartOrderAsync(order.Id, PreleveurId, TenantId);

        var updatedRound = await _dbContext.SamplingRounds.FindAsync(round.Id);
        updatedRound!.Status.Should().Be(SamplingRoundStatus.InProgress);
    }

    // --- TransmitAllAsync ---

    [Fact]
    public async Task TransmitAllAsync_ShouldTransmitCompletedOrders()
    {
        var round = await CreateRoundInStatus(SamplingRoundStatus.InProgress);
        var orders = await _dbContext.Orders
            .Where(o => o.SamplingRoundId == round.Id)
            .ToListAsync();
        foreach (var order in orders)
        {
            order.Status = OrderStatus.Completed;
        }
        await _dbContext.SaveChangesAsync();

        var result = await _sut.TransmitAllAsync(round.Id, UserId, TenantId);

        result.Should().NotBeNull();
        var transmittedOrders = await _dbContext.Orders
            .Where(o => o.SamplingRoundId == round.Id)
            .ToListAsync();
        transmittedOrders.Should().AllSatisfy(o => o.Status.Should().Be(OrderStatus.Transmitted));
        // F-108 — the round path now produces the SAME state as the bulk path: timestamps set,
        // LimsOrderId assigned (forwarded to Mock LIMS), and an audit entry per order.
        transmittedOrders.Should().AllSatisfy(o =>
        {
            o.TransmittedAt.Should().NotBeNull();
            o.StatusChangedBy.Should().Be(UserId);
            o.LimsOrderId.Should().NotBeNull();
        });
        _mockLimsServiceMock.Verify(
            s => s.ReceiveOrderAsync(It.IsAny<MockLimsOrderCreateDto>(), TenantId, It.IsAny<CancellationToken>()),
            Times.Exactly(transmittedOrders.Count));
        _auditServiceMock.Verify(
            a => a.LogAsync(It.IsAny<Guid>(), "StatusTransitioned", It.IsAny<string>(),
                "Completed", "Transmitted", UserId, TenantId, It.IsAny<CancellationToken>()),
            Times.Exactly(transmittedOrders.Count));
    }

    [Fact]
    public async Task TransmitAllAsync_WhenNoCompletedOrders_ShouldThrow()
    {
        var round = await CreateRoundInStatus(SamplingRoundStatus.InProgress);

        await _sut.Awaiting(s => s.TransmitAllAsync(round.Id, UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*No completed orders*");
    }

    // --- UpdateSamplerCommentAsync ---

    [Fact]
    public async Task UpdateSamplerCommentAsync_ShouldUpdateComment()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round.Id);

        var order = await _dbContext.Orders
            .Where(o => o.SamplingRoundId == round.Id)
            .FirstAsync();

        var dto = new SamplerCommentDto(Comment: "Water looks cloudy");
        var result = await _sut.UpdateSamplerCommentAsync(order.Id, dto, PreleveurId, TenantId);

        result.Should().BeTrue();

        var updatedOrder = await _dbContext.Orders.FindAsync(order.Id);
        updatedOrder!.SamplerComment.Should().Be("Water looks cloudy");
    }

    // --- ContainerSummary (AQ-341) ---

    [Fact]
    public async Task GetByIdAsync_WithMultipleOrders_ShouldAggregateContainers()
    {
        var (program, _, _) = await CreateAnalysisProgramWithTwoContainers();
        var round = await CreateDraftRound("Round summary");
        await CreateOrderInRoundWithProgram(round.Id, program.Id, sortOrder: 0);
        await CreateOrderInRoundWithProgram(round.Id, program.Id, sortOrder: 1);

        var result = await _sut.GetByIdAsync(round.Id, TenantId);

        result.Should().NotBeNull();
        result!.ContainerSummary.Should().HaveCount(2);
        result.ContainerSummary.Should().AllSatisfy(s => s.Count.Should().Be(2));
    }

    [Fact]
    public async Task GetByIdAsync_WithCancelledOrder_ShouldExcludeFromSummary()
    {
        var (program, _, _) = await CreateAnalysisProgramWithTwoContainers();
        var round = await CreateDraftRound("Round with cancel");
        await CreateOrderInRoundWithProgram(round.Id, program.Id, sortOrder: 0);
        var cancelledOrderId = await CreateOrderInRoundWithProgram(round.Id, program.Id, sortOrder: 1);

        var cancelled = await _dbContext.Orders.FindAsync(cancelledOrderId);
        cancelled!.Status = OrderStatus.Cancelled;
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(round.Id, TenantId);

        result.Should().NotBeNull();
        result!.ContainerSummary.Should().HaveCount(2);
        result.ContainerSummary.Should().AllSatisfy(s => s.Count.Should().Be(1));
    }

    [Fact]
    public async Task GetByIdAsync_With2OrdersSharingContainer_ShouldSumCount()
    {
        var (program, _, _) = await CreateAnalysisProgramWithSharedContainer();
        var round = await CreateDraftRound("Round shared");
        await CreateOrderInRoundWithProgram(round.Id, program.Id, sortOrder: 0);
        await CreateOrderInRoundWithProgram(round.Id, program.Id, sortOrder: 1);

        var result = await _sut.GetByIdAsync(round.Id, TenantId);

        result.Should().NotBeNull();
        result!.ContainerSummary.Should().HaveCount(1);
        result.ContainerSummary[0].Count.Should().Be(2);
    }

    [Fact]
    public async Task GetByIdAsync_WithEmptyRound_ShouldReturnEmptySummary()
    {
        var round = await CreateDraftRound("Empty round");

        var result = await _sut.GetByIdAsync(round.Id, TenantId);

        result.Should().NotBeNull();
        result!.ContainerSummary.Should().BeEmpty();
    }

    // --- Order indicator flags (AQ-414) ---

    [Fact]
    public async Task GetByIdAsync_WithoutAnyIndicator_ShouldReturnAllFlagsFalse()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);

        var result = await _sut.GetByIdAsync(round.Id, TenantId);

        result.Should().NotBeNull();
        var orderDto = result!.Orders.Single();
        orderDto.HasMandatorNote.Should().BeFalse();
        orderDto.HasPreleveurNote.Should().BeFalse();
        orderDto.HasReplacedLocation.Should().BeFalse();
        orderDto.PreleveurNote.Should().BeNull();
    }

    [Fact]
    public async Task GetByIdAsync_WithMandatorNote_ShouldSetHasMandatorNote()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        var order = await _dbContext.Orders.FirstAsync(o => o.SamplingRoundId == round.Id);
        order.Notes = "Please be cautious near the gate";
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(round.Id, TenantId);

        result!.Orders.Single().HasMandatorNote.Should().BeTrue();
        result.Orders.Single().HasPreleveurNote.Should().BeFalse();
        result.Orders.Single().HasReplacedLocation.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_WithWhitespaceOnlyNotes_ShouldNotFlagMandatorNote()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        var order = await _dbContext.Orders.FirstAsync(o => o.SamplingRoundId == round.Id);
        order.Notes = "   ";
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(round.Id, TenantId);

        result!.Orders.Single().HasMandatorNote.Should().BeFalse();
    }

    [Fact]
    public async Task GetByIdAsync_WithReplacedLocation_ShouldSetHasReplacedLocation()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        var order = await _dbContext.Orders.FirstAsync(o => o.SamplingRoundId == round.Id);
        // Simulate a completed replacement: original id captured, current id differs.
        order.OriginalSamplingLocationId = SamplingLocationId;
        order.SamplingLocationId = SamplingLocation2Id;
        order.LocationReplacementReason = "LDP inaccessible that day";
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(round.Id, TenantId);

        result!.Orders.Single().HasReplacedLocation.Should().BeTrue();
    }

    [Fact]
    public async Task GetByIdAsync_WithPreleveurSamplingNote_ShouldSetHasPreleveurNote()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        var order = await _dbContext.Orders.FirstAsync(o => o.SamplingRoundId == round.Id);

        var sampling = new Sampling
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            PreleveurId = PreleveurId,
            SamplingDateTime = DateTime.UtcNow,
            Temperature = 8,
            Weather = "Sunny",
            Notes = "Slight turbidity observed",
            IsChlorinated = false,
            CreatedAt = DateTime.UtcNow,
            TenantId = TenantId,
        };
        _dbContext.Samplings.Add(sampling);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetByIdAsync(round.Id, TenantId);

        var dto = result!.Orders.Single();
        dto.HasPreleveurNote.Should().BeTrue();
        dto.PreleveurNote.Should().Be("Slight turbidity observed");
    }

    // --- AQ-370 StartAsync ---

    [Fact]
    public async Task StartAsync_ShouldSetIsLockedAndLockedBy()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round.Id);

        var result = await _sut.StartAsync(round.Id, PreleveurId, TenantId, isAdmin: false);

        result.Should().NotBeNull();
        result!.Status.Should().Be(SamplingRoundStatus.InProgress);
        result.IsLocked.Should().BeTrue();
        result.LockedById.Should().Be(PreleveurId);
        result.LockedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task StartAsync_WhenNotAssigned_ShouldThrowConflict()
    {
        // AQ-394 — starting a round in Draft/other status is a state conflict (409).
        var round = await CreateDraftRound("Not assigned");

        await _sut.Awaiting(s => s.StartAsync(round.Id, PreleveurId, TenantId, isAdmin: true))
            .Should().ThrowAsync<ConflictOperationException>()
            .WithMessage("*ne peut pas être démarrée*");
    }

    [Fact]
    public async Task StartAsync_WhenAlreadyInProgress_ShouldThrowConflict()
    {
        // AQ-394 — second /start call on an InProgress round must raise 409 Conflict.
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round.Id);
        await _sut.StartAsync(round.Id, PreleveurId, TenantId, isAdmin: false);

        await _sut.Awaiting(s => s.StartAsync(round.Id, PreleveurId, TenantId, isAdmin: false))
            .Should().ThrowAsync<ConflictOperationException>()
            .WithMessage("*ne peut pas être démarrée*");
    }

    [Fact]
    public async Task StartAsync_WhenNotAssignedPreleveur_ShouldThrowForbidden()
    {
        // AQ-394 — a préleveur not assigned to the round must receive 403 Forbidden.
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round.Id);

        await _sut.Awaiting(s => s.StartAsync(round.Id, "other-user", TenantId, isAdmin: false))
            .Should().ThrowAsync<ForbiddenOperationException>()
            .WithMessage("*pas assigné*");
    }

    [Fact]
    public async Task StartAsync_AsAdmin_ShouldSucceedEvenIfNotAssignedUser()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round.Id);

        var result = await _sut.StartAsync(round.Id, "admin-id", TenantId, isAdmin: true);

        result.Should().NotBeNull();
        result!.IsLocked.Should().BeTrue();
        // LockedById should be the preleveur (assignee), not the admin who triggered start
        result.LockedById.Should().Be(PreleveurId);
    }

    // --- AQ-372 ForceUnlockAsync ---

    [Fact]
    public async Task ForceUnlockAsync_WhenLocked_ShouldResetLockAndRevertToAssigned()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round.Id);
        await _sut.StartAsync(round.Id, PreleveurId, TenantId, isAdmin: false);

        var result = await _sut.ForceUnlockAsync(round.Id, "admin-id", TenantId);

        result.Should().NotBeNull();
        result!.Status.Should().Be(SamplingRoundStatus.Assigned);
        result.IsLocked.Should().BeFalse();
        result.LockedById.Should().BeNull();
        result.LockedAt.Should().BeNull();
    }

    [Fact]
    public async Task ForceUnlockAsync_WhenNotLocked_ShouldThrow()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round.Id);

        await _sut.Awaiting(s => s.ForceUnlockAsync(round.Id, "admin-id", TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*not locked*");
    }

    [Fact]
    public async Task ForceUnlockAsync_WhenNotFound_ShouldReturnNull()
    {
        var result = await _sut.ForceUnlockAsync(Guid.NewGuid(), "admin-id", TenantId);

        result.Should().BeNull();
    }

    // --- AQ-371 EnsureRoundNotLockedForWriteAsync ---

    [Fact]
    public async Task EnsureRoundNotLockedForWriteAsync_WhenLockedByOtherUser_ShouldThrow()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round.Id);
        await _sut.StartAsync(round.Id, PreleveurId, TenantId, isAdmin: false);

        await _sut.Awaiting(s => s.EnsureRoundNotLockedForWriteAsync(round.Id, "other-user", isAdmin: false, TenantId))
            .Should().ThrowAsync<RoundLockedException>();
    }

    [Fact]
    public async Task EnsureRoundNotLockedForWriteAsync_WhenLockedBySameUser_ShouldNotThrow()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round.Id);
        await _sut.StartAsync(round.Id, PreleveurId, TenantId, isAdmin: false);

        await _sut.EnsureRoundNotLockedForWriteAsync(round.Id, PreleveurId, isAdmin: false, TenantId);
    }

    [Fact]
    public async Task EnsureRoundNotLockedForWriteAsync_WhenRoundNotLocked_ShouldNotThrow()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);

        await _sut.EnsureRoundNotLockedForWriteAsync(round.Id, "other-user", isAdmin: false, TenantId);
    }

    [Fact]
    public async Task EnsureRoundNotLockedForWriteAsync_WithNullRoundId_ShouldNotThrow()
    {
        await _sut.EnsureRoundNotLockedForWriteAsync(null, "any-user", isAdmin: false, TenantId);
    }

    // --- AQ-373 GetOfflineSnapshotAsync ---

    [Fact]
    public async Task GetOfflineSnapshotAsync_AsAssignedPreleveur_ShouldReturnCompleteSnapshot()
    {
        var (program, containerA, containerB) = await CreateAnalysisProgramWithTwoContainers();
        var round = await CreateDraftRound("Snapshot round");
        await CreateOrderInRoundWithProgram(round.Id, program.Id, sortOrder: 0);
        await TransitionToAssigned(round.Id);

        var result = await _sut.GetOfflineSnapshotAsync(
            round.Id, PreleveurId, isAdmin: false, TenantId);

        result.Should().NotBeNull();
        result!.Round.Id.Should().Be(round.Id);
        result.Orders.Should().HaveCount(1);
        result.Orders[0].AnalysisPrograms.Should().ContainSingle(p => p.AnalysisProgramId == program.Id);
        result.AnalysisPrograms.Should().ContainSingle(p => p.Id == program.Id);
        result.AnalysisProfiles.Should().HaveCount(2);
        result.Containers.Select(c => c.Id).Should().BeEquivalentTo(new[] { containerA.Id, containerB.Id });
        result.SamplingLocations.Should().OnlyContain(sl => sl.DistributorId == DistributorId);
        result.SnapshotedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetOfflineSnapshotAsync_AsAdmin_ShouldSucceed()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round.Id);

        var result = await _sut.GetOfflineSnapshotAsync(
            round.Id, "some-admin", isAdmin: true, TenantId);

        result.Should().NotBeNull();
        result!.Round.Id.Should().Be(round.Id);
    }

    [Fact]
    public async Task GetOfflineSnapshotAsync_AsOtherPreleveur_ShouldThrowUnauthorized()
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round.Id);

        await _sut.Awaiting(s => s.GetOfflineSnapshotAsync(
                round.Id, "other-preleveur", isAdmin: false, TenantId))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task GetOfflineSnapshotAsync_WhenRoundNotExists_ShouldReturnNull()
    {
        var result = await _sut.GetOfflineSnapshotAsync(
            Guid.NewGuid(), PreleveurId, isAdmin: false, TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetOfflineSnapshotAsync_ShouldOnlyIncludeActiveValidatedLocations()
    {
        // Add an inactive and a non-validated location to the same distributor.
        _dbContext.SamplingLocations.Add(new SamplingLocation
        {
            Id = Guid.NewGuid(), Name = "Inactive source", LocationCode = "INACTIVE",
            DistributorId = DistributorId, IsActive = false, IsValidated = true,
        });
        _dbContext.SamplingLocations.Add(new SamplingLocation
        {
            Id = Guid.NewGuid(), Name = "Not validated", LocationCode = "UNVAL",
            DistributorId = DistributorId, IsActive = true, IsValidated = false,
        });
        await _dbContext.SaveChangesAsync();

        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round.Id);

        var result = await _sut.GetOfflineSnapshotAsync(
            round.Id, PreleveurId, isAdmin: false, TenantId);

        result.Should().NotBeNull();
        result!.SamplingLocations.Should().OnlyContain(sl => sl.IsActive && sl.IsValidated);
    }

    [Fact]
    public async Task GetOfflineSnapshotAsync_ShouldBeJsonSerializable()
    {
        // AQ-373 — the DTO must round-trip through System.Text.Json so it can be
        // persisted in IndexedDB and re-hydrated by the Angular offline service.
        var round = await CreateDraftRoundWithOrders(orderCount: 1);
        await TransitionToAssigned(round.Id);

        var snapshot = await _sut.GetOfflineSnapshotAsync(
            round.Id, PreleveurId, isAdmin: false, TenantId);

        var json = System.Text.Json.JsonSerializer.Serialize(snapshot);
        json.Should().NotBeNullOrWhiteSpace();
        var rehydrated = System.Text.Json.JsonSerializer.Deserialize<OfflineSnapshotDto>(json);
        rehydrated.Should().NotBeNull();
        rehydrated!.Round.Id.Should().Be(snapshot!.Round.Id);
        rehydrated.Orders.Should().HaveCount(snapshot.Orders.Count);
    }

    private async Task<(AnalysisProgram Program, Container ContainerA, Container ContainerB)> CreateAnalysisProgramWithTwoContainers()
    {
        var containerA = new Container
        {
            Id = Guid.NewGuid(), Code = "ROUND-A", Name = "Flacon A",
            Material = "Verre", VolumeMl = 250, Color = "T", IsActive = true, TenantId = TenantId,
        };
        var containerB = new Container
        {
            Id = Guid.NewGuid(), Code = "ROUND-B", Name = "Flacon B",
            Material = "PET", VolumeMl = 500, Color = "T", IsActive = true, TenantId = TenantId,
        };
        _dbContext.Containers.Add(containerA);
        _dbContext.Containers.Add(containerB);

        var profileA = new AnalysisProfile
        {
            Id = Guid.NewGuid(), Code = "pA", Name = "Profil A",
            TenantId = TenantId, ContainerId = containerA.Id,
        };
        var profileB = new AnalysisProfile
        {
            Id = Guid.NewGuid(), Code = "pB", Name = "Profil B",
            TenantId = TenantId, ContainerId = containerB.Id,
        };
        _dbContext.AnalysisProfiles.Add(profileA);
        _dbContext.AnalysisProfiles.Add(profileB);

        var program = new AnalysisProgram
        {
            Id = Guid.NewGuid(), Code = "PRG-R", Name = "Prog Round", TenantId = TenantId,
        };
        _dbContext.AnalysisPrograms.Add(program);
        _dbContext.AnalysisProgramProfiles.Add(new AnalysisProgramProfile
        {
            AnalysisProgramId = program.Id, AnalysisProfileId = profileA.Id,
        });
        _dbContext.AnalysisProgramProfiles.Add(new AnalysisProgramProfile
        {
            AnalysisProgramId = program.Id, AnalysisProfileId = profileB.Id,
        });
        await _dbContext.SaveChangesAsync();
        return (program, containerA, containerB);
    }

    private async Task<(AnalysisProgram Program, Container Container, Container _)> CreateAnalysisProgramWithSharedContainer()
    {
        var container = new Container
        {
            Id = Guid.NewGuid(), Code = "ROUND-SHARED", Name = "Flacon Shared",
            Material = "Verre", VolumeMl = 250, Color = "T", IsActive = true, TenantId = TenantId,
        };
        _dbContext.Containers.Add(container);

        var profileA = new AnalysisProfile
        {
            Id = Guid.NewGuid(), Code = "pA", Name = "Profil A",
            TenantId = TenantId, ContainerId = container.Id,
        };
        var profileB = new AnalysisProfile
        {
            Id = Guid.NewGuid(), Code = "pB", Name = "Profil B",
            TenantId = TenantId, ContainerId = container.Id,
        };
        _dbContext.AnalysisProfiles.Add(profileA);
        _dbContext.AnalysisProfiles.Add(profileB);

        var program = new AnalysisProgram
        {
            Id = Guid.NewGuid(), Code = "PRG-SH", Name = "Prog Shared", TenantId = TenantId,
        };
        _dbContext.AnalysisPrograms.Add(program);
        _dbContext.AnalysisProgramProfiles.Add(new AnalysisProgramProfile
        {
            AnalysisProgramId = program.Id, AnalysisProfileId = profileA.Id,
        });
        _dbContext.AnalysisProgramProfiles.Add(new AnalysisProgramProfile
        {
            AnalysisProgramId = program.Id, AnalysisProfileId = profileB.Id,
        });
        await _dbContext.SaveChangesAsync();
        return (program, container, container);
    }

    private async Task<Guid> CreateOrderInRoundWithProgram(Guid roundId, Guid programId, int sortOrder)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"ORD-{Guid.NewGuid().ToString()[..4]}",
            Status = OrderStatus.New,
            DistributorId = DistributorId,
            SamplingRoundId = roundId,
            SortOrder = sortOrder,
            SamplingLocationId = SamplingLocationId,
            CreatedById = UserId,
            TenantId = TenantId,
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.Orders.Add(order);
        _dbContext.OrderAnalysisPrograms.Add(new OrderAnalysisProgram
        {
            OrderId = order.Id, AnalysisProgramId = programId,
        });
        await _dbContext.SaveChangesAsync();
        return order.Id;
    }

    // --- Helpers ---

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

        _dbContext.Users.Add(new AppUser
        {
            Id = PreleveurId,
            UserName = "preleveur1@test.com",
            Email = "preleveur1@test.com",
            FirstName = "Pierre",
            LastName = "Martin",
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

        _dbContext.Distributors.Add(new Distributor
        {
            Id = OtherDistributorId,
            Name = "Other Distributor",
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

        _dbContext.SamplingLocations.Add(new SamplingLocation
        {
            Id = SamplingLocation2Id,
            Name = "Source B",
            LocationCode = "SRC-B",
            DistributorId = DistributorId,
            IsActive = true,
        });

        _dbContext.SamplingLocations.Add(new SamplingLocation
        {
            Id = OtherDistributorLocationId,
            Name = "Source X",
            LocationCode = "SRC-X",
            DistributorId = OtherDistributorId,
            IsActive = true,
        });

        await _dbContext.SaveChangesAsync();
    }

    private async Task<SamplingRoundDetailDto> CreateDraftRound(string name)
    {
        var dto = new SamplingRoundCreateDto(
            Name: name,
            Description: null,
            Deadline: null,
            Notes: null,
            DistributorId: DistributorId);

        return await _sut.CreateAsync(dto, UserId, TenantId);
    }

    private async Task<SamplingRoundDetailDto> CreateDraftRoundWithOrders(int orderCount)
    {
        var round = await CreateDraftRound("Test Round");

        for (var i = 0; i < orderCount; i++)
        {
            await CreateOrderInRound(round.Id, DistributorId, i);
        }

        return (await _sut.GetByIdAsync(round.Id, TenantId))!;
    }

    private async Task<Guid> CreateOrderInRound(Guid roundId, Guid distributorId, int sortOrder)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"ORD-{Guid.NewGuid().ToString()[..4]}",
            Status = OrderStatus.New,
            DistributorId = distributorId,
            SamplingRoundId = roundId,
            SortOrder = sortOrder,
            SamplingLocationId = distributorId == DistributorId ? SamplingLocationId : OtherDistributorLocationId,
            CreatedById = UserId,
            TenantId = TenantId,
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        return order.Id;
    }

    private async Task<Guid> CreateStandaloneOrder(Guid distributorId)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"ORD-{Guid.NewGuid().ToString()[..4]}",
            Status = OrderStatus.New,
            DistributorId = distributorId,
            SamplingRoundId = Guid.Empty,
            SamplingLocationId = distributorId == DistributorId ? SamplingLocationId : OtherDistributorLocationId,
            CreatedById = UserId,
            TenantId = TenantId,
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        return order.Id;
    }

    private async Task<SamplingRoundDetailDto> CreateRoundInStatus(SamplingRoundStatus status)
    {
        var round = await CreateDraftRoundWithOrders(orderCount: 1);

        var entity = await _dbContext.SamplingRounds.FindAsync(round.Id);
        entity!.Status = status;
        entity.PreleveurId = PreleveurId;
        await _dbContext.SaveChangesAsync();

        return (await _sut.GetByIdAsync(round.Id, TenantId))!;
    }

    private async Task TransitionToAssigned(Guid roundId)
    {
        var dto = new SamplingRoundAssignDto(PreleveurId: PreleveurId);
        await _sut.AssignPreleveurAsync(roundId, dto, UserId, TenantId);
    }

    private async Task TransitionToCompleted(Guid roundId)
    {
        var round = await _dbContext.SamplingRounds.FirstAsync(r => r.Id == roundId);
        round.Status = SamplingRoundStatus.Completed;
        await _dbContext.SaveChangesAsync();
    }
}
