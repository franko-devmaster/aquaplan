using AquaPlan.Application.DTOs.MockLims;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Services.MockLims;
using Microsoft.EntityFrameworkCore;

namespace AquaPlan.Infrastructure.Tests.Services.MockLims;

public class MockLimsServiceTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;
    private readonly IMockLimsResultGenerator _generator = new MockLimsResultGenerator();
    private readonly MockLimsService _sut;

    private static readonly Guid TenantId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    public MockLimsServiceTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        _dbContext = new AquaPlanDbContext(options);
        _sut = new MockLimsService(_dbContext, _generator);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public async Task ReceiveOrderAsync_WithValidPayload_ShouldPersistAndReturnId()
    {
        var dto = new MockLimsOrderCreateDto("ORD-001", DateTime.UtcNow, new[] { "PH", "NITRATES" });

        var result = await _sut.ReceiveOrderAsync(dto, TenantId);

        result.LimsOrderId.Should().NotBeEmpty();
        var persisted = await _dbContext.MockLimsOrders.SingleAsync();
        persisted.OrderReference.Should().Be("ORD-001");
        persisted.TenantId.Should().Be(TenantId);
        persisted.Status.Should().Be(MockLimsOrderStatus.Received);
    }

    [Fact]
    public async Task ReceiveOrderAsync_WithEmptyParameters_ShouldThrow()
    {
        var dto = new MockLimsOrderCreateDto("ORD-001", DateTime.UtcNow, Array.Empty<string>());

        await _sut.Awaiting(s => s.ReceiveOrderAsync(dto, TenantId))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ReceiveOrderAsync_WithBlankReference_ShouldThrow()
    {
        var dto = new MockLimsOrderCreateDto(" ", DateTime.UtcNow, new[] { "PH" });

        await _sut.Awaiting(s => s.ReceiveOrderAsync(dto, TenantId))
            .Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task ReceiveOrderAsync_WhenReferenceAlreadyExists_ShouldBeIdempotent()
    {
        var dto = new MockLimsOrderCreateDto("ORD-001", DateTime.UtcNow, new[] { "PH" });

        var first = await _sut.ReceiveOrderAsync(dto, TenantId);
        var second = await _sut.ReceiveOrderAsync(dto, TenantId);

        second.LimsOrderId.Should().Be(first.LimsOrderId);
        (await _dbContext.MockLimsOrders.CountAsync()).Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_WhenExists_ShouldReturnDto()
    {
        var dto = new MockLimsOrderCreateDto("ORD-010", DateTime.UtcNow, new[] { "PH", "TURBIDITY" });
        var created = await _sut.ReceiveOrderAsync(dto, TenantId);

        var fetched = await _sut.GetAsync(created.LimsOrderId, TenantId);

        fetched.Should().NotBeNull();
        fetched!.OrderReference.Should().Be("ORD-010");
        fetched.Parameters.Should().BeEquivalentTo(new[] { "PH", "TURBIDITY" });
        fetched.Status.Should().Be(MockLimsOrderStatus.Received);
    }

    [Fact]
    public async Task GetAsync_CrossTenant_ShouldReturnNull()
    {
        var dto = new MockLimsOrderCreateDto("ORD-XT", DateTime.UtcNow, new[] { "PH" });
        var created = await _sut.ReceiveOrderAsync(dto, TenantId);

        var otherTenant = Guid.NewGuid();
        var fetched = await _sut.GetAsync(created.LimsOrderId, otherTenant);

        fetched.Should().BeNull();
    }

    [Fact]
    public async Task GetResultsAsync_WhenFirstCall_ShouldGenerateAndPersistResults()
    {
        var dto = new MockLimsOrderCreateDto("ORD-R1", DateTime.UtcNow, new[] { "PH", "NITRATES", "FREE_CHLORINE" });
        var created = await _sut.ReceiveOrderAsync(dto, TenantId);

        var results = await _sut.GetResultsAsync(created.LimsOrderId, TenantId);

        results.Should().NotBeNull();
        results!.Results.Should().HaveCount(3);

        var persisted = await _dbContext.MockLimsOrders.SingleAsync();
        persisted.Status.Should().Be(MockLimsOrderStatus.ResultsReady);
        persisted.ResultsJson.Should().NotBeNullOrEmpty();
        persisted.ResultsReadyAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetResultsAsync_WhenCalledTwice_ShouldReturnSameResults()
    {
        var dto = new MockLimsOrderCreateDto("ORD-R2", DateTime.UtcNow, new[] { "PH", "NITRATES" });
        var created = await _sut.ReceiveOrderAsync(dto, TenantId);

        var first = await _sut.GetResultsAsync(created.LimsOrderId, TenantId);
        var second = await _sut.GetResultsAsync(created.LimsOrderId, TenantId);

        second.Should().BeEquivalentTo(first);
    }

    [Fact]
    public async Task GetResultsAsync_WhenUnknownId_ShouldReturnNull()
    {
        var results = await _sut.GetResultsAsync(Guid.NewGuid(), TenantId);

        results.Should().BeNull();
    }
}
