using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Services;

public class OrderStatusServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<IOrderAuditService> _auditServiceMock = new();
    private readonly Mock<ILogger<OrderStatusService>> _loggerMock = new();
    private readonly OrderStatusService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OrderId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private const string UserId = "user-1";

    public OrderStatusServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AquaPlanDbContext(options);
        _sut = new OrderStatusService(_dbContext, _auditServiceMock.Object, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public void GetAllStatuses_ShouldReturnAllNineStatuses()
    {
        var result = _sut.GetAllStatuses();

        result.Should().HaveCount(9);
    }

    [Fact]
    public void GetAllStatuses_ShouldContainAllEnumValues()
    {
        var result = _sut.GetAllStatuses();
        var statusValues = result.Select(s => s.Status).ToList();

        statusValues.Should().Contain(OrderStatus.Draft);
        statusValues.Should().Contain(OrderStatus.Assigned);
        statusValues.Should().Contain(OrderStatus.InProgress);
        statusValues.Should().Contain(OrderStatus.SamplingCompleted);
        statusValues.Should().Contain(OrderStatus.Validated);
        statusValues.Should().Contain(OrderStatus.SentToLims);
        statusValues.Should().Contain(OrderStatus.ResultsReceived);
        statusValues.Should().Contain(OrderStatus.Completed);
        statusValues.Should().Contain(OrderStatus.Cancelled);
    }

    [Fact]
    public void GetAllStatuses_ShouldMarkCompletedAsTerminal()
    {
        var result = _sut.GetAllStatuses();
        var completed = result.Single(s => s.Status == OrderStatus.Completed);

        completed.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void GetAllStatuses_ShouldMarkCancelledAsTerminal()
    {
        var result = _sut.GetAllStatuses();
        var cancelled = result.Single(s => s.Status == OrderStatus.Cancelled);

        cancelled.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void GetAllStatuses_ShouldMarkDraftAsNonTerminal()
    {
        var result = _sut.GetAllStatuses();
        var draft = result.Single(s => s.Status == OrderStatus.Draft);

        draft.IsTerminal.Should().BeFalse();
    }

    [Theory]
    [InlineData(OrderStatus.Draft, OrderStatus.Assigned)]
    [InlineData(OrderStatus.Draft, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Assigned, OrderStatus.InProgress)]
    [InlineData(OrderStatus.Assigned, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.InProgress, OrderStatus.SamplingCompleted)]
    [InlineData(OrderStatus.InProgress, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.SamplingCompleted, OrderStatus.Validated)]
    [InlineData(OrderStatus.SamplingCompleted, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Validated, OrderStatus.SentToLims)]
    [InlineData(OrderStatus.Validated, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.SentToLims, OrderStatus.ResultsReceived)]
    [InlineData(OrderStatus.ResultsReceived, OrderStatus.Completed)]
    public void ValidateTransition_ShouldReturnTrue_WhenTransitionIsValid(OrderStatus from, OrderStatus to)
    {
        var result = _sut.ValidateTransition(from, to);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(OrderStatus.Draft, OrderStatus.Completed)]
    [InlineData(OrderStatus.Draft, OrderStatus.InProgress)]
    [InlineData(OrderStatus.Draft, OrderStatus.SentToLims)]
    [InlineData(OrderStatus.Assigned, OrderStatus.Completed)]
    [InlineData(OrderStatus.Assigned, OrderStatus.Draft)]
    [InlineData(OrderStatus.InProgress, OrderStatus.Draft)]
    [InlineData(OrderStatus.InProgress, OrderStatus.Assigned)]
    [InlineData(OrderStatus.SamplingCompleted, OrderStatus.Draft)]
    [InlineData(OrderStatus.Validated, OrderStatus.Draft)]
    [InlineData(OrderStatus.SentToLims, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.SentToLims, OrderStatus.Draft)]
    [InlineData(OrderStatus.ResultsReceived, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.ResultsReceived, OrderStatus.Draft)]
    [InlineData(OrderStatus.Completed, OrderStatus.Draft)]
    [InlineData(OrderStatus.Completed, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Draft)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Completed)]
    public void ValidateTransition_ShouldReturnFalse_WhenTransitionIsInvalid(OrderStatus from, OrderStatus to)
    {
        var result = _sut.ValidateTransition(from, to);

        result.Should().BeFalse();
    }

    [Fact]
    public void GetAllowedTransitions_FromDraft_ShouldReturnAssignedAndCancelled()
    {
        var result = _sut.GetAllowedTransitions(OrderStatus.Draft);

        result.Should().HaveCount(2);
        result.Select(s => s.Status).Should().Contain(OrderStatus.Assigned);
        result.Select(s => s.Status).Should().Contain(OrderStatus.Cancelled);
    }

    [Fact]
    public void GetAllowedTransitions_FromCompleted_ShouldReturnEmpty()
    {
        var result = _sut.GetAllowedTransitions(OrderStatus.Completed);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAllowedTransitions_FromCancelled_ShouldReturnEmpty()
    {
        var result = _sut.GetAllowedTransitions(OrderStatus.Cancelled);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAllowedTransitions_FromSentToLims_ShouldReturnOnlyResultsReceived()
    {
        var result = _sut.GetAllowedTransitions(OrderStatus.SentToLims);

        result.Should().HaveCount(1);
        result.Single().Status.Should().Be(OrderStatus.ResultsReceived);
    }

    [Fact]
    public async Task TransitionOrderAsync_ShouldUpdateStatus_WhenTransitionIsValid()
    {
        await SeedOrder(OrderStatus.Draft);

        var result = await _sut.TransitionOrderAsync(OrderId, OrderStatus.Assigned, UserId, TenantId);

        result.FromStatus.Should().Be(OrderStatus.Draft);
        result.ToStatus.Should().Be(OrderStatus.Assigned);
        result.TransitionDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var order = await _dbContext.Orders.FindAsync(OrderId);
        order!.Status.Should().Be(OrderStatus.Assigned);
        order.StatusChangedAt.Should().NotBeNull();
        order.StatusChangedBy.Should().Be(UserId);
        order.UpdatedAt.Should().NotBeNull();
        order.UpdatedBy.Should().Be(UserId);
    }

    [Fact]
    public async Task TransitionOrderAsync_ShouldThrowInvalidOperationException_WhenTransitionIsInvalid()
    {
        await SeedOrder(OrderStatus.Draft);

        await _sut.Awaiting(x => x.TransitionOrderAsync(OrderId, OrderStatus.Completed, UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Draft*Completed*not allowed*");
    }

    [Fact]
    public async Task TransitionOrderAsync_ShouldThrowKeyNotFoundException_WhenOrderDoesNotExist()
    {
        var nonExistentId = Guid.NewGuid();

        await _sut.Awaiting(x => x.TransitionOrderAsync(nonExistentId, OrderStatus.Assigned, UserId, TenantId))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task TransitionOrderAsync_ShouldThrowKeyNotFoundException_WhenOrderBelongsToOtherTenant()
    {
        await SeedOrder(OrderStatus.Draft);
        var otherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000099");

        await _sut.Awaiting(x => x.TransitionOrderAsync(OrderId, OrderStatus.Assigned, UserId, otherTenantId))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task TransitionOrderAsync_ShouldNotAllowTransitionFromTerminalStatus()
    {
        await SeedOrder(OrderStatus.Completed);

        await _sut.Awaiting(x => x.TransitionOrderAsync(OrderId, OrderStatus.Draft, UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task TransitionOrderAsync_ToCancelled_ShouldSucceedFromAssigned()
    {
        await SeedOrder(OrderStatus.Assigned);

        var result = await _sut.TransitionOrderAsync(OrderId, OrderStatus.Cancelled, UserId, TenantId);

        result.FromStatus.Should().Be(OrderStatus.Assigned);
        result.ToStatus.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task TransitionOrderAsync_ShouldCreateAuditLogEntry_WhenTransitionIsValid()
    {
        await SeedOrder(OrderStatus.Draft);

        await _sut.TransitionOrderAsync(OrderId, OrderStatus.Assigned, UserId, TenantId);

        _auditServiceMock.Verify(
            a => a.LogAsync(
                OrderId,
                "StatusTransitioned",
                It.Is<string>(s => s.Contains("Draft") && s.Contains("Assigned")),
                "Draft",
                "Assigned",
                UserId,
                TenantId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    private async Task SeedOrder(OrderStatus status)
    {
        _dbContext.Orders.Add(new Order
        {
            Id = OrderId,
            OrderNumber = "ORD-001",
            Status = status,
            CreatedById = UserId,
            DistributorId = Guid.NewGuid(),
            TenantId = TenantId,
        });
        await _dbContext.SaveChangesAsync();
    }
}
