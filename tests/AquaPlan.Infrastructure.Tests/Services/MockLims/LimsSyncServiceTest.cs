using AquaPlan.Application.DTOs.LimsSync;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services.MockLims;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace AquaPlan.Infrastructure.Tests.Services.MockLims;

public class LimsSyncServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly LimsSyncService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    public LimsSyncServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AquaPlanDbContext(options);
        _sut = new LimsSyncService(
            _dbContext,
            Options.Create(new LimsSyncOptions { Enabled = true, IntervalMinutes = 5 }));
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task WriteLogAsync_ShouldPersistEntryWithDuration()
    {
        var started = DateTime.UtcNow;
        var completed = started.AddMilliseconds(250);

        await _sut.WriteLogAsync(
            Guid.NewGuid(), Guid.NewGuid(), "PullResults",
            LimsSyncStatus.Success, "ok", started, completed, TenantId);

        var log = await _dbContext.LimsSyncLogs.SingleAsync();
        log.Status.Should().Be(LimsSyncStatus.Success);
        log.Operation.Should().Be("PullResults");
        log.DurationMs.Should().BeGreaterThanOrEqualTo(200);
    }

    [Fact]
    public async Task GetLogsAsync_ShouldFilterByStatusAndTenantAndOrderByRecent()
    {
        _dbContext.LimsSyncLogs.AddRange(
            BuildLog(TenantId, LimsSyncStatus.Success, DateTime.UtcNow.AddMinutes(-10)),
            BuildLog(TenantId, LimsSyncStatus.Error, DateTime.UtcNow.AddMinutes(-1)),
            BuildLog(OtherTenantId, LimsSyncStatus.Error, DateTime.UtcNow));
        await _dbContext.SaveChangesAsync();

        var all = await _sut.GetLogsAsync(TenantId);
        var errorsOnly = await _sut.GetLogsAsync(TenantId, status: LimsSyncStatus.Error);

        all.Should().HaveCount(2);
        all.First().Status.Should().Be(LimsSyncStatus.Error); // most recent first
        errorsOnly.Should().HaveCount(1);
    }

    [Fact]
    public async Task GetStatusAsync_ShouldAggregatePendingOrdersAndLastCycle()
    {
        _dbContext.Orders.AddRange(
            new Order { Id = Guid.NewGuid(), OrderNumber = "A", Status = OrderStatus.Transmitted, CreatedById = "u", DistributorId = Guid.NewGuid(), TenantId = TenantId, LimsOrderId = Guid.NewGuid() },
            new Order { Id = Guid.NewGuid(), OrderNumber = "B", Status = OrderStatus.Transmitted, CreatedById = "u", DistributorId = Guid.NewGuid(), TenantId = TenantId, LimsOrderId = Guid.NewGuid() },
            new Order { Id = Guid.NewGuid(), OrderNumber = "C", Status = OrderStatus.Done, CreatedById = "u", DistributorId = Guid.NewGuid(), TenantId = TenantId });
        _dbContext.LimsSyncLogs.Add(BuildLog(TenantId, LimsSyncStatus.Success, DateTime.UtcNow.AddMinutes(-2)));
        await _dbContext.SaveChangesAsync();

        var status = await _sut.GetStatusAsync(TenantId);

        status.IsEnabled.Should().BeTrue();
        status.IntervalMinutes.Should().Be(5);
        status.PendingOrdersCount.Should().Be(2);
        status.RecentLogsCount.Should().Be(1);
        status.LastCycleAt.Should().NotBeNull();
        status.NextCycleAt.Should().NotBeNull();
    }

    private static LimsSyncLog BuildLog(Guid tenantId, LimsSyncStatus status, DateTime completedAt)
    {
        return new LimsSyncLog
        {
            Id = Guid.NewGuid(),
            CycleId = Guid.NewGuid(),
            Operation = "PullResults",
            Status = status,
            StartedAt = completedAt.AddMilliseconds(-100),
            CompletedAt = completedAt,
            DurationMs = 100,
            TenantId = tenantId,
        };
    }
}
