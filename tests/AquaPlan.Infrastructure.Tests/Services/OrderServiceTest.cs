using AquaPlan.Application.DTOs.MockLims;
using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.Exceptions;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AquaPlan.Infrastructure.Tests.Services;

public class OrderServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<IOrderAuditService> _auditServiceMock = new();
    private readonly Mock<IDelegationService> _delegationServiceMock = new();
    private readonly Mock<IMockLimsService> _mockLimsServiceMock = new();
    private readonly Mock<INotificationService> _notificationServiceMock = new();
    private readonly Mock<ILogger<OrderService>> _loggerMock = new();
    private readonly Mock<ILogger<SamplingRoundService>> _roundLoggerMock = new();
    private readonly SamplingRoundService _roundService;
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
        _roundService = new SamplingRoundService(_dbContext, _delegationServiceMock.Object, _notificationServiceMock.Object, _roundLoggerMock.Object);
        var mockLimsOptions = Options.Create(new MockLimsOptions { Enabled = false });
        _sut = new OrderService(_dbContext, _auditServiceMock.Object, _roundService, _mockLimsServiceMock.Object, _notificationServiceMock.Object, mockLimsOptions, _loggerMock.Object);

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

    // --- GetRequiredContainersAsync ---

    [Fact]
    public async Task GetRequiredContainersAsync_WithNoOrder_ShouldReturnNull()
    {
        var result = await _sut.GetRequiredContainersAsync(Guid.NewGuid(), TenantId);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRequiredContainersAsync_WithCrossTenant_ShouldReturnNull()
    {
        var otherTenant = Guid.Parse("00000000-0000-0000-0000-000000000999");
        var order = await CreateOrderWithPrograms();

        var result = await _sut.GetRequiredContainersAsync(order.Id, otherTenant);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetRequiredContainersAsync_With2ProfilesSharingContainer_ShouldDeduplicate()
    {
        var (order, containerA, _) = await CreateOrderSharedContainerScenario();

        var result = await _sut.GetRequiredContainersAsync(order.Id, TenantId);

        result.Should().NotBeNull();
        result!.Should().HaveCount(1);
        result[0].ContainerId.Should().Be(containerA.Id);
        result[0].ExistingBarcode.Should().BeNull();
    }

    [Fact]
    public async Task GetRequiredContainersAsync_With2ProfilesDifferentContainers_ShouldReturnBoth()
    {
        var (order, containerA, containerB) = await CreateOrderTwoContainerScenario();

        var result = await _sut.GetRequiredContainersAsync(order.Id, TenantId);

        result.Should().NotBeNull();
        result!.Should().HaveCount(2);
        result.Select(r => r.ContainerId).Should().BeEquivalentTo(new[] { containerA.Id, containerB.Id });
        result.Should().BeInAscendingOrder(r => r.Code);
    }

    [Fact]
    public async Task GetRequiredContainersAsync_WithoutSampling_ShouldReturnEmptyExistingBarcodes()
    {
        var (order, _, _) = await CreateOrderTwoContainerScenario();

        var result = await _sut.GetRequiredContainersAsync(order.Id, TenantId);

        result.Should().NotBeNull();
        result!.Should().AllSatisfy(r => r.ExistingBarcode.Should().BeNull());
    }

    [Fact]
    public async Task GetRequiredContainersAsync_WithExistingSampling_ShouldReturnCanonicalBarcodeForAllContainers()
    {
        var (order, containerA, containerB) = await CreateOrderTwoContainerScenario();

        // AQ-363 — a mandate has a SINGLE canonical barcode shared by every container.
        var sampling = new Sampling
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            PreleveurId = UserId,
            SamplingDateTime = DateTime.UtcNow,
            SampleBarcode = "BC-CANONICAL",
            TenantId = TenantId,
        };
        _dbContext.Samplings.Add(sampling);
        _dbContext.SamplingContainers.Add(new SamplingContainer
        {
            Id = Guid.NewGuid(),
            SamplingId = sampling.Id,
            ContainerId = containerA.Id,
        });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetRequiredContainersAsync(order.Id, TenantId);

        result.Should().NotBeNull();
        result!.Single(r => r.ContainerId == containerA.Id).ExistingBarcode.Should().Be("BC-CANONICAL");
        result.Single(r => r.ContainerId == containerB.Id).ExistingBarcode.Should().Be("BC-CANONICAL");
    }

    // ─── Bulk transitions ──────────────────────────────────────
    [Fact]
    public async Task BulkValidateAsync_WithMultipleInProgress_ShouldTransitionAll()
    {
        await CreateSeedOrder(OrderStatus.InProgress);
        await CreateSeedOrder(OrderStatus.InProgress);
        await CreateSeedOrder(OrderStatus.InProgress);
        await CreateSeedOrder(OrderStatus.New);
        await CreateSeedOrder(OrderStatus.Completed);

        var result = await _sut.BulkValidateAsync(UserId, TenantId);

        result.Affected.Should().Be(3);
        var completedCount = await _dbContext.Orders
            .CountAsync(o => o.TenantId == TenantId && o.Status == OrderStatus.Completed);
        completedCount.Should().Be(4); // 3 transitioned + 1 pre-existing
        var inProgressCount = await _dbContext.Orders
            .CountAsync(o => o.TenantId == TenantId && o.Status == OrderStatus.InProgress);
        inProgressCount.Should().Be(0);
        _auditServiceMock.Verify(a => a.LogAsync(
            It.IsAny<Guid>(), "StatusTransitioned", It.IsAny<string>(),
            "InProgress", "Completed", UserId, TenantId, It.IsAny<CancellationToken>()),
            Times.Exactly(3));
    }

    [Fact]
    public async Task BulkValidateAsync_WithNoInProgress_ShouldReturnZeroAffected()
    {
        await CreateSeedOrder(OrderStatus.New);
        await CreateSeedOrder(OrderStatus.Completed);

        var result = await _sut.BulkValidateAsync(UserId, TenantId);

        result.Affected.Should().Be(0);
        _auditServiceMock.Verify(a => a.LogAsync(
            It.IsAny<Guid>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task BulkValidateAsync_ShouldRespectTenantIsolation()
    {
        var otherTenant = Guid.Parse("00000000-0000-0000-0000-000000000099");
        await CreateSeedOrder(OrderStatus.InProgress);
        await CreateSeedOrder(OrderStatus.InProgress);
        // Seed a different-tenant InProgress order
        _dbContext.Distributors.Add(new Distributor
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000999"),
            Name = "Other Tenant Distributor",
            TenantId = otherTenant,
            IsActive = true,
        });
        _dbContext.Orders.Add(new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-OTHER-01",
            Status = OrderStatus.InProgress,
            CreatedById = UserId,
            DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000999"),
            TenantId = otherTenant,
        });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.BulkValidateAsync(UserId, TenantId);

        result.Affected.Should().Be(2);
        var otherOrder = await _dbContext.Orders
            .FirstAsync(o => o.TenantId == otherTenant);
        otherOrder.Status.Should().Be(OrderStatus.InProgress);
    }

    [Fact]
    public async Task BulkTransmitAsync_WithMultipleCompleted_ShouldTransitionAll()
    {
        await CreateSeedOrder(OrderStatus.Completed);
        await CreateSeedOrder(OrderStatus.Completed);
        await CreateSeedOrder(OrderStatus.InProgress);

        var result = await _sut.BulkTransmitAsync(UserId, TenantId);

        result.Affected.Should().Be(2);
        var transmittedCount = await _dbContext.Orders
            .CountAsync(o => o.TenantId == TenantId && o.Status == OrderStatus.Transmitted);
        transmittedCount.Should().Be(2);
        _auditServiceMock.Verify(a => a.LogAsync(
            It.IsAny<Guid>(), "StatusTransitioned", It.IsAny<string>(),
            "Completed", "Transmitted", UserId, TenantId, It.IsAny<CancellationToken>()),
            Times.Exactly(2));
    }

    [Fact]
    public async Task BulkTransmitAsync_WithNoCompleted_ShouldReturnZeroAffected()
    {
        await CreateSeedOrder(OrderStatus.InProgress);
        await CreateSeedOrder(OrderStatus.New);

        var result = await _sut.BulkTransmitAsync(UserId, TenantId);

        result.Affected.Should().Be(0);
    }

    // ─── BulkFinalize (AQ-406) ─────────────────────────────────
    [Fact]
    public async Task BulkFinalizeAsync_WithMixedStatuses_ShouldValidateThenTransmit()
    {
        await CreateSeedOrder(OrderStatus.InProgress);
        await CreateSeedOrder(OrderStatus.InProgress);
        await CreateSeedOrder(OrderStatus.Completed);
        await CreateSeedOrder(OrderStatus.New); // must stay untouched

        var result = await _sut.BulkFinalizeAsync(UserId, TenantId);

        // 2 InProgress → Completed, then all 3 Completed (2 newly + 1 original) → Transmitted
        result.Validated.Should().Be(2);
        result.Transmitted.Should().Be(3);
        var transmittedCount = await _dbContext.Orders
            .CountAsync(o => o.TenantId == TenantId && o.Status == OrderStatus.Transmitted);
        transmittedCount.Should().Be(3);
        var untouched = await _dbContext.Orders
            .CountAsync(o => o.TenantId == TenantId && o.Status == OrderStatus.New);
        untouched.Should().Be(1);
    }

    [Fact]
    public async Task BulkFinalizeAsync_WithNoEligibleOrders_ShouldReturnZeros()
    {
        await CreateSeedOrder(OrderStatus.New);
        await CreateSeedOrder(OrderStatus.Transmitted);

        var result = await _sut.BulkFinalizeAsync(UserId, TenantId);

        result.Validated.Should().Be(0);
        result.Transmitted.Should().Be(0);
    }

    [Fact]
    public async Task BulkFinalizeAsync_WithOnlyInProgress_ShouldValidateAndTransmitAll()
    {
        await CreateSeedOrder(OrderStatus.InProgress);
        await CreateSeedOrder(OrderStatus.InProgress);

        var result = await _sut.BulkFinalizeAsync(UserId, TenantId);

        result.Validated.Should().Be(2);
        result.Transmitted.Should().Be(2);
        var transmittedCount = await _dbContext.Orders
            .CountAsync(o => o.TenantId == TenantId && o.Status == OrderStatus.Transmitted);
        transmittedCount.Should().Be(2);
    }

    [Fact]
    public async Task BulkFinalizeAsync_ShouldRespectTenantIsolation()
    {
        var otherTenant = Guid.Parse("00000000-0000-0000-0000-000000000097");
        await CreateSeedOrder(OrderStatus.InProgress);
        _dbContext.Distributors.Add(new Distributor
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000997"),
            Name = "Other Tenant Distributor 3",
            TenantId = otherTenant,
            IsActive = true,
        });
        _dbContext.Orders.Add(new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-OTHER-03",
            Status = OrderStatus.InProgress,
            CreatedById = UserId,
            DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000997"),
            TenantId = otherTenant,
        });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.BulkFinalizeAsync(UserId, TenantId);

        result.Validated.Should().Be(1);
        result.Transmitted.Should().Be(1);
        var otherOrder = await _dbContext.Orders.FirstAsync(o => o.TenantId == otherTenant);
        otherOrder.Status.Should().Be(OrderStatus.InProgress);
    }

    [Fact]
    public async Task BulkTransmitAsync_ShouldRespectTenantIsolation()
    {
        var otherTenant = Guid.Parse("00000000-0000-0000-0000-000000000099");
        await CreateSeedOrder(OrderStatus.Completed);
        _dbContext.Distributors.Add(new Distributor
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000998"),
            Name = "Other Tenant Distributor 2",
            TenantId = otherTenant,
            IsActive = true,
        });
        _dbContext.Orders.Add(new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-OTHER-02",
            Status = OrderStatus.Completed,
            CreatedById = UserId,
            DistributorId = Guid.Parse("00000000-0000-0000-0000-000000000998"),
            TenantId = otherTenant,
        });
        await _dbContext.SaveChangesAsync();

        var result = await _sut.BulkTransmitAsync(UserId, TenantId);

        result.Affected.Should().Be(1);
        var otherOrder = await _dbContext.Orders
            .FirstAsync(o => o.TenantId == otherTenant);
        otherOrder.Status.Should().Be(OrderStatus.Completed);
    }

    // ─── AQ-33 — Mock LIMS outbound integration ─────────────────────────────────

    [Fact]
    public async Task BulkTransmitAsync_WhenMockLimsEnabled_ShouldForwardOrdersAndStoreLimsOrderId()
    {
        var sut = CreateSutWithMockLimsEnabled();
        await CreateSeedOrder(OrderStatus.Completed);

        var expectedLimsId = Guid.Parse("00000000-0000-0000-0000-000000000ABC".Replace("A", "0").Replace("B", "1").Replace("C", "2"));
        _mockLimsServiceMock
            .Setup(s => s.ReceiveOrderAsync(It.IsAny<MockLimsOrderCreateDto>(), TenantId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MockLimsOrderCreatedDto(expectedLimsId, DateTime.UtcNow));

        var result = await sut.BulkTransmitAsync(UserId, TenantId);

        result.Affected.Should().Be(1);
        _mockLimsServiceMock.Verify(s => s.ReceiveOrderAsync(
            It.IsAny<MockLimsOrderCreateDto>(), TenantId, It.IsAny<CancellationToken>()),
            Times.Once);

        var order = await _dbContext.Orders.SingleAsync(o => o.TenantId == TenantId);
        order.Status.Should().Be(OrderStatus.Transmitted);
        order.LimsOrderId.Should().Be(expectedLimsId);
        order.TransmittedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task BulkTransmitAsync_WhenMockLimsDisabled_ShouldNotCallMockLims()
    {
        await CreateSeedOrder(OrderStatus.Completed);

        var result = await _sut.BulkTransmitAsync(UserId, TenantId);

        result.Affected.Should().Be(1);
        _mockLimsServiceMock.Verify(s => s.ReceiveOrderAsync(
            It.IsAny<MockLimsOrderCreateDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var order = await _dbContext.Orders.SingleAsync(o => o.TenantId == TenantId);
        order.Status.Should().Be(OrderStatus.Transmitted);
        order.LimsOrderId.Should().BeNull();
        order.TransmittedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task BulkTransmitAsync_WhenMockLimsThrows_ShouldStillTransitionOrder()
    {
        var sut = CreateSutWithMockLimsEnabled();
        await CreateSeedOrder(OrderStatus.Completed);

        _mockLimsServiceMock
            .Setup(s => s.ReceiveOrderAsync(It.IsAny<MockLimsOrderCreateDto>(), TenantId, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("Mock LIMS down"));

        var result = await sut.BulkTransmitAsync(UserId, TenantId);

        result.Affected.Should().Be(1);
        var order = await _dbContext.Orders.SingleAsync(o => o.TenantId == TenantId);
        order.Status.Should().Be(OrderStatus.Transmitted);
        order.LimsOrderId.Should().BeNull();
    }

    [Fact]
    public async Task BulkTransmitAsync_WhenAlreadyTransmittedToLims_ShouldNotDuplicate()
    {
        var sut = CreateSutWithMockLimsEnabled();
        var existingLimsId = Guid.NewGuid();

        // Simulate an order that previously got a LimsOrderId but was reverted to Completed.
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-REPEAT",
            Status = OrderStatus.Completed,
            CreatedById = UserId,
            DistributorId = DistributorId,
            TenantId = TenantId,
            LimsOrderId = existingLimsId,
        };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await sut.BulkTransmitAsync(UserId, TenantId);

        result.Affected.Should().Be(1);
        _mockLimsServiceMock.Verify(s => s.ReceiveOrderAsync(
            It.IsAny<MockLimsOrderCreateDto>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()),
            Times.Never);

        var reloaded = await _dbContext.Orders.SingleAsync(o => o.Id == order.Id);
        reloaded.LimsOrderId.Should().Be(existingLimsId);
    }

    private OrderService CreateSutWithMockLimsEnabled()
    {
        var options = Options.Create(new MockLimsOptions { Enabled = true });
        return new OrderService(
            _dbContext,
            _auditServiceMock.Object,
            _roundService,
            _mockLimsServiceMock.Object,
            _notificationServiceMock.Object,
            options,
            _loggerMock.Object);
    }

    private async Task<Order> CreateOrderWithPrograms()
    {
        var (order, _, _) = await CreateOrderTwoContainerScenario();
        return order;
    }

    private async Task<(Order Order, Container ContainerA, Container ContainerB)> CreateOrderTwoContainerScenario()
    {
        var containerA = new Container
        {
            Id = Guid.NewGuid(),
            Code = "AAA-CONT",
            Name = "Flacon A",
            Material = "Verre",
            VolumeMl = 250,
            Color = "Transparent",
            IsActive = true,
            TenantId = TenantId,
        };
        var containerB = new Container
        {
            Id = Guid.NewGuid(),
            Code = "BBB-CONT",
            Name = "Flacon B",
            Material = "PET",
            VolumeMl = 500,
            Color = "Transparent",
            IsActive = true,
            TenantId = TenantId,
        };
        _dbContext.Containers.Add(containerA);
        _dbContext.Containers.Add(containerB);

        var profileA = new AnalysisProfile
        {
            Id = Guid.NewGuid(), Code = "PA", Name = "Profil A",
            TenantId = TenantId, ContainerId = containerA.Id,
        };
        var profileB = new AnalysisProfile
        {
            Id = Guid.NewGuid(), Code = "PB", Name = "Profil B",
            TenantId = TenantId, ContainerId = containerB.Id,
        };
        _dbContext.AnalysisProfiles.Add(profileA);
        _dbContext.AnalysisProfiles.Add(profileB);

        var program = new AnalysisProgram
        {
            Id = Guid.NewGuid(), Code = "PRG", Name = "Programme", TenantId = TenantId,
        };
        _dbContext.AnalysisPrograms.Add(program);
        _dbContext.AnalysisProgramProfiles.Add(new AnalysisProgramProfile
        {
            AnalysisProgramId = program.Id, AnalysisProfileId = profileA.Id,
        });
        _dbContext.AnalysisProgramProfiles.Add(new AnalysisProgramProfile
        {
            AnalysisProgramId = program.Id, AnalysisProfileId = profileB.Id,
        });

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-REQ",
            CreatedById = UserId,
            DistributorId = DistributorId,
            TenantId = TenantId,
            Status = OrderStatus.New,
        };
        _dbContext.Orders.Add(order);
        _dbContext.OrderAnalysisPrograms.Add(new OrderAnalysisProgram
        {
            OrderId = order.Id, AnalysisProgramId = program.Id,
        });
        await _dbContext.SaveChangesAsync();
        return (order, containerA, containerB);
    }

    private async Task<(Order Order, Container ContainerA, Container ContainerB)> CreateOrderSharedContainerScenario()
    {
        var container = new Container
        {
            Id = Guid.NewGuid(),
            Code = "AAA-CONT",
            Name = "Flacon partagé",
            Material = "Verre",
            VolumeMl = 250,
            Color = "Transparent",
            IsActive = true,
            TenantId = TenantId,
        };
        _dbContext.Containers.Add(container);

        var profileA = new AnalysisProfile
        {
            Id = Guid.NewGuid(), Code = "PA", Name = "Profil A",
            TenantId = TenantId, ContainerId = container.Id,
        };
        var profileB = new AnalysisProfile
        {
            Id = Guid.NewGuid(), Code = "PB", Name = "Profil B",
            TenantId = TenantId, ContainerId = container.Id,
        };
        _dbContext.AnalysisProfiles.Add(profileA);
        _dbContext.AnalysisProfiles.Add(profileB);

        var program = new AnalysisProgram
        {
            Id = Guid.NewGuid(), Code = "PRG", Name = "Programme", TenantId = TenantId,
        };
        _dbContext.AnalysisPrograms.Add(program);
        _dbContext.AnalysisProgramProfiles.Add(new AnalysisProgramProfile
        {
            AnalysisProgramId = program.Id, AnalysisProfileId = profileA.Id,
        });
        _dbContext.AnalysisProgramProfiles.Add(new AnalysisProgramProfile
        {
            AnalysisProgramId = program.Id, AnalysisProfileId = profileB.Id,
        });

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-SHARED",
            CreatedById = UserId,
            DistributorId = DistributorId,
            TenantId = TenantId,
            Status = OrderStatus.New,
        };
        _dbContext.Orders.Add(order);
        _dbContext.OrderAnalysisPrograms.Add(new OrderAnalysisProgram
        {
            OrderId = order.Id, AnalysisProgramId = program.Id,
        });
        await _dbContext.SaveChangesAsync();
        return (order, container, container);
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

    // AQ-43 — notification trigger on single-order préleveur assignment
    [Fact]
    public async Task AssignPreleveurAsync_WhenPreleveurChanges_ShouldCreateOrderAssignedNotification()
    {
        var order = await CreateSeedOrder(OrderStatus.New);
        var dto = new OrderAssignDto(PreleveurId: "preleveur-X");

        await _sut.AssignPreleveurAsync(order.Id, dto, UserId, TenantId);

        _notificationServiceMock.Verify(n => n.CreateAsync(
            "preleveur-X",
            NotificationType.OrderAssigned,
            It.IsAny<string>(),
            It.IsAny<string>(),
            TenantId,
            "Order",
            order.Id,
            false,
            It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task AssignPreleveurAsync_WhenPreleveurUnchanged_ShouldNotCreateNotification()
    {
        var order = await CreateSeedOrder(OrderStatus.New);
        order.PreleveurId = "preleveur-X";
        await _dbContext.SaveChangesAsync();
        var dto = new OrderAssignDto(PreleveurId: "preleveur-X");

        await _sut.AssignPreleveurAsync(order.Id, dto, UserId, TenantId);

        _notificationServiceMock.Verify(n => n.CreateAsync(
            It.IsAny<string>(), It.IsAny<NotificationType>(), It.IsAny<string>(), It.IsAny<string>(),
            It.IsAny<Guid>(), It.IsAny<string?>(), It.IsAny<Guid?>(), It.IsAny<bool>(), It.IsAny<CancellationToken>()),
            Times.Never);
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

        _dbContext.Users.Add(new AppUser
        {
            Id = "preleveur-lock",
            UserName = "preleveur-lock@test.com",
            Email = "preleveur-lock@test.com",
            FirstName = "Lock",
            LastName = "Holder",
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

    // --- AQ-371 lock guard integration ---

    [Fact]
    public async Task UpdateOrderAsync_WhenRoundLockedByOther_ShouldThrowRoundLockedException()
    {
        var order = await CreateOrderInLockedRound();
        var dto = new OrderUpdateDto(null, null, null, null, "Attempt");

        await _sut.Awaiting(s => s.UpdateOrderAsync(order.Id, dto, UserId, TenantId, isAdmin: false))
            .Should().ThrowAsync<RoundLockedException>();
    }

    [Fact]
    public async Task UpdateOrderAsync_WhenRoundLockedBySameUser_ShouldSucceed()
    {
        var order = await CreateOrderInLockedRound(lockedById: UserId);
        var dto = new OrderUpdateDto(null, null, null, null, "By lock holder");

        // Still will fail because Status is already In Progress for a locked round.
        // So use a Draft-status order with user=UserId as lock holder then set status=New manually.
        var orderEntity = await _dbContext.Orders.FindAsync(order.Id);
        orderEntity!.Status = OrderStatus.New;
        await _dbContext.SaveChangesAsync();

        var result = await _sut.UpdateOrderAsync(order.Id, dto, UserId, TenantId, isAdmin: false);

        result.Should().NotBeNull();
    }

    [Fact]
    public async Task DeleteOrderAsync_WhenRoundLockedByOther_ShouldThrowRoundLockedException()
    {
        var order = await CreateOrderInLockedRound();

        await _sut.Awaiting(s => s.DeleteOrderAsync(order.Id, UserId, TenantId, isAdmin: false))
            .Should().ThrowAsync<RoundLockedException>();
    }

    private async Task<Order> CreateOrderInLockedRound(string? lockedById = null)
    {
        var round = new SamplingRound
        {
            Id = Guid.NewGuid(),
            Name = "Locked round",
            DistributorId = DistributorId,
            Status = SamplingRoundStatus.InProgress,
            TenantId = TenantId,
            CreatedById = UserId,
            CreatedAt = DateTime.UtcNow,
            IsLocked = true,
            LockedById = lockedById ?? "preleveur-lock",
            LockedAt = DateTime.UtcNow,
        };
        _dbContext.SamplingRounds.Add(round);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"ORD-LCK-{Guid.NewGuid():N}".Substring(0, 20),
            Status = OrderStatus.New,
            IsUnplanned = false,
            CreatedById = UserId,
            DistributorId = DistributorId,
            TenantId = TenantId,
            SamplingRoundId = round.Id,
        };
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();
        return order;
    }

    // ─── AQ-31 — Dashboard summary + ResultsStatus ────────────

    private async Task<Order> SeedDoneOrderAsync(
        string createdById = UserId,
        bool conform = true,
        bool withResults = true)
    {
        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = $"ORD-DONE-{Guid.NewGuid():N}".Substring(0, 20),
            Status = OrderStatus.Done,
            CreatedById = createdById,
            DistributorId = DistributorId,
            TenantId = TenantId,
        };
        _dbContext.Orders.Add(order);
        if (withResults)
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
                IsConform = conform,
                TenantId = TenantId,
                ReceivedAt = DateTime.UtcNow,
            });
        }
        await _dbContext.SaveChangesAsync();
        return order;
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_AsAdmin_ShouldAggregateAllOrders()
    {
        await SeedDoneOrderAsync(conform: true);
        await SeedDoneOrderAsync(conform: true);
        await SeedDoneOrderAsync(conform: false);
        await CreateSeedOrder(OrderStatus.Transmitted);
        await CreateSeedOrder(OrderStatus.New);

        var summary = await _sut.GetDashboardSummaryAsync(UserId, TenantId, isAdmin: true);

        summary.TotalCount.Should().Be(5);
        summary.ConformCount.Should().Be(2);
        summary.NonConformCount.Should().Be(1);
        summary.PendingCount.Should().Be(2);
    }

    [Fact]
    public async Task GetDashboardSummaryAsync_AsNonAdmin_ShouldRespectVisibilityScope()
    {
        await SeedDoneOrderAsync(createdById: UserId, conform: true);
        await SeedDoneOrderAsync(createdById: "other-user", conform: false);

        var summary = await _sut.GetDashboardSummaryAsync(UserId, TenantId, isAdmin: false);

        summary.TotalCount.Should().Be(1);
        summary.ConformCount.Should().Be(1);
        summary.NonConformCount.Should().Be(0);
    }

    [Fact]
    public async Task GetOrdersFilteredAsync_ShouldDeriveResultsStatus()
    {
        await SeedDoneOrderAsync(conform: true);
        await SeedDoneOrderAsync(conform: false);
        await CreateSeedOrder(OrderStatus.Transmitted);

        var result = await _sut.GetOrdersFilteredAsync(
            UserId, TenantId,
            new OrderFilterDto(null, null, null, null),
            isAdmin: true);

        result.Items.Should().HaveCount(3);
        result.Items.Should().Contain(i => i.ResultsStatus == ResultsStatus.Conform);
        result.Items.Should().Contain(i => i.ResultsStatus == ResultsStatus.NonConform);
        result.Items.Should().Contain(i => i.ResultsStatus == ResultsStatus.NotReceived);
    }

    [Fact]
    public async Task GetOrdersFilteredAsync_WithResultsStatusFilter_ShouldRestrictResults()
    {
        await SeedDoneOrderAsync(conform: true);
        await SeedDoneOrderAsync(conform: false);
        await CreateSeedOrder(OrderStatus.Transmitted);

        var filter = new OrderFilterDto(null, null, null, null, ResultsStatus: ResultsStatus.NonConform);
        var result = await _sut.GetOrdersFilteredAsync(UserId, TenantId, filter, isAdmin: true);

        result.Items.Should().HaveCount(1);
        result.Items.Single().ResultsStatus.Should().Be(ResultsStatus.NonConform);
    }

    [Fact]
    public async Task GetOrderByIdAsync_ShouldPopulateResultsStatus()
    {
        var order = await SeedDoneOrderAsync(conform: false);

        var dto = await _sut.GetOrderByIdAsync(order.Id, TenantId);

        dto!.ResultsStatus.Should().Be(ResultsStatus.NonConform);
    }
}
