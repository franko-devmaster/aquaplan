using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.DTOs.Samplings;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Services;

public class SamplingServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<IOrderAuditService> _auditServiceMock = new();
    private readonly Mock<ILogger<SamplingService>> _loggerMock = new();
    private readonly SamplingService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid OtherTenantId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid SamplingLocationId = Guid.Parse("00000000-0000-0000-0000-000000000020");
    private const string PreleveurId = "preleveur-1";
    private const string OtherPreleveurId = "preleveur-2";
    private const string ValidatorId = "validator-1";

    public SamplingServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AquaPlanDbContext(options);
        _sut = new SamplingService(_dbContext, _auditServiceMock.Object, _loggerMock.Object);

        SeedData().GetAwaiter().GetResult();
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    // --- GetByOrderIdAsync ---

    [Fact]
    public async Task GetByOrderIdAsync_ShouldReturnSamplingForOrder()
    {
        var orderId = await CreateOrderWithSampling(OrderStatus.InProgress);

        var result = await _sut.GetByOrderIdAsync(orderId, TenantId);

        result.Should().NotBeNull();
        result!.OrderId.Should().Be(orderId);
        result.PreleveurId.Should().Be(PreleveurId);
        result.PreleveurName.Should().Be("Pierre Martin");
    }

    [Fact]
    public async Task GetByOrderIdAsync_WhenNotFound_ShouldReturnNull()
    {
        var result = await _sut.GetByOrderIdAsync(Guid.NewGuid(), TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetByOrderIdAsync_WhenDifferentTenant_ShouldReturnNull()
    {
        var orderId = await CreateOrderWithSampling(OrderStatus.InProgress);

        var result = await _sut.GetByOrderIdAsync(orderId, OtherTenantId);

        result.Should().BeNull();
    }

    // --- CreateAsync ---

    [Fact]
    public async Task CreateAsync_ShouldCreateSampling()
    {
        var orderId = await CreateOrder(OrderStatus.InProgress, PreleveurId);
        var dto = CreateSamplingDto(orderId);

        var result = await _sut.CreateAsync(dto, PreleveurId, TenantId);

        result.Should().NotBeNull();
        result.OrderId.Should().Be(orderId);
        result.PreleveurId.Should().Be(PreleveurId);
        result.Temperature.Should().Be(15.5);
        result.Weather.Should().Be("Sunny");
        result.Notes.Should().Be("Test notes");
        result.IsValidated.Should().BeFalse();
    }

    [Fact]
    public async Task CreateAsync_WhenOrderNotFound_ShouldThrow()
    {
        var dto = CreateSamplingDto(Guid.NewGuid());

        await _sut.Awaiting(s => s.CreateAsync(dto, PreleveurId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("Order not found.");
    }

    [Fact]
    public async Task CreateAsync_WhenOrderNotInProgress_ShouldThrow()
    {
        var orderId = await CreateOrder(OrderStatus.New, PreleveurId);
        var dto = CreateSamplingDto(orderId);

        await _sut.Awaiting(s => s.CreateAsync(dto, PreleveurId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Order must be InProgress*");
    }

    [Fact]
    public async Task CreateAsync_WhenNotAssignedPreleveur_ShouldThrow()
    {
        var orderId = await CreateOrder(OrderStatus.InProgress, PreleveurId);
        var dto = CreateSamplingDto(orderId);

        await _sut.Awaiting(s => s.CreateAsync(dto, OtherPreleveurId, TenantId))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*assigned préleveur*");
    }

    [Fact]
    public async Task CreateAsync_WhenSamplingAlreadyExists_ShouldThrow()
    {
        var orderId = await CreateOrderWithSampling(OrderStatus.InProgress);
        var dto = CreateSamplingDto(orderId);

        await _sut.Awaiting(s => s.CreateAsync(dto, PreleveurId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*already exists*");
    }

    [Fact]
    public async Task CreateAsync_WhenPreleveurViaRound_ShouldSucceed()
    {
        var orderId = await CreateOrderWithRound(OrderStatus.InProgress, PreleveurId);
        var dto = CreateSamplingDto(orderId);

        var result = await _sut.CreateAsync(dto, PreleveurId, TenantId);

        result.Should().NotBeNull();
        result.OrderId.Should().Be(orderId);
    }

    // --- UpdateAsync ---

    [Fact]
    public async Task UpdateAsync_ShouldUpdateSamplingFields()
    {
        var orderId = await CreateOrderWithSampling(OrderStatus.InProgress);
        var dto = new SamplingCreateDto(
            OrderId: orderId,
            SamplingDateTime: new DateTime(2026, 5, 1, 10, 0, 0, DateTimeKind.Utc),
            Temperature: 20.0,
            Weather: "Rainy",
            LocationLat: 47.0,
            LocationLng: 7.2,
            Notes: "Updated notes",
            HasWaterSoftener: null,
            IsChlorinated: false);

        var result = await _sut.UpdateAsync(orderId, dto, PreleveurId, TenantId);

        result.Should().NotBeNull();
        result!.Temperature.Should().Be(20.0);
        result.Weather.Should().Be("Rainy");
        result.Notes.Should().Be("Updated notes");
    }

    [Fact]
    public async Task UpdateAsync_WhenOrderNotFound_ShouldReturnNull()
    {
        var dto = CreateSamplingDto(Guid.NewGuid());

        var result = await _sut.UpdateAsync(Guid.NewGuid(), dto, PreleveurId, TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task UpdateAsync_WhenOrderNotInProgress_ShouldThrow()
    {
        var orderId = await CreateOrderWithSampling(OrderStatus.Completed);
        var dto = CreateSamplingDto(orderId);

        await _sut.Awaiting(s => s.UpdateAsync(orderId, dto, PreleveurId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Order must be InProgress*");
    }

    [Fact]
    public async Task UpdateAsync_WhenNotAssignedPreleveur_ShouldThrow()
    {
        var orderId = await CreateOrderWithSampling(OrderStatus.InProgress);
        var dto = CreateSamplingDto(orderId);

        await _sut.Awaiting(s => s.UpdateAsync(orderId, dto, OtherPreleveurId, TenantId))
            .Should().ThrowAsync<UnauthorizedAccessException>()
            .WithMessage("*assigned préleveur*");
    }

    // --- CompleteAsync ---

    [Fact]
    public async Task CompleteAsync_ShouldTransitionToCompleted()
    {
        var orderId = await CreateOrderWithSampling(OrderStatus.InProgress);

        var result = await _sut.CompleteAsync(orderId, PreleveurId, TenantId);

        result.Should().BeTrue();

        var order = await _dbContext.Orders.FindAsync(orderId);
        order!.Status.Should().Be(OrderStatus.Completed);
        order.StatusChangedBy.Should().Be(PreleveurId);
    }

    [Fact]
    public async Task CompleteAsync_WhenOrderNotFound_ShouldReturnFalse()
    {
        var result = await _sut.CompleteAsync(Guid.NewGuid(), PreleveurId, TenantId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task CompleteAsync_WhenNoSamplingData_ShouldThrow()
    {
        var orderId = await CreateOrder(OrderStatus.InProgress, PreleveurId);

        await _sut.Awaiting(s => s.CompleteAsync(orderId, PreleveurId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no sampling data*");
    }

    [Fact]
    public async Task CompleteAsync_WhenNotInProgress_ShouldThrow()
    {
        var orderId = await CreateOrderWithSampling(OrderStatus.Completed);

        await _sut.Awaiting(s => s.CompleteAsync(orderId, PreleveurId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Order must be InProgress*");
    }

    // --- ValidateAsync ---

    [Fact]
    public async Task ValidateAsync_ShouldMarkAsValidated()
    {
        var orderId = await CreateOrderWithSampling(OrderStatus.Completed);

        var result = await _sut.ValidateAsync(orderId, ValidatorId, TenantId);

        result.Should().BeTrue();

        var order = await _dbContext.Orders.Include(o => o.Sampling).FirstAsync(o => o.Id == orderId);
        order.Status.Should().Be(OrderStatus.Completed);
        order.Sampling!.IsValidated.Should().BeTrue();
        order.Sampling.ValidatedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateAsync_WhenOrderNotFound_ShouldReturnFalse()
    {
        var result = await _sut.ValidateAsync(Guid.NewGuid(), ValidatorId, TenantId);

        result.Should().BeFalse();
    }

    [Fact]
    public async Task ValidateAsync_WhenNotCompleted_ShouldThrow()
    {
        var orderId = await CreateOrderWithSampling(OrderStatus.InProgress);

        await _sut.Awaiting(s => s.ValidateAsync(orderId, ValidatorId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Order must be Completed*");
    }

    [Fact]
    public async Task ValidateAsync_WhenNoSamplingData_ShouldThrow()
    {
        var orderId = await CreateOrder(OrderStatus.Completed, PreleveurId);

        await _sut.Awaiting(s => s.ValidateAsync(orderId, ValidatorId, TenantId))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*no sampling data found*");
    }

    // --- Helpers ---

    private async Task SeedData()
    {
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

        _dbContext.Users.Add(new AppUser
        {
            Id = OtherPreleveurId,
            UserName = "preleveur2@test.com",
            Email = "preleveur2@test.com",
            FirstName = "Jean",
            LastName = "Dupont",
            TenantId = TenantId,
            IsActive = true,
        });

        _dbContext.Users.Add(new AppUser
        {
            Id = ValidatorId,
            UserName = "validator@test.com",
            Email = "validator@test.com",
            FirstName = "Marie",
            LastName = "Curie",
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

        _dbContext.SamplingLocations.Add(new SamplingLocation
        {
            Id = SamplingLocationId,
            Name = "Source A",
            LocationCode = "SRC-A",
            DistributorId = DistributorId,
            IsActive = true,
        });

        await _dbContext.SaveChangesAsync();
    }

    private async Task<Guid> CreateOrder(OrderStatus status, string? preleveurId)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"ORD-{Guid.NewGuid().ToString()[..4]}",
            Status = status,
            PreleveurId = preleveurId,
            DistributorId = DistributorId,
            SamplingLocationId = SamplingLocationId,
            CreatedById = PreleveurId,
            TenantId = TenantId,
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        return order.Id;
    }

    private async Task<Guid> CreateOrderWithRound(OrderStatus status, string roundPreleveurId)
    {
        var round = new SamplingRound
        {
            Id = Guid.NewGuid(),
            Name = "Test Round",
            DistributorId = DistributorId,
            Status = SamplingRoundStatus.InProgress,
            TenantId = TenantId,
            PreleveurId = roundPreleveurId,
            CreatedById = PreleveurId,
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.SamplingRounds.Add(round);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"ORD-{Guid.NewGuid().ToString()[..4]}",
            Status = status,
            PreleveurId = null, // préleveur assigned via round, not directly
            DistributorId = DistributorId,
            SamplingRoundId = round.Id,
            SamplingLocationId = SamplingLocationId,
            CreatedById = PreleveurId,
            TenantId = TenantId,
            CreatedAt = DateTime.UtcNow,
        };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        return order.Id;
    }

    private async Task<Guid> CreateOrderWithSampling(OrderStatus status)
    {
        var orderId = await CreateOrder(status, PreleveurId);

        var sampling = new Sampling
        {
            Id = Guid.NewGuid(),
            OrderId = orderId,
            PreleveurId = PreleveurId,
            SamplingDateTime = DateTime.UtcNow,
            Temperature = 15.5,
            Weather = "Sunny",
            Notes = "Test notes",
            CreatedAt = DateTime.UtcNow,
        };

        _dbContext.Samplings.Add(sampling);
        await _dbContext.SaveChangesAsync();
        return orderId;
    }

    private static SamplingCreateDto CreateSamplingDto(Guid orderId)
    {
        return new SamplingCreateDto(
            OrderId: orderId,
            SamplingDateTime: new DateTime(2026, 4, 10, 8, 30, 0, DateTimeKind.Utc),
            Temperature: 15.5,
            Weather: "Sunny",
            LocationLat: 46.8,
            LocationLng: 7.15,
            Notes: "Test notes",
            HasWaterSoftener: null,
            IsChlorinated: false);
    }
}
