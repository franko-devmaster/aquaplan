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
            AnalysisProgramIds: null,
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
            AnalysisProgramIds: null,
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
            AnalysisProgramIds: null,
            Notes: null,
            IsUnplanned: false,
            UnplannedReason: UnplannedReason.Urgency,
            UnplannedReasonDetails: "Should be ignored");

        var result = await _sut.CreateOrderAsync(dto, UserId, TenantId);

        result.IsUnplanned.Should().BeFalse();
        result.UnplannedReason.Should().BeNull();
        result.UnplannedReasonDetails.Should().BeNull();
        result.Status.Should().Be(OrderStatus.New);
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
            AnalysisProgramIds: null,
            Notes: null,
            IsUnplanned: true,
            UnplannedReason: reason,
            UnplannedReasonDetails: null);

        var result = await _sut.CreateOrderAsync(dto, UserId, TenantId);

        result.UnplannedReason.Should().Be(reason);
    }

    // --- Admin order management ---

    [Fact]
    public async Task UpdateOrderAsync_ShouldAllowAdmin_WhenStatusInProgress()
    {
        var order = await CreateSeedOrder(OrderStatus.InProgress);
        var dto = new OrderUpdateDto(null, null, DateTime.UtcNow.AddDays(5), null, "Admin update");

        var result = await _sut.UpdateOrderAsync(order.Id, dto, UserId, TenantId, isAdmin: true);

        result.Should().NotBeNull();
        result!.Notes.Should().Be("Admin update");
    }

    [Fact]
    public async Task UpdateOrderAsync_ShouldThrowForAdmin_WhenStatusDone()
    {
        var order = await CreateSeedOrder(OrderStatus.Done);
        var dto = new OrderUpdateDto(null, null, null, null, "Should fail");

        await _sut.Awaiting(s => s.UpdateOrderAsync(order.Id, dto, UserId, TenantId, isAdmin: true))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*terminal*");
    }

    [Fact]
    public async Task UpdateOrderAsync_ShouldThrowForNonAdmin_WhenStatusInProgress()
    {
        var order = await CreateSeedOrder(OrderStatus.InProgress);
        var dto = new OrderUpdateDto(null, null, null, null, "Should fail");

        await _sut.Awaiting(s => s.UpdateOrderAsync(order.Id, dto, UserId, TenantId, isAdmin: false))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*New*");
    }

    [Fact]
    public async Task DeleteOrderAsync_ShouldAllowAdmin_WhenStatusNew()
    {
        var order = await CreateSeedOrder(OrderStatus.New);

        var deleted = await _sut.DeleteOrderAsync(order.Id, UserId, TenantId, isAdmin: true);

        deleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteOrderAsync_ShouldAllowAdmin_WhenStatusInProgress()
    {
        var order = await CreateSeedOrder(OrderStatus.InProgress);

        var deleted = await _sut.DeleteOrderAsync(order.Id, UserId, TenantId, isAdmin: true);

        deleted.Should().BeTrue();
    }

    [Fact]
    public async Task DeleteOrderAsync_ShouldThrowForAdmin_WhenStatusCompleted()
    {
        var order = await CreateSeedOrder(OrderStatus.Completed);

        await _sut.Awaiting(s => s.DeleteOrderAsync(order.Id, UserId, TenantId, isAdmin: true))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*before sampling*");
    }

    [Fact]
    public async Task DeleteOrderAsync_ShouldThrowForNonAdmin_WhenStatusInProgress()
    {
        var order = await CreateSeedOrder(OrderStatus.InProgress);

        await _sut.Awaiting(s => s.DeleteOrderAsync(order.Id, UserId, TenantId, isAdmin: false))
            .Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*New*");
    }

    // --- Advanced filters + CSV export ---

    [Fact]
    public async Task GetOrdersFilteredAsync_ShouldFilterByDistributor()
    {
        var otherDistId = Guid.NewGuid();
        _dbContext.Distributors.Add(new Distributor { Id = otherDistId, Name = "Other Dist", TenantId = TenantId, IsActive = true });
        _dbContext.Orders.Add(new Order { Id = Guid.NewGuid(), OrderNumber = "ORD-DIST-01", Status = OrderStatus.New, IsUnplanned = false, CreatedById = UserId, DistributorId = DistributorId, TenantId = TenantId });
        _dbContext.Orders.Add(new Order { Id = Guid.NewGuid(), OrderNumber = "ORD-DIST-02", Status = OrderStatus.New, IsUnplanned = false, CreatedById = UserId, DistributorId = otherDistId, TenantId = TenantId });
        _dbContext.UserDistributors.Add(new UserDistributor { UserId = UserId, DistributorId = otherDistId });
        await _dbContext.SaveChangesAsync();

        var filter = new OrderFilterDto(null, null, null, null, DistributorId: DistributorId);
        var result = await _sut.GetOrdersFilteredAsync(UserId, TenantId, filter, isAdmin: true);

        result.Items.Should().AllSatisfy(o => o.DistributorId.Should().Be(DistributorId));
    }

    [Fact]
    public async Task GetOrdersFilteredAsync_ShouldFilterByDateRange()
    {
        _dbContext.Orders.Add(new Order { Id = Guid.NewGuid(), OrderNumber = "ORD-DATE-01", Status = OrderStatus.New, IsUnplanned = false, CreatedById = UserId, DistributorId = DistributorId, TenantId = TenantId, PlannedDate = new DateTime(2026, 3, 15) });
        _dbContext.Orders.Add(new Order { Id = Guid.NewGuid(), OrderNumber = "ORD-DATE-02", Status = OrderStatus.New, IsUnplanned = false, CreatedById = UserId, DistributorId = DistributorId, TenantId = TenantId, PlannedDate = new DateTime(2026, 5, 20) });
        await _dbContext.SaveChangesAsync();

        var filter = new OrderFilterDto(null, null, null, null, DateFrom: new DateTime(2026, 4, 1), DateTo: new DateTime(2026, 6, 1));
        var result = await _sut.GetOrdersFilteredAsync(UserId, TenantId, filter, isAdmin: true);

        result.Items.Should().HaveCount(1);
        result.Items[0].OrderNumber.Should().Be("ORD-DATE-02");
    }

    [Fact]
    public async Task ExportOrdersCsvAsync_ShouldReturnCsvBytes()
    {
        _dbContext.Orders.Add(new Order { Id = Guid.NewGuid(), OrderNumber = "ORD-CSV-01", Status = OrderStatus.New, IsUnplanned = false, CreatedById = UserId, DistributorId = DistributorId, TenantId = TenantId });
        await _dbContext.SaveChangesAsync();

        var filter = new OrderFilterDto(null, null, null, null);
        var csv = await _sut.ExportOrdersCsvAsync(TenantId, filter);

        csv.Should().NotBeEmpty();
        var content = System.Text.Encoding.UTF8.GetString(csv);
        content.Should().Contain("OrderNumber;Status");
        content.Should().Contain("ORD-CSV-01");
    }

    // --- Delegation ---

    [Fact]
    public async Task GetOrdersFilteredAsync_ShouldIncludeDelegatedDistributors()
    {
        var delegatedDistId = Guid.NewGuid();
        _dbContext.Distributors.Add(new Distributor { Id = delegatedDistId, Name = "Delegated Dist", TenantId = TenantId, IsActive = true });
        _dbContext.DistributorDelegations.Add(new DistributorDelegation
        {
            Id = Guid.NewGuid(),
            DelegatingDistributorId = delegatedDistId,
            DelegatedToDistributorId = DistributorId,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            IsActive = true,
            TenantId = TenantId,
        });
        _dbContext.Orders.Add(new Order { Id = Guid.NewGuid(), OrderNumber = "ORD-DEL-01", Status = OrderStatus.New, IsUnplanned = false, CreatedById = UserId, DistributorId = delegatedDistId, TenantId = TenantId });
        await _dbContext.SaveChangesAsync();

        var filter = new OrderFilterDto(null, null, null, null);
        var result = await _sut.GetOrdersFilteredAsync(UserId, TenantId, filter, isAdmin: false);

        result.Items.Should().Contain(o => o.OrderNumber == "ORD-DEL-01");
    }

    // --- Analysis programs attachment (AQ-307) ---

    [Fact]
    public async Task CreateOrderAsync_WithAnalysisProgramIds_ShouldAttachPrograms()
    {
        var programA = new AnalysisProgram { Id = Guid.NewGuid(), Code = "EP-01", Name = "Eau potable", TenantId = TenantId };
        var programB = new AnalysisProgram { Id = Guid.NewGuid(), Code = "BAIG-01", Name = "Baignade", TenantId = TenantId };
        _dbContext.AnalysisPrograms.AddRange(programA, programB);
        await _dbContext.SaveChangesAsync();

        var dto = new OrderCreateDto(
            DistributorId: DistributorId,
            SamplingLocationId: null,
            PreleveurId: null,
            PlannedDate: DateTime.UtcNow.AddDays(5),
            AnalysisProgramIds: [programA.Id, programB.Id],
            Notes: null,
            IsUnplanned: false);

        var result = await _sut.CreateOrderAsync(dto, UserId, TenantId);

        result.AnalysisPrograms.Should().HaveCount(2);
        result.AnalysisPrograms.Select(p => p.Code).Should().BeEquivalentTo(new[] { "EP-01", "BAIG-01" });
        result.AnalysisPrograms.Select(p => p.Name).Should().BeEquivalentTo(new[] { "Eau potable", "Baignade" });
    }

    [Fact]
    public async Task UpdateOrderAsync_WithNewProgramIds_ShouldReplaceExistingPrograms()
    {
        var oldProgram = new AnalysisProgram { Id = Guid.NewGuid(), Code = "OLD", Name = "Old program", TenantId = TenantId };
        var newProgram = new AnalysisProgram { Id = Guid.NewGuid(), Code = "NEW", Name = "New program", TenantId = TenantId };
        _dbContext.AnalysisPrograms.AddRange(oldProgram, newProgram);
        await _dbContext.SaveChangesAsync();

        var order = await CreateSeedOrder(OrderStatus.New);
        _dbContext.OrderAnalysisPrograms.Add(new OrderAnalysisProgram { OrderId = order.Id, AnalysisProgramId = oldProgram.Id });
        await _dbContext.SaveChangesAsync();

        var dto = new OrderUpdateDto(null, null, null, [newProgram.Id], null);

        var result = await _sut.UpdateOrderAsync(order.Id, dto, UserId, TenantId, isAdmin: false);

        result.Should().NotBeNull();
        result!.AnalysisPrograms.Should().HaveCount(1);
        result.AnalysisPrograms[0].Code.Should().Be("NEW");
    }

    [Fact]
    public async Task GetOrderByIdAsync_ShouldIncludeAnalysisPrograms()
    {
        var program = new AnalysisProgram { Id = Guid.NewGuid(), Code = "EP-42", Name = "Full programme", TenantId = TenantId };
        _dbContext.AnalysisPrograms.Add(program);
        var order = await CreateSeedOrder(OrderStatus.New);
        _dbContext.OrderAnalysisPrograms.Add(new OrderAnalysisProgram { OrderId = order.Id, AnalysisProgramId = program.Id });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetOrderByIdAsync(order.Id, TenantId);

        result.Should().NotBeNull();
        result!.AnalysisPrograms.Should().HaveCount(1);
        result.AnalysisPrograms[0].AnalysisProgramId.Should().Be(program.Id);
        result.AnalysisPrograms[0].Code.Should().Be("EP-42");
        result.AnalysisPrograms[0].Name.Should().Be("Full programme");
    }

    [Fact]
    public async Task CreateOrderAsync_ShouldSetIsDelegated_WhenDistributorNotOwn()
    {
        var delegatedDistId = Guid.NewGuid();
        _dbContext.Distributors.Add(new Distributor { Id = delegatedDistId, Name = "Delegated Dist", TenantId = TenantId, IsActive = true });
        _dbContext.DistributorDelegations.Add(new DistributorDelegation
        {
            Id = Guid.NewGuid(),
            DelegatingDistributorId = delegatedDistId,
            DelegatedToDistributorId = DistributorId,
            ValidFrom = DateTime.UtcNow.AddDays(-1),
            IsActive = true,
            TenantId = TenantId,
        });
        await _dbContext.SaveChangesAsync();

        var dto = new OrderCreateDto(
            DistributorId: delegatedDistId,
            SamplingLocationId: null,
            PreleveurId: null,
            PlannedDate: DateTime.UtcNow.AddDays(7),
            AnalysisProgramIds: null,
            Notes: null,
            IsUnplanned: false);

        var result = await _sut.CreateOrderAsync(dto, UserId, TenantId);

        result.IsDelegated.Should().BeTrue();
    }

    private async Task<Order> CreateSeedOrder(OrderStatus status)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"ORD-TEST-{Guid.NewGuid():N}".Substring(0, 20),
            Status = status,
            IsUnplanned = false,
            CreatedById = UserId,
            DistributorId = DistributorId,
            TenantId = TenantId,
        };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        return order;
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
