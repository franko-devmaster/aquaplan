using AquaPlan.Application.DTOs.Results;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Services;

public class ResultsServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly Mock<IDelegationService> _delegationServiceMock = new();
    private readonly Mock<ILogger<ResultsService>> _loggerMock = new();
    private readonly IConfiguration _configuration;
    private readonly ResultsService _sut;

    private static readonly Guid TenantId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherTenantId = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid DistributorAId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid DistributorBId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SectorAId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SectorBId = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid LocationAId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid LocationBId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private const string UserId = "user-1";

    public ResultsServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AquaPlanDbContext(options);

        _configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>())
            .Build();

        _sut = new ResultsService(_dbContext, _delegationServiceMock.Object, _configuration, _loggerMock.Object);

        SeedCommon();

        // By default the user is authorized on distributor A only.
        _delegationServiceMock
            .Setup(s => s.GetAuthorizedDistributorIdsForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([DistributorAId]);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    // --- GetRecentAsync ---

    [Fact]
    public async Task GetRecentAsync_ShouldReturnOrdersWithResultsInWindow_DescendingByDate()
    {
        var recent = CreateOrder("O-RECENT", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-1));
        var older = CreateOrder("O-OLD", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-10));
        _dbContext.Orders.AddRange(recent, older);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetRecentAsync(UserId, TenantId, isAdmin: false);

        result.Should().HaveCount(1);
        result[0].OrderNumber.Should().Be("O-RECENT");
    }

    [Fact]
    public async Task GetRecentAsync_ShouldReturnEmpty_WhenNoOrdersInTenant()
    {
        var result = await _sut.GetRecentAsync(UserId, TenantId, isAdmin: false);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecentAsync_ShouldRespectTenantIsolation()
    {
        var foreign = CreateOrder("FOREIGN", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-1));
        foreign.TenantId = OtherTenantId;
        _dbContext.Orders.Add(foreign);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetRecentAsync(UserId, TenantId, isAdmin: false);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task GetRecentAsync_ShouldFilterByAuthorizedDistributors_WhenNotAdmin()
    {
        var okOrder = CreateOrder("OK", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-1));
        var forbiddenOrder = CreateOrder("KO", LocationBId, DistributorBId, DateTime.UtcNow.AddDays(-1));
        _dbContext.Orders.AddRange(okOrder, forbiddenOrder);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetRecentAsync(UserId, TenantId, isAdmin: false);

        result.Should().ContainSingle().Which.OrderNumber.Should().Be("OK");
    }

    [Fact]
    public async Task GetRecentAsync_ShouldReturnAllTenantOrders_WhenAdmin()
    {
        var a = CreateOrder("A", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-1));
        var b = CreateOrder("B", LocationBId, DistributorBId, DateTime.UtcNow.AddDays(-1));
        _dbContext.Orders.AddRange(a, b);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetRecentAsync(UserId, TenantId, isAdmin: true);

        result.Should().HaveCount(2);
    }

    [Fact]
    public async Task GetRecentAsync_ShouldMarkPending_WhenNoResults()
    {
        var order = CreateOrder("O-PENDING", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-1));
        order.SamplingResults.Clear();
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetRecentAsync(UserId, TenantId, isAdmin: false);

        result.Should().ContainSingle().Which.Conformity.Should().Be(ResultConformity.Pending);
    }

    [Fact]
    public async Task GetRecentAsync_ShouldMarkRed_WhenCriticalParamIsNonConform()
    {
        var order = CreateOrder("O-RED", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-1));
        order.SamplingResults = [NonConform("E_COLI"), Conform("PH")];
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetRecentAsync(UserId, TenantId, isAdmin: false);

        result.Should().ContainSingle().Which.Conformity.Should().Be(ResultConformity.Red);
    }

    [Fact]
    public async Task GetRecentAsync_ShouldMarkYellow_WhenOnlyNonCriticalNonConform()
    {
        var order = CreateOrder("O-YELLOW", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-1));
        order.SamplingResults = [NonConform("PH"), Conform("E_COLI")];
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetRecentAsync(UserId, TenantId, isAdmin: false);

        result.Should().ContainSingle().Which.Conformity.Should().Be(ResultConformity.Yellow);
    }

    [Fact]
    public async Task GetRecentAsync_ShouldMarkGreen_WhenAllConform()
    {
        var order = CreateOrder("O-GREEN", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-1));
        order.SamplingResults = [Conform("E_COLI"), Conform("PH")];
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetRecentAsync(UserId, TenantId, isAdmin: false);

        result.Should().ContainSingle().Which.Conformity.Should().Be(ResultConformity.Green);
    }

    [Fact]
    public async Task GetRecentAsync_ShouldHonorConfiguredCriticalParameters()
    {
        // Build a SUT with a custom critical parameter list (only "PH" is critical).
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ResultsConfig:CriticalParameters:0"] = "PH",
            })
            .Build();
        var sut = new ResultsService(_dbContext, _delegationServiceMock.Object, config, _loggerMock.Object);

        var order = CreateOrder("O-CUSTOM", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-1));
        order.SamplingResults = [NonConform("PH"), Conform("E_COLI")];
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await sut.GetRecentAsync(UserId, TenantId, isAdmin: false);

        result.Should().ContainSingle().Which.Conformity.Should().Be(ResultConformity.Red);
    }

    // --- GetMatrixAsync ---

    [Fact]
    public async Task GetMatrixAsync_ShouldReturnEmpty_WhenNoOrders()
    {
        var result = await _sut.GetMatrixAsync(UserId, TenantId, isAdmin: false, null, null, false, null, null);

        result.Locations.Should().BeEmpty();
        result.Dates.Should().BeEmpty();
        result.Cells.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMatrixAsync_ShouldProjectOrdersToCells()
    {
        var order = CreateOrder("O-1", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-1));
        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetMatrixAsync(UserId, TenantId, isAdmin: false, null, null, false, null, null);

        result.Cells.Should().ContainSingle();
        result.Cells[0].LocationId.Should().Be(LocationAId);
        result.Cells[0].OrderId.Should().Be(order.Id);
        result.Locations.Should().ContainSingle().Which.Id.Should().Be(LocationAId);
        result.Dates.Should().ContainSingle();
    }

    [Fact]
    public async Task GetMatrixAsync_ShouldFilterByDistributor()
    {
        _delegationServiceMock
            .Setup(s => s.GetAuthorizedDistributorIdsForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([DistributorAId, DistributorBId]);

        var a = CreateOrder("A", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-1));
        var b = CreateOrder("B", LocationBId, DistributorBId, DateTime.UtcNow.AddDays(-2));
        _dbContext.Orders.AddRange(a, b);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetMatrixAsync(UserId, TenantId, isAdmin: false, DistributorBId, null, false, null, null);

        result.Cells.Should().ContainSingle().Which.OrderId.Should().Be(b.Id);
    }

    [Fact]
    public async Task GetMatrixAsync_ShouldFilterBySector()
    {
        _delegationServiceMock
            .Setup(s => s.GetAuthorizedDistributorIdsForUserAsync(UserId, It.IsAny<CancellationToken>()))
            .ReturnsAsync([DistributorAId, DistributorBId]);

        var a = CreateOrder("A", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-1)); // sector A
        var b = CreateOrder("B", LocationBId, DistributorBId, DateTime.UtcNow.AddDays(-2)); // sector B
        _dbContext.Orders.AddRange(a, b);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetMatrixAsync(UserId, TenantId, isAdmin: false, null, SectorAId, false, null, null);

        result.Cells.Should().ContainSingle().Which.OrderId.Should().Be(a.Id);
    }

    [Fact]
    public async Task GetMatrixAsync_WithAnomaliesOnly_ShouldExcludeGreenCells()
    {
        var greenOrder = CreateOrder("GREEN", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-1));
        greenOrder.SamplingResults = [Conform("PH")];
        var redOrder = CreateOrder("RED", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-2));
        redOrder.SamplingResults = [NonConform("E_COLI")];
        _dbContext.Orders.AddRange(greenOrder, redOrder);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetMatrixAsync(UserId, TenantId, isAdmin: false, null, null, anomaliesOnly: true, null, null);

        result.Cells.Should().ContainSingle().Which.Conformity.Should().Be(ResultConformity.Red);
    }

    [Fact]
    public async Task GetMatrixAsync_ShouldFilterByDateRange()
    {
        var inRange = CreateOrder("IN", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-2));
        var outRange = CreateOrder("OUT", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-40));
        _dbContext.Orders.AddRange(inRange, outRange);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetMatrixAsync(
            UserId, TenantId, isAdmin: false, null, null, false,
            DateTime.UtcNow.AddDays(-7), DateTime.UtcNow);

        result.Cells.Should().ContainSingle().Which.OrderId.Should().Be(inRange.Id);
    }

    [Fact]
    public async Task GetMatrixAsync_ShouldRespectTenantIsolation()
    {
        var foreign = CreateOrder("FOREIGN", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-1));
        foreign.TenantId = OtherTenantId;
        _dbContext.Orders.Add(foreign);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetMatrixAsync(UserId, TenantId, isAdmin: false, null, null, false, null, null);

        result.Cells.Should().BeEmpty();
    }

    [Fact]
    public async Task GetMatrixAsync_ShouldIgnoreOrdersWithoutLocation()
    {
        var noLoc = CreateOrder("NO-LOC", LocationAId, DistributorAId, DateTime.UtcNow.AddDays(-1));
        noLoc.SamplingLocationId = null;
        _dbContext.Orders.Add(noLoc);
        await _dbContext.SaveChangesAsync();

        var result = await _sut.GetMatrixAsync(UserId, TenantId, isAdmin: false, null, null, false, null, null);

        result.Cells.Should().BeEmpty();
    }

    // --- ComputeConformity pure logic ---

    [Fact]
    public void ComputeConformity_ShouldBePending_WhenNull()
    {
        ResultsService.ComputeConformity(null, []).Should().Be(ResultConformity.Pending);
    }

    [Fact]
    public void ComputeConformity_ShouldBeGreen_WhenEveryoneConform()
    {
        var r = new List<SamplingResult> { Conform("E_COLI"), Conform("PH") };
        ResultsService.ComputeConformity(r, new HashSet<string>(["E_COLI"], StringComparer.OrdinalIgnoreCase))
            .Should().Be(ResultConformity.Green);
    }

    [Fact]
    public void ComputeConformity_ShouldBeRed_WhenCriticalNonConform()
    {
        var r = new List<SamplingResult> { NonConform("E_COLI") };
        ResultsService.ComputeConformity(r, new HashSet<string>(["E_COLI"], StringComparer.OrdinalIgnoreCase))
            .Should().Be(ResultConformity.Red);
    }

    [Fact]
    public void ComputeConformity_ShouldBeYellow_WhenOnlyNonCriticalNonConform()
    {
        var r = new List<SamplingResult> { NonConform("PH") };
        ResultsService.ComputeConformity(r, new HashSet<string>(["E_COLI"], StringComparer.OrdinalIgnoreCase))
            .Should().Be(ResultConformity.Yellow);
    }

    // --- Seed helpers ---

    private void SeedCommon()
    {
        _dbContext.Sectors.AddRange(
            new Sector { Id = SectorAId, Name = "Secteur A", Code = "SA", TenantId = TenantId },
            new Sector { Id = SectorBId, Name = "Secteur B", Code = "SB", TenantId = TenantId });

        _dbContext.Distributors.AddRange(
            new Distributor { Id = DistributorAId, Name = "Distributor A", TenantId = TenantId },
            new Distributor { Id = DistributorBId, Name = "Distributor B", TenantId = TenantId });

        _dbContext.SamplingLocations.AddRange(
            new SamplingLocation
            {
                Id = LocationAId,
                Name = "Loc A",
                LocationCode = "LA",
                DistributorId = DistributorAId,
                SectorId = SectorAId,
            },
            new SamplingLocation
            {
                Id = LocationBId,
                Name = "Loc B",
                LocationCode = "LB",
                DistributorId = DistributorBId,
                SectorId = SectorBId,
            });

        _dbContext.SaveChanges();
    }

    private Order CreateOrder(string number, Guid locationId, Guid distributorId, DateTime receivedAt)
    {
        return new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = number,
            DistributorId = distributorId,
            SamplingLocationId = locationId,
            TenantId = TenantId,
            Status = OrderStatus.Done,
            ResultsReceivedAt = receivedAt,
            CreatedById = UserId,
            SamplingResults = [Conform("E_COLI")],
        };
    }

    private static SamplingResult Conform(string parameterCode)
    {
        return new SamplingResult
        {
            Id = Guid.NewGuid(),
            ParameterCode = parameterCode,
            Value = 1m,
            Unit = "u",
            IsConform = true,
            ReceivedAt = DateTime.UtcNow,
            TenantId = TenantId,
        };
    }

    private static SamplingResult NonConform(string parameterCode)
    {
        return new SamplingResult
        {
            Id = Guid.NewGuid(),
            ParameterCode = parameterCode,
            Value = 999m,
            Unit = "u",
            IsConform = false,
            ReceivedAt = DateTime.UtcNow,
            TenantId = TenantId,
        };
    }
}
