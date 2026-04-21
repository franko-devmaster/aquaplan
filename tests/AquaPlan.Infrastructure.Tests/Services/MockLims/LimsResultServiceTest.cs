using AquaPlan.Application.DTOs.MockLims;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services.MockLims;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Services.MockLims;

public class LimsResultServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<IMockLimsService> _mockLimsServiceMock = new();
    private readonly Mock<IOrderAuditService> _auditMock = new();
    private readonly Mock<INotificationService> _notificationMock = new();
    private readonly Mock<UserManager<AppUser>> _userManagerMock;
    private readonly Mock<ILogger<LimsResultService>> _loggerMock = new();
    private readonly LimsResultService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private const string UserId = "user-1";

    public LimsResultServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AquaPlanDbContext(options);

        var store = new Mock<IUserStore<AppUser>>();
        _userManagerMock = new Mock<UserManager<AppUser>>(
            store.Object, null!, null!, null!, null!, null!, null!, null!, null!);
        _userManagerMock
            .Setup(u => u.GetUsersInRoleAsync(It.IsAny<string>()))
            .ReturnsAsync(new List<AppUser>());

        _sut = new LimsResultService(
            _dbContext,
            _mockLimsServiceMock.Object,
            _auditMock.Object,
            _notificationMock.Object,
            _userManagerMock.Object,
            _loggerMock.Object);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private async Task<Order> SeedOrderAsync(
        OrderStatus status = OrderStatus.Transmitted,
        Guid? limsOrderId = null,
        Guid? tenantId = null)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-" + Guid.NewGuid().ToString("N")[..6],
            Status = status,
            CreatedById = UserId,
            DistributorId = DistributorId,
            TenantId = tenantId ?? TenantId,
            LimsOrderId = limsOrderId ?? Guid.NewGuid(),
            TransmittedAt = DateTime.UtcNow.AddMinutes(-10),
        };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        return order;
    }

    private static MockLimsResultListDto BuildLimsPayload(Guid limsOrderId, params (string code, decimal value, decimal? min, decimal? max)[] entries)
    {
        var results = entries
            .Select(e => new MockLimsResultDto(
                e.code, e.value, "mg/L", e.min, e.max,
                (!e.min.HasValue || e.value >= e.min.Value) && (!e.max.HasValue || e.value <= e.max.Value)))
            .ToList();
        return new MockLimsResultListDto(limsOrderId, results);
    }

    [Fact]
    public async Task PullAsync_WhenOrderMissing_ShouldReturnNull()
    {
        var outcome = await _sut.PullAsync(Guid.NewGuid(), TenantId);

        outcome.Should().BeNull();
    }

    [Fact]
    public async Task PullAsync_WhenCrossTenant_ShouldReturnNull()
    {
        var order = await SeedOrderAsync();

        var outcome = await _sut.PullAsync(order.Id, OtherTenantId);

        outcome.Should().BeNull();
    }

    [Fact]
    public async Task PullAsync_WhenResultsReady_ShouldPersistAndTransitionToDone()
    {
        var order = await SeedOrderAsync();
        _mockLimsServiceMock
            .Setup(s => s.GetResultsAsync(order.LimsOrderId!.Value, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildLimsPayload(order.LimsOrderId!.Value,
                ("PH", 7.2m, 6.5m, 8.5m),
                ("NITRATES", 12m, 0m, 40m)));

        var outcome = await _sut.PullAsync(order.Id, TenantId);

        outcome.Should().NotBeNull();
        outcome!.NewResultsCount.Should().Be(2);
        outcome.TransitionedToDone.Should().BeTrue();
        outcome.Results.TotalCount.Should().Be(2);
        outcome.Results.ConformCount.Should().Be(2);

        var refreshed = await _dbContext.Orders.FindAsync(order.Id);
        refreshed!.Status.Should().Be(OrderStatus.Done);
        refreshed.ResultsReceivedAt.Should().NotBeNull();

        var persisted = await _dbContext.SamplingResults.Where(r => r.OrderId == order.Id).ToListAsync();
        persisted.Should().HaveCount(2);
        // Audit trail for LIMS sync lives in lims_sync_logs (AQ-35), not in order_audit_logs.
        _auditMock.Verify(a => a.LogAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string?>(), It.IsAny<string?>(),
            It.IsAny<string?>(), It.IsAny<string>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task PullAsync_ShouldComputeConformityFromReferenceRange()
    {
        var order = await SeedOrderAsync();
        _mockLimsServiceMock
            .Setup(s => s.GetResultsAsync(order.LimsOrderId!.Value, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildLimsPayload(order.LimsOrderId!.Value,
                ("PH", 9.5m, 6.5m, 8.5m),  // above max → non-conform
                ("NITRATES", 5m, 0m, 40m),  // in range → conform
                ("E_COLI", 3m, 0m, 0m)));  // above max → non-conform

        var outcome = await _sut.PullAsync(order.Id, TenantId);

        outcome!.Results.ConformCount.Should().Be(1);
        outcome.Results.NonConformCount.Should().Be(2);
        var ph = outcome.Results.Items.Single(i => i.ParameterCode == "PH");
        ph.IsConform.Should().BeFalse();
        var nitrates = outcome.Results.Items.Single(i => i.ParameterCode == "NITRATES");
        nitrates.IsConform.Should().BeTrue();
    }

    [Fact]
    public async Task PullAsync_WhenAlreadyPulled_ShouldBeIdempotent()
    {
        var order = await SeedOrderAsync(status: OrderStatus.Done);
        _dbContext.SamplingResults.Add(new SamplingResult
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            ParameterCode = "PH",
            Value = 7m,
            Unit = "pH",
            ReferenceMin = 6.5m,
            ReferenceMax = 8.5m,
            IsConform = true,
            TenantId = TenantId,
            ReceivedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();

        var outcome = await _sut.PullAsync(order.Id, TenantId);

        outcome!.NewResultsCount.Should().Be(0);
        outcome.TransitionedToDone.Should().BeFalse();
        outcome.Results.TotalCount.Should().Be(1);
        _mockLimsServiceMock.Verify(s => s.GetResultsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task PullAsync_WhenResultsNotReady_ShouldReturnEmptyWithoutTransition()
    {
        var order = await SeedOrderAsync();
        _mockLimsServiceMock
            .Setup(s => s.GetResultsAsync(order.LimsOrderId!.Value, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MockLimsResultListDto(order.LimsOrderId!.Value, []));

        var outcome = await _sut.PullAsync(order.Id, TenantId);

        outcome!.NewResultsCount.Should().Be(0);
        outcome.TransitionedToDone.Should().BeFalse();
        outcome.Results.TotalCount.Should().Be(0);
        var refreshed = await _dbContext.Orders.FindAsync(order.Id);
        refreshed!.Status.Should().Be(OrderStatus.Transmitted);
    }

    [Fact]
    public async Task PullAsync_WhenOrderHasNoLimsOrderId_ShouldReturnEmpty()
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-NO-LIMS",
            Status = OrderStatus.Transmitted,
            CreatedById = UserId,
            DistributorId = DistributorId,
            TenantId = TenantId,
            LimsOrderId = null,
        };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var outcome = await _sut.PullAsync(order.Id, TenantId);

        outcome!.NewResultsCount.Should().Be(0);
        _mockLimsServiceMock.Verify(s => s.GetResultsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task GetByOrderAsync_WhenOrderMissing_ShouldReturnNull()
    {
        var dto = await _sut.GetByOrderAsync(Guid.NewGuid(), TenantId);
        dto.Should().BeNull();
    }

    [Fact]
    public async Task GetByOrderAsync_ShouldReturnSortedByParameterCode()
    {
        var order = await SeedOrderAsync(status: OrderStatus.Done);
        _dbContext.SamplingResults.AddRange(
            new SamplingResult { Id = Guid.NewGuid(), OrderId = order.Id, ParameterCode = "NITRATES", Value = 5m, Unit = "mg/L", ReferenceMin = 0m, ReferenceMax = 40m, IsConform = true, TenantId = TenantId, ReceivedAt = DateTime.UtcNow },
            new SamplingResult { Id = Guid.NewGuid(), OrderId = order.Id, ParameterCode = "E_COLI", Value = 0m, Unit = "UFC", ReferenceMin = 0m, ReferenceMax = 0m, IsConform = true, TenantId = TenantId, ReceivedAt = DateTime.UtcNow },
            new SamplingResult { Id = Guid.NewGuid(), OrderId = order.Id, ParameterCode = "AMMONIUM", Value = 0.1m, Unit = "mg/L", ReferenceMin = 0m, ReferenceMax = 0.5m, IsConform = true, TenantId = TenantId, ReceivedAt = DateTime.UtcNow });
        await _dbContext.SaveChangesAsync();

        var dto = await _sut.GetByOrderAsync(order.Id, TenantId);

        dto!.Items.Select(i => i.ParameterCode).Should().ContainInOrder("AMMONIUM", "E_COLI", "NITRATES");
        dto.TotalCount.Should().Be(3);
        dto.ConformCount.Should().Be(3);
        dto.NonConformCount.Should().Be(0);
    }

    [Fact]
    public async Task GetByOrderAsync_WithNoResults_ShouldReturnEmpty()
    {
        var order = await SeedOrderAsync();

        var dto = await _sut.GetByOrderAsync(order.Id, TenantId);

        dto.Should().NotBeNull();
        dto!.Items.Should().BeEmpty();
        dto.TotalCount.Should().Be(0);
    }

    // AQ-44 — when results are received, notify the mandate's creator and every admin.
    [Fact]
    public async Task PullAsync_WhenResultsReady_ShouldNotifyRequesterAndAdmins()
    {
        var order = await SeedOrderAsync();
        var admin = new AppUser { Id = "admin-1", UserName = "admin", Email = "admin@aq.ch", FirstName = "A", LastName = "Dmin", TenantId = TenantId };
        _dbContext.Users.Add(admin);
        await _dbContext.SaveChangesAsync();
        _userManagerMock.Setup(u => u.GetUsersInRoleAsync(RoleName.Administrator))
            .ReturnsAsync(new List<AppUser> { admin });
        _mockLimsServiceMock
            .Setup(s => s.GetResultsAsync(order.LimsOrderId!.Value, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildLimsPayload(order.LimsOrderId!.Value,
                ("PH", 7.2m, 6.5m, 8.5m),
                ("NITRATES", 12m, 0m, 40m)));

        await _sut.PullAsync(order.Id, TenantId);

        // Requester (UserId) receives a ResultsReceived notification.
        _notificationMock.Verify(n => n.CreateAsync(
            UserId, NotificationType.ResultsReceived, It.IsAny<string>(), It.IsAny<string>(),
            TenantId, "Order", order.Id, false, It.IsAny<CancellationToken>()),
            Times.Once);
        // Admin receives a ResultsReceived notification as well.
        _notificationMock.Verify(n => n.CreateAsync(
            "admin-1", NotificationType.ResultsReceived, It.IsAny<string>(), It.IsAny<string>(),
            TenantId, "Order", order.Id, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // AQ-44 — conformity counts are mentioned in the message.
    [Fact]
    public async Task PullAsync_ResultsReceivedMessage_ShouldMentionNonConformCount()
    {
        var order = await SeedOrderAsync();
        _mockLimsServiceMock
            .Setup(s => s.GetResultsAsync(order.LimsOrderId!.Value, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildLimsPayload(order.LimsOrderId!.Value,
                ("PH", 9.5m, 6.5m, 8.5m),  // non conform
                ("NITRATES", 12m, 0m, 40m)));

        await _sut.PullAsync(order.Id, TenantId);

        _notificationMock.Verify(n => n.CreateAsync(
            UserId, NotificationType.ResultsReceived, It.IsAny<string>(),
            It.Is<string>(m => m.Contains("non conforme")),
            TenantId, "Order", order.Id, false, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // AQ-45 — non conform results also raise an urgent notification (separate from AQ-44).
    [Fact]
    public async Task PullAsync_WhenNonConformResults_ShouldRaiseUrgentNonConformNotification()
    {
        var order = await SeedOrderAsync();
        _mockLimsServiceMock
            .Setup(s => s.GetResultsAsync(order.LimsOrderId!.Value, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildLimsPayload(order.LimsOrderId!.Value,
                ("PH", 9.5m, 6.5m, 8.5m),  // non conform
                ("E_COLI", 3m, 0m, 0m)));   // non conform

        await _sut.PullAsync(order.Id, TenantId);

        _notificationMock.Verify(n => n.CreateAsync(
            UserId, NotificationType.NonConformResult, It.IsAny<string>(), It.IsAny<string>(),
            TenantId, "Order", order.Id, true, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // AQ-45 — when every result is conform, no urgent notification is emitted.
    [Fact]
    public async Task PullAsync_WhenAllResultsConform_ShouldNotRaiseUrgentNotification()
    {
        var order = await SeedOrderAsync();
        _mockLimsServiceMock
            .Setup(s => s.GetResultsAsync(order.LimsOrderId!.Value, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(BuildLimsPayload(order.LimsOrderId!.Value,
                ("PH", 7.2m, 6.5m, 8.5m)));

        await _sut.PullAsync(order.Id, TenantId);

        _notificationMock.Verify(n => n.CreateAsync(
            It.IsAny<string>(), NotificationType.NonConformResult, It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    // AQ-44 — subsequent idempotent pulls (already persisted) do not re-notify recipients.
    [Fact]
    public async Task PullAsync_WhenAlreadyPulled_ShouldNotDuplicateNotifications()
    {
        var order = await SeedOrderAsync(status: OrderStatus.Done);
        _dbContext.SamplingResults.Add(new SamplingResult
        {
            Id = Guid.NewGuid(), OrderId = order.Id, ParameterCode = "PH", Value = 7m, Unit = "pH",
            ReferenceMin = 6.5m, ReferenceMax = 8.5m, IsConform = true, TenantId = TenantId, ReceivedAt = DateTime.UtcNow,
        });
        await _dbContext.SaveChangesAsync();

        await _sut.PullAsync(order.Id, TenantId);

        _notificationMock.Verify(n => n.CreateAsync(
            It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }
}
