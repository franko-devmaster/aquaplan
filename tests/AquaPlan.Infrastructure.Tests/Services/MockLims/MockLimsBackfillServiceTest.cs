using AquaPlan.Application.DTOs.MockLims;
using AquaPlan.Application.DTOs.SamplingResults;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services.MockLims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace AquaPlan.Infrastructure.Tests.Services.MockLims;

public class MockLimsBackfillServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<IMockLimsService> _mockLimsServiceMock = new();
    private readonly Mock<ILimsResultService> _limsResultServiceMock = new();
    private readonly MockLimsBackfillService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public MockLimsBackfillServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AquaPlanDbContext(options);

        _sut = new MockLimsBackfillService(
            _dbContext,
            _mockLimsServiceMock.Object,
            _limsResultServiceMock.Object,
            NullLogger<MockLimsBackfillService>.Instance);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    private Order SeedOrder(
        OrderStatus status = OrderStatus.Transmitted,
        Guid? limsOrderId = null,
        Guid? tenantId = null,
        string? orderNumber = null)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber ?? $"ORD-{Guid.NewGuid().ToString()[..8]}",
            Status = status,
            LimsOrderId = limsOrderId,
            TenantId = tenantId ?? TenantId,
            CreatedAt = DateTime.UtcNow,
            CreatedById = "u-1",
            PlannedDate = DateTime.UtcNow.Date,
        };
        _dbContext.Orders.Add(order);
        _dbContext.SaveChanges();
        return order;
    }

    private void SetupHappyPath(Guid limsOrderId)
    {
        _mockLimsServiceMock
            .Setup(s => s.ReceiveOrderAsync(It.IsAny<MockLimsOrderCreateDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MockLimsOrderCreatedDto(limsOrderId, DateTime.UtcNow));

        _limsResultServiceMock
            .Setup(s => s.PullAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullResultsOutcome(
                new SamplingResultListDto(Array.Empty<SamplingResultDto>(), 0, 0, 0),
                NewResultsCount: 3,
                TransitionedToDone: true));
    }

    [Fact]
    public async Task BackfillAsync_ShouldProcessTransmittedOrdersWithoutLimsOrderId()
    {
        var order = SeedOrder();
        var returnedLimsId = Guid.NewGuid();
        SetupHappyPath(returnedLimsId);

        var result = await _sut.BackfillAsync(TenantId, maxOrders: 100, CancellationToken.None);

        result.TotalEligible.Should().Be(1);
        result.Processed.Should().Be(1);
        result.SuccessCount.Should().Be(1);
        result.FailureCount.Should().Be(0);
        result.Failures.Should().BeEmpty();

        var reloaded = await _dbContext.Orders.SingleAsync(o => o.Id == order.Id);
        reloaded.LimsOrderId.Should().Be(returnedLimsId);
        reloaded.TransmittedAt.Should().NotBeNull();

        _mockLimsServiceMock.Verify(
            s => s.ReceiveOrderAsync(It.IsAny<MockLimsOrderCreateDto>(), TenantId, It.IsAny<CancellationToken>()),
            Times.Once);
        _limsResultServiceMock.Verify(
            s => s.PullAsync(order.Id, TenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task BackfillAsync_WhenOrderHasLimsOrderId_ShouldSkip()
    {
        SeedOrder(limsOrderId: Guid.NewGuid(), orderNumber: "ORD-KEEP");
        SetupHappyPath(Guid.NewGuid());

        var result = await _sut.BackfillAsync(TenantId, maxOrders: 100, CancellationToken.None);

        result.TotalEligible.Should().Be(0);
        result.Processed.Should().Be(0);
        result.SuccessCount.Should().Be(0);
        result.FailureCount.Should().Be(0);

        _mockLimsServiceMock.Verify(
            s => s.ReceiveOrderAsync(It.IsAny<MockLimsOrderCreateDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _limsResultServiceMock.Verify(
            s => s.PullAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BackfillAsync_WhenOrderIsNotTransmitted_ShouldSkip()
    {
        SeedOrder(status: OrderStatus.New);
        SeedOrder(status: OrderStatus.Done);
        SetupHappyPath(Guid.NewGuid());

        var result = await _sut.BackfillAsync(TenantId, maxOrders: 100, CancellationToken.None);

        result.TotalEligible.Should().Be(0);
        result.Processed.Should().Be(0);
        _mockLimsServiceMock.Verify(
            s => s.ReceiveOrderAsync(It.IsAny<MockLimsOrderCreateDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BackfillAsync_WhenReceiveOrderFails_ShouldLogFailureAndContinue()
    {
        var ok1 = SeedOrder(orderNumber: "ORD-OK1");
        var boom = SeedOrder(orderNumber: "ORD-BOOM");
        var ok2 = SeedOrder(orderNumber: "ORD-OK2");

        var limsOk1 = Guid.NewGuid();
        var limsOk2 = Guid.NewGuid();

        _mockLimsServiceMock
            .Setup(s => s.ReceiveOrderAsync(
                It.Is<MockLimsOrderCreateDto>(d => d.OrderReference == "ORD-OK1"),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MockLimsOrderCreatedDto(limsOk1, DateTime.UtcNow));

        _mockLimsServiceMock
            .Setup(s => s.ReceiveOrderAsync(
                It.Is<MockLimsOrderCreateDto>(d => d.OrderReference == "ORD-BOOM"),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("mock-lims-unreachable"));

        _mockLimsServiceMock
            .Setup(s => s.ReceiveOrderAsync(
                It.Is<MockLimsOrderCreateDto>(d => d.OrderReference == "ORD-OK2"),
                It.IsAny<Guid>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MockLimsOrderCreatedDto(limsOk2, DateTime.UtcNow));

        _limsResultServiceMock
            .Setup(s => s.PullAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PullResultsOutcome(
                new SamplingResultListDto(Array.Empty<SamplingResultDto>(), 0, 0, 0),
                NewResultsCount: 1,
                TransitionedToDone: true));

        var result = await _sut.BackfillAsync(TenantId, maxOrders: 100, CancellationToken.None);

        result.TotalEligible.Should().Be(3);
        result.Processed.Should().Be(3);
        result.SuccessCount.Should().Be(2);
        result.FailureCount.Should().Be(1);
        result.Failures.Should().ContainSingle();
        var failure = result.Failures[0];
        failure.OrderId.Should().Be(boom.Id);
        failure.OrderNumber.Should().Be("ORD-BOOM");
        failure.Error.Should().Contain("mock-lims-unreachable");

        // Even though ORD-BOOM is in the middle, the loop continued and reconciled the others.
        (await _dbContext.Orders.FindAsync(ok1.Id))!.LimsOrderId.Should().Be(limsOk1);
        (await _dbContext.Orders.FindAsync(ok2.Id))!.LimsOrderId.Should().Be(limsOk2);
        (await _dbContext.Orders.FindAsync(boom.Id))!.LimsOrderId.Should().BeNull();
    }

    [Fact]
    public async Task BackfillAsync_WhenPullAsyncFails_ShouldRecordFailureButKeepLimsOrderIdPersisted()
    {
        var order = SeedOrder(orderNumber: "ORD-PULLFAIL");
        var returnedLimsId = Guid.NewGuid();
        _mockLimsServiceMock
            .Setup(s => s.ReceiveOrderAsync(It.IsAny<MockLimsOrderCreateDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MockLimsOrderCreatedDto(returnedLimsId, DateTime.UtcNow));
        _limsResultServiceMock
            .Setup(s => s.PullAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("pull-failed"));

        var result = await _sut.BackfillAsync(TenantId, maxOrders: 100, CancellationToken.None);

        result.SuccessCount.Should().Be(0);
        result.FailureCount.Should().Be(1);
        result.Failures[0].OrderId.Should().Be(order.Id);
        result.Failures[0].Error.Should().Contain("pull-failed");

        // Outbound already succeeded before the failure, so LimsOrderId must remain persisted.
        var reloaded = await _dbContext.Orders.FindAsync(order.Id);
        reloaded!.LimsOrderId.Should().Be(returnedLimsId);
    }

    [Fact]
    public async Task BackfillAsync_ShouldRespectMaxOrdersLimit()
    {
        for (int i = 0; i < 5; i++)
        {
            SeedOrder(orderNumber: $"ORD-{i}");
        }
        SetupHappyPath(Guid.NewGuid());

        var result = await _sut.BackfillAsync(TenantId, maxOrders: 2, CancellationToken.None);

        result.TotalEligible.Should().Be(5);
        result.Processed.Should().Be(2);
        result.SuccessCount.Should().Be(2);
        _mockLimsServiceMock.Verify(
            s => s.ReceiveOrderAsync(It.IsAny<MockLimsOrderCreateDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task BackfillAsync_ShouldIsolateByTenant()
    {
        var otherTenant = Guid.Parse("00000000-0000-0000-0000-0000000000ff");
        SeedOrder(tenantId: otherTenant, orderNumber: "ORD-OTHER");
        var mine = SeedOrder(orderNumber: "ORD-MINE");
        SetupHappyPath(Guid.NewGuid());

        var result = await _sut.BackfillAsync(TenantId, maxOrders: 100, CancellationToken.None);

        result.TotalEligible.Should().Be(1);
        result.Processed.Should().Be(1);
        _mockLimsServiceMock.Verify(
            s => s.ReceiveOrderAsync(
                It.Is<MockLimsOrderCreateDto>(d => d.OrderReference == "ORD-MINE"),
                TenantId,
                It.IsAny<CancellationToken>()),
            Times.Once);
        _mockLimsServiceMock.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BackfillAsync_WithInvalidMaxOrders_ShouldThrow()
    {
        await _sut.Awaiting(s => s.BackfillAsync(TenantId, maxOrders: 0, CancellationToken.None))
            .Should().ThrowAsync<ArgumentOutOfRangeException>();
    }
}
