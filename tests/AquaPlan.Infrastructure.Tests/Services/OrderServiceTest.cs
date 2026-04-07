using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Services;

public class OrderServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<ILogger<OrderService>> _loggerMock = new();
    private readonly OrderService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private const string UserId = "user-1";

    public OrderServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AquaPlanDbContext(options);
        _sut = new OrderService(_dbContext, _loggerMock.Object);

        SeedData().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldSetUnplannedReason_WhenUnplanned()
    {
        var dto = new OrderCreateDto(
            DistributorId: DistributorId,
            SamplingLocationId: null,
            PreleveurId: null,
            PlannedDate: null,
            AnalysisProfileIds: null,
            Notes: null,
            IsUnplanned: true,
            UnplannedReason: UnplannedReason.Pollution,
            UnplannedReasonDetails: "Contamination detected upstream");

        var result = await _sut.CreateOrderAsync(dto, UserId, TenantId);

        result.IsUnplanned.Should().BeTrue();
        result.UnplannedReason.Should().Be(UnplannedReason.Pollution);
        result.UnplannedReasonDetails.Should().Be("Contamination detected upstream");
        result.Status.Should().Be(OrderStatus.InProgress);
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldThrow_WhenUnplannedWithoutReason()
    {
        var dto = new OrderCreateDto(
            DistributorId: DistributorId,
            SamplingLocationId: null,
            PreleveurId: null,
            PlannedDate: null,
            AnalysisProfileIds: null,
            Notes: null,
            IsUnplanned: true,
            UnplannedReason: null,
            UnplannedReasonDetails: null);

        await _sut.Awaiting(s => s.CreateOrderAsync(dto, UserId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*UnplannedReason*");
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldNotSetReason_WhenPlanned()
    {
        var dto = new OrderCreateDto(
            DistributorId: DistributorId,
            SamplingLocationId: null,
            PreleveurId: null,
            PlannedDate: DateTime.UtcNow.AddDays(7),
            AnalysisProfileIds: null,
            Notes: null,
            IsUnplanned: false,
            UnplannedReason: UnplannedReason.Urgency,
            UnplannedReasonDetails: "Should be ignored");

        var result = await _sut.CreateOrderAsync(dto, UserId, TenantId);

        result.IsUnplanned.Should().BeFalse();
        result.UnplannedReason.Should().BeNull();
        result.UnplannedReasonDetails.Should().BeNull();
        result.Status.Should().Be(OrderStatus.Draft);
    }

    [Theory]
    [InlineData(UnplannedReason.Pollution)]
    [InlineData(UnplannedReason.Urgency)]
    [InlineData(UnplannedReason.ComplementaryControl)]
    public async Task CreateOrderAsync_ShouldAcceptAllReasonValues(UnplannedReason reason)
    {
        var dto = new OrderCreateDto(
            DistributorId: DistributorId,
            SamplingLocationId: null,
            PreleveurId: null,
            PlannedDate: null,
            AnalysisProfileIds: null,
            Notes: null,
            IsUnplanned: true,
            UnplannedReason: reason,
            UnplannedReasonDetails: null);

        var result = await _sut.CreateOrderAsync(dto, UserId, TenantId);

        result.UnplannedReason.Should().Be(reason);
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

        await _dbContext.SaveChangesAsync();
    }
}
