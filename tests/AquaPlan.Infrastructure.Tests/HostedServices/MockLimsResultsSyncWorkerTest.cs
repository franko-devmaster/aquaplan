using AquaPlan.Application.DTOs.LimsSync;
using AquaPlan.Application.DTOs.SamplingResults;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.HostedServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AquaPlan.Infrastructure.Tests.HostedServices;

public class MockLimsResultsSyncWorkerTest : IDisposable
{
    private readonly ServiceProvider _provider;
    private readonly Mock<ILimsResultService> _resultServiceMock = new();
    private readonly Mock<ILimsSyncService> _syncServiceMock = new();
    private readonly AquaPlanDbContext _dbContext;
    private readonly MockLimsResultsSyncWorker _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public MockLimsResultsSyncWorkerTest()
    {
        var services = new ServiceCollection();
        var dbName = Guid.NewGuid().ToString();
        services.AddDbContext<AquaPlanDbContext>(o => o.UseInMemoryDatabase(dbName));
        services.AddSingleton(_resultServiceMock.Object);
        services.AddSingleton(_syncServiceMock.Object);
        _provider = services.BuildServiceProvider();
        _dbContext = _provider.GetRequiredService<AquaPlanDbContext>();

        _sut = new MockLimsResultsSyncWorker(
            _provider.GetRequiredService<IServiceScopeFactory>(),
            Options.Create(new LimsSyncOptions { Enabled = true, IntervalMinutes = 5 }),
            new Mock<ILogger<MockLimsResultsSyncWorker>>().Object);
    }

    public void Dispose()
    {
        _provider.Dispose();
    }

    private async Task<Order> SeedOrderAsync(OrderStatus status, bool withResult = false)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-" + Guid.NewGuid().ToString("N")[..6],
            Status = status,
            CreatedById = "u1",
            DistributorId = Guid.NewGuid(),
            TenantId = TenantId,
            LimsOrderId = Guid.NewGuid(),
        };
        _dbContext.Orders.Add(order);
        if (withResult)
        {
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
        }
        await _dbContext.SaveChangesAsync();
        return order;
    }

    private static PullResultsOutcome MakeSuccess(int newCount, bool transitioned) =>
        new(new SamplingResultListDto([], 0, 0, 0), newCount, transitioned);

    [Fact]
    public async Task RunCycleAsync_WhenTransmittedOrdersExist_ShouldPullForEach()
    {
        var o1 = await SeedOrderAsync(OrderStatus.Transmitted);
        var o2 = await SeedOrderAsync(OrderStatus.Transmitted);
        _resultServiceMock.Setup(r => r.PullAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSuccess(3, true));

        await _sut.RunCycleAsync(CancellationToken.None);

        _resultServiceMock.Verify(r => r.PullAsync(o1.Id, TenantId, It.IsAny<CancellationToken>()), Times.Once);
        _resultServiceMock.Verify(r => r.PullAsync(o2.Id, TenantId, It.IsAny<CancellationToken>()), Times.Once);
        _syncServiceMock.Verify(s => s.WriteLogAsync(
            It.IsAny<Guid>(), It.IsAny<Guid?>(), "PullResults", LimsSyncStatus.Success,
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), TenantId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task RunCycleAsync_ShouldSkipOrdersWithExistingResults()
    {
        await SeedOrderAsync(OrderStatus.Transmitted, withResult: true);

        await _sut.RunCycleAsync(CancellationToken.None);

        _resultServiceMock.Verify(r => r.PullAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RunCycleAsync_WhenPullReturnsNoContent_ShouldLogNoContent()
    {
        await SeedOrderAsync(OrderStatus.Transmitted);
        _resultServiceMock.Setup(r => r.PullAsync(It.IsAny<Guid>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSuccess(0, transitioned: false));

        await _sut.RunCycleAsync(CancellationToken.None);

        _syncServiceMock.Verify(s => s.WriteLogAsync(
            It.IsAny<Guid>(), It.IsAny<Guid?>(), "PullResults", LimsSyncStatus.NoContent,
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), TenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunCycleAsync_WhenPullThrows_ShouldLogErrorAndContinue()
    {
        var o1 = await SeedOrderAsync(OrderStatus.Transmitted);
        var o2 = await SeedOrderAsync(OrderStatus.Transmitted);
        _resultServiceMock.Setup(r => r.PullAsync(o1.Id, TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("boom"));
        _resultServiceMock.Setup(r => r.PullAsync(o2.Id, TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(MakeSuccess(2, true));

        await _sut.RunCycleAsync(CancellationToken.None);

        _syncServiceMock.Verify(s => s.WriteLogAsync(
            It.IsAny<Guid>(), o1.Id, "PullResults", LimsSyncStatus.Error,
            "boom", It.IsAny<DateTime>(), It.IsAny<DateTime>(), TenantId, It.IsAny<CancellationToken>()),
            Times.Once);
        _syncServiceMock.Verify(s => s.WriteLogAsync(
            It.IsAny<Guid>(), o2.Id, "PullResults", LimsSyncStatus.Success,
            It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), TenantId, It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task RunCycleAsync_WhenNoPendingOrders_ShouldNotPull()
    {
        await SeedOrderAsync(OrderStatus.Completed);

        await _sut.RunCycleAsync(CancellationToken.None);

        _resultServiceMock.Verify(r => r.PullAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }
}
