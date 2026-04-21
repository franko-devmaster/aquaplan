using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Services;

/// <summary>
/// AQ-43 — Tests for the in-app notification service (persistence, tenant + user isolation,
/// mark-read idempotency, urgent flag, mock email trace).
/// </summary>
public class NotificationServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<ILogger<NotificationService>> _loggerMock = new();
    private readonly NotificationService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private const string UserId = "user-1";
    private const string OtherUserId = "user-2";

    public NotificationServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AquaPlanDbContext(options);
        _sut = new NotificationService(_dbContext, _loggerMock.Object);

        _dbContext.Users.Add(new AppUser { Id = UserId, UserName = UserId, Email = "user1@aq.ch", FirstName = "U", LastName = "One", TenantId = TenantId });
        _dbContext.Users.Add(new AppUser { Id = OtherUserId, UserName = OtherUserId, Email = "user2@aq.ch", FirstName = "U", LastName = "Two", TenantId = TenantId });
        _dbContext.SaveChanges();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CreateAsync_ShouldPersistNotificationAndMockEmailLog()
    {
        var id = await _sut.CreateAsync(UserId, NotificationType.RoundAssigned,
            "Nouvelle tournée", "Tournée T1 vous a été assignée.", TenantId,
            relatedEntityType: "SamplingRound", relatedEntityId: Guid.NewGuid());

        var persisted = await _dbContext.Notifications.SingleAsync(n => n.Id == id);
        persisted.UserId.Should().Be(UserId);
        persisted.Type.Should().Be(NotificationType.RoundAssigned);
        persisted.IsRead.Should().BeFalse();
        persisted.TenantId.Should().Be(TenantId);

        var log = await _dbContext.NotificationLogs.SingleAsync(l => l.NotificationId == id);
        log.ToEmail.Should().Be("user1@aq.ch");
        log.WasActuallySent.Should().BeFalse();
        log.Subject.Should().Contain("AquaPlan");
    }

    [Fact]
    public async Task CreateAsync_WithIsUrgent_ShouldPersistFlag()
    {
        var id = await _sut.CreateAsync(UserId, NotificationType.NonConformResult,
            "⚠ Non conforme", "3 paramètres non conformes.", TenantId, isUrgent: true);

        var persisted = await _dbContext.Notifications.SingleAsync(n => n.Id == id);
        persisted.IsUrgent.Should().BeTrue();

        var log = await _dbContext.NotificationLogs.SingleAsync(l => l.NotificationId == id);
        log.Subject.Should().Contain("URGENT");
    }

    [Fact]
    public async Task CreateAsync_WhenUserIdEmpty_ShouldThrow()
    {
        await _sut.Awaiting(s => s.CreateAsync(string.Empty, NotificationType.OrderAssigned,
                "t", "m", TenantId))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task GetForUserAsync_ShouldReturnOnlyUserNotifications()
    {
        await _sut.CreateAsync(UserId, NotificationType.OrderAssigned, "T1", "M1", TenantId);
        await _sut.CreateAsync(OtherUserId, NotificationType.OrderAssigned, "T2", "M2", TenantId);

        var list = await _sut.GetForUserAsync(UserId, TenantId, unreadOnly: false, take: 20);

        list.Items.Should().HaveCount(1);
        list.Items[0].Title.Should().Be("T1");
    }

    [Fact]
    public async Task GetForUserAsync_WhenUnreadOnly_ShouldFilterReadOnes()
    {
        var id1 = await _sut.CreateAsync(UserId, NotificationType.OrderAssigned, "T1", "M1", TenantId);
        await _sut.CreateAsync(UserId, NotificationType.OrderAssigned, "T2", "M2", TenantId);
        await _sut.MarkAsReadAsync(id1, UserId, TenantId);

        var list = await _sut.GetForUserAsync(UserId, TenantId, unreadOnly: true, take: 20);

        list.Items.Should().HaveCount(1);
        list.Items[0].Title.Should().Be("T2");
        list.UnreadCount.Should().Be(1);
    }

    [Fact]
    public async Task GetForUserAsync_ShouldSurfaceUrgentUnreadFirst()
    {
        await _sut.CreateAsync(UserId, NotificationType.ResultsReceived, "Normal", "ok", TenantId);
        await _sut.CreateAsync(UserId, NotificationType.NonConformResult, "Urgent", "!", TenantId, isUrgent: true);

        var list = await _sut.GetForUserAsync(UserId, TenantId, unreadOnly: false, take: 20);

        list.Items[0].Title.Should().Be("Urgent");
        list.UrgentUnreadCount.Should().Be(1);
    }

    [Fact]
    public async Task GetForUserAsync_ShouldEnforceTenantIsolation()
    {
        await _sut.CreateAsync(UserId, NotificationType.OrderAssigned, "Tenant1", "m", TenantId);

        var list = await _sut.GetForUserAsync(UserId, OtherTenantId, unreadOnly: false, take: 20);

        list.Items.Should().BeEmpty();
        list.UnreadCount.Should().Be(0);
    }

    [Fact]
    public async Task GetUnreadCountAsync_ShouldCountOnlyUnread()
    {
        var id1 = await _sut.CreateAsync(UserId, NotificationType.OrderAssigned, "T1", "M1", TenantId);
        await _sut.CreateAsync(UserId, NotificationType.OrderAssigned, "T2", "M2", TenantId);
        await _sut.MarkAsReadAsync(id1, UserId, TenantId);

        var count = await _sut.GetUnreadCountAsync(UserId, TenantId);

        count.Should().Be(1);
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenNotFound_ShouldReturnFalse()
    {
        var result = await _sut.MarkAsReadAsync(Guid.NewGuid(), UserId, TenantId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task MarkAsReadAsync_WhenOwnedByAnotherUser_ShouldThrowUnauthorized()
    {
        var id = await _sut.CreateAsync(OtherUserId, NotificationType.OrderAssigned, "T", "M", TenantId);

        await _sut.Awaiting(s => s.MarkAsReadAsync(id, UserId, TenantId))
            .Should().ThrowAsync<UnauthorizedAccessException>();
    }

    [Fact]
    public async Task MarkAsReadAsync_ShouldBeIdempotent()
    {
        var id = await _sut.CreateAsync(UserId, NotificationType.OrderAssigned, "T", "M", TenantId);

        (await _sut.MarkAsReadAsync(id, UserId, TenantId)).Should().BeTrue();
        (await _sut.MarkAsReadAsync(id, UserId, TenantId)).Should().BeTrue();

        var notification = await _dbContext.Notifications.SingleAsync(n => n.Id == id);
        notification.IsRead.Should().BeTrue();
        notification.ReadAt.Should().NotBeNull();
    }

    [Fact]
    public async Task MarkAllAsReadAsync_ShouldMarkEveryUnreadOfTheUserOnly()
    {
        await _sut.CreateAsync(UserId, NotificationType.OrderAssigned, "T1", "M1", TenantId);
        await _sut.CreateAsync(UserId, NotificationType.OrderAssigned, "T2", "M2", TenantId);
        await _sut.CreateAsync(OtherUserId, NotificationType.OrderAssigned, "OtherT", "M", TenantId);

        var updated = await _sut.MarkAllAsReadAsync(UserId, TenantId);

        updated.Should().Be(2);
        (await _sut.GetUnreadCountAsync(UserId, TenantId)).Should().Be(0);
        (await _sut.GetUnreadCountAsync(OtherUserId, TenantId)).Should().Be(1);
    }

    [Fact]
    public async Task GetLogsAsync_ShouldReturnRecentMostFirstScopedToTenant()
    {
        await _sut.CreateAsync(UserId, NotificationType.OrderAssigned, "Old", "m", TenantId);
        await _sut.CreateAsync(UserId, NotificationType.ResultsReceived, "New", "m", TenantId);
        // Log for another tenant — must not appear.
        _dbContext.Users.Add(new AppUser { Id = "stranger", UserName = "stranger", Email = "x@y.z", FirstName = "X", LastName = "Y", TenantId = OtherTenantId });
        await _dbContext.SaveChangesAsync();
        await _sut.CreateAsync("stranger", NotificationType.OrderAssigned, "Other", "m", OtherTenantId);

        var logs = await _sut.GetLogsAsync(TenantId, take: 10);

        logs.Should().HaveCount(2);
        logs[0].Subject.Should().Contain("New");
    }
}
