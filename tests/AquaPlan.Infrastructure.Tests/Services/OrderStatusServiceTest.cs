using AquaPlan.Application.DTOs.MockLims;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AquaPlan.Infrastructure.Tests.Services;

public class OrderStatusServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<IOrderAuditService> _auditServiceMock = new();
    private readonly Mock<IMockLimsService> _mockLimsServiceMock = new();
    private readonly Mock<ILogger<OrderStatusService>> _loggerMock = new();
    private readonly Mock<ILogger<OrderTransmissionService>> _transmissionLoggerMock = new();
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

        // Sprint Robustesse F-105 — the Completed → Transmitted transition is delegated to the
        // shared transmission service. Mock LIMS enabled so the single-transition path is shown
        // to forward to the LIMS just like the bulk/round paths.
        _mockLimsServiceMock
            .Setup(s => s.ReceiveOrderAsync(It.IsAny<MockLimsOrderCreateDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MockLimsOrderCreatedDto(Guid.NewGuid(), DateTime.UtcNow));
        var mockLimsOptions = Options.Create(new MockLimsOptions { Enabled = true });
        var transmissionService = new OrderTransmissionService(
            _dbContext, _auditServiceMock.Object, _mockLimsServiceMock.Object, mockLimsOptions, _transmissionLoggerMock.Object);

        _sut = new OrderStatusService(_dbContext, _auditServiceMock.Object, transmissionService, _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public void GetAllStatuses_ShouldReturnAllSixStatuses()
    {
        var result = _sut.GetAllStatuses();

        result.Should().HaveCount(6);
    }

    [Fact]
    public void GetAllStatuses_ShouldContainAllEnumValues()
    {
        var result = _sut.GetAllStatuses();
        var statusValues = result.Select(s => s.Status).ToList();

        statusValues.Should().Contain(OrderStatus.New);
        statusValues.Should().Contain(OrderStatus.InProgress);
        statusValues.Should().Contain(OrderStatus.Completed);
        statusValues.Should().Contain(OrderStatus.Transmitted);
        statusValues.Should().Contain(OrderStatus.Done);
        statusValues.Should().Contain(OrderStatus.Cancelled);
    }

    [Fact]
    public void GetAllStatuses_ShouldMarkDoneAsTerminal()
    {
        var result = _sut.GetAllStatuses();
        var done = result.Single(s => s.Status == OrderStatus.Done);

        done.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void GetAllStatuses_ShouldMarkCancelledAsTerminal()
    {
        var result = _sut.GetAllStatuses();
        var cancelled = result.Single(s => s.Status == OrderStatus.Cancelled);

        cancelled.IsTerminal.Should().BeTrue();
    }

    [Fact]
    public void GetAllStatuses_ShouldMarkNewAsNonTerminal()
    {
        var result = _sut.GetAllStatuses();
        var newStatus = result.Single(s => s.Status == OrderStatus.New);

        newStatus.IsTerminal.Should().BeFalse();
    }

    [Theory]
    [InlineData(OrderStatus.New, OrderStatus.InProgress)]
    [InlineData(OrderStatus.New, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.InProgress, OrderStatus.Completed)]
    [InlineData(OrderStatus.InProgress, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Completed, OrderStatus.Transmitted)]
    [InlineData(OrderStatus.Completed, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Transmitted, OrderStatus.Done)]
    public void ValidateTransition_ShouldReturnTrue_WhenTransitionIsValid(OrderStatus from, OrderStatus to)
    {
        var result = _sut.ValidateTransition(from, to);

        result.Should().BeTrue();
    }

    [Theory]
    [InlineData(OrderStatus.New, OrderStatus.Done)]
    [InlineData(OrderStatus.New, OrderStatus.Transmitted)]
    [InlineData(OrderStatus.New, OrderStatus.Completed)]
    [InlineData(OrderStatus.InProgress, OrderStatus.New)]
    [InlineData(OrderStatus.InProgress, OrderStatus.Done)]
    [InlineData(OrderStatus.Completed, OrderStatus.New)]
    [InlineData(OrderStatus.Completed, OrderStatus.InProgress)]
    [InlineData(OrderStatus.Transmitted, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Transmitted, OrderStatus.New)]
    [InlineData(OrderStatus.Done, OrderStatus.New)]
    [InlineData(OrderStatus.Done, OrderStatus.Cancelled)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.New)]
    [InlineData(OrderStatus.Cancelled, OrderStatus.Done)]
    public void ValidateTransition_ShouldReturnFalse_WhenTransitionIsInvalid(OrderStatus from, OrderStatus to)
    {
        var result = _sut.ValidateTransition(from, to);

        result.Should().BeFalse();
    }

    [Fact]
    public void GetAllowedTransitions_FromNew_ShouldReturnInProgressAndCancelled()
    {
        var result = _sut.GetAllowedTransitions(OrderStatus.New);

        result.Should().HaveCount(2);
        result.Select(s => s.Status).Should().Contain(OrderStatus.InProgress);
        result.Select(s => s.Status).Should().Contain(OrderStatus.Cancelled);
    }

    [Fact]
    public void GetAllowedTransitions_FromDone_ShouldReturnEmpty()
    {
        var result = _sut.GetAllowedTransitions(OrderStatus.Done);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAllowedTransitions_FromCancelled_ShouldReturnEmpty()
    {
        var result = _sut.GetAllowedTransitions(OrderStatus.Cancelled);

        result.Should().BeEmpty();
    }

    [Fact]
    public void GetAllowedTransitions_FromTransmitted_ShouldReturnOnlyDone()
    {
        var result = _sut.GetAllowedTransitions(OrderStatus.Transmitted);

        result.Should().HaveCount(1);
        result.Single().Status.Should().Be(OrderStatus.Done);
    }

    [Fact]
    public async Task TransitionOrderAsync_ShouldUpdateStatus_WhenTransitionIsValid()
    {
        await SeedOrder(OrderStatus.New);

        var result = await _sut.TransitionOrderAsync(OrderId, OrderStatus.InProgress, UserId, TenantId);

        result.FromStatus.Should().Be(OrderStatus.New);
        result.ToStatus.Should().Be(OrderStatus.InProgress);
        result.TransitionDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));

        var order = await _dbContext.Orders.FindAsync(OrderId);
        order!.Status.Should().Be(OrderStatus.InProgress);
        order.StatusChangedAt.Should().NotBeNull();
        order.StatusChangedBy.Should().Be(UserId);
        order.UpdatedAt.Should().NotBeNull();
        order.UpdatedBy.Should().Be(UserId);
    }

    [Fact]
    public async Task TransitionOrderAsync_ShouldThrowInvalidOperationException_WhenTransitionIsInvalid()
    {
        await SeedOrder(OrderStatus.New);

        await _sut.Awaiting(x => x.TransitionOrderAsync(OrderId, OrderStatus.Done, UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*New*Done*not allowed*");
    }

    [Fact]
    public async Task TransitionOrderAsync_ShouldThrowKeyNotFoundException_WhenOrderDoesNotExist()
    {
        var nonExistentId = Guid.NewGuid();

        await _sut.Awaiting(x => x.TransitionOrderAsync(nonExistentId, OrderStatus.InProgress, UserId, TenantId))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task TransitionOrderAsync_ShouldThrowKeyNotFoundException_WhenOrderBelongsToOtherTenant()
    {
        await SeedOrder(OrderStatus.New);
        var otherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000099");

        await _sut.Awaiting(x => x.TransitionOrderAsync(OrderId, OrderStatus.InProgress, UserId, otherTenantId))
            .Should().ThrowAsync<KeyNotFoundException>();
    }

    [Fact]
    public async Task TransitionOrderAsync_ShouldNotAllowTransitionFromTerminalStatus()
    {
        await SeedOrder(OrderStatus.Done);

        await _sut.Awaiting(x => x.TransitionOrderAsync(OrderId, OrderStatus.New, UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task TransitionOrderAsync_ToCancelled_ShouldSucceedFromNew()
    {
        await SeedOrder(OrderStatus.New);

        var result = await _sut.TransitionOrderAsync(OrderId, OrderStatus.Cancelled, UserId, TenantId);

        result.FromStatus.Should().Be(OrderStatus.New);
        result.ToStatus.Should().Be(OrderStatus.Cancelled);
    }

    [Fact]
    public async Task TransitionOrderAsync_ShouldCreateAuditLogEntry_WhenTransitionIsValid()
    {
        await SeedOrder(OrderStatus.New);

        await _sut.TransitionOrderAsync(OrderId, OrderStatus.InProgress, UserId, TenantId);

        _auditServiceMock.Verify(
            a => a.LogAsync(
                OrderId,
                "StatusTransitioned",
                It.Is<string>(s => s.Contains("New") && s.Contains("InProgress")),
                "New",
                "InProgress",
                UserId,
                TenantId,
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task TransitionOrderAsync_ToTransmitted_ShouldSetTransmittedFieldsAndForwardToLims()
    {
        // F-105 — the single-order transition to Transmitted must behave exactly like the
        // bulk/round paths: TransmittedAt + StatusChangedAt/By set, Mock LIMS forwarded, audit.
        await SeedOrder(OrderStatus.Completed);

        var result = await _sut.TransitionOrderAsync(OrderId, OrderStatus.Transmitted, UserId, TenantId);

        result.ToStatus.Should().Be(OrderStatus.Transmitted);

        var reloaded = await _dbContext.Orders.SingleAsync(o => o.Id == OrderId);
        reloaded.Status.Should().Be(OrderStatus.Transmitted);
        reloaded.TransmittedAt.Should().NotBeNull();
        reloaded.StatusChangedBy.Should().Be(UserId);
        reloaded.LimsOrderId.Should().NotBeNull();

        _mockLimsServiceMock.Verify(
            s => s.ReceiveOrderAsync(It.IsAny<MockLimsOrderCreateDto>(), TenantId, It.IsAny<CancellationToken>()),
            Times.Once);
        _auditServiceMock.Verify(
            a => a.LogAsync(OrderId, "StatusTransitioned", It.IsAny<string>(),
                "Completed", "Transmitted", UserId, TenantId, It.IsAny<CancellationToken>()),
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
