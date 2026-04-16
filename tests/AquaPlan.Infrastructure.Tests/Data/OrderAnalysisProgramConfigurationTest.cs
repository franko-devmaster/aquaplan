using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AquaPlan.Infrastructure.Tests.Data;

public class OrderAnalysisProgramConfigurationTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;

    public OrderAnalysisProgramConfigurationTest()
    {
        var options = new DbContextOptionsBuilder<AquaPlanDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _dbContext = new AquaPlanDbContext(options);
    }

    public void Dispose()
    {
        _dbContext.Dispose();
    }

    [Fact]
    public void OrderAnalysisProgram_ShouldHaveCompositeKey_OnOrderIdAndAnalysisProgramId()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(OrderAnalysisProgram));

        entityType.Should().NotBeNull();
        var primaryKey = entityType!.FindPrimaryKey();
        primaryKey.Should().NotBeNull();
        primaryKey!.Properties.Select(p => p.Name).Should().BeEquivalentTo(
            new[] { nameof(OrderAnalysisProgram.OrderId), nameof(OrderAnalysisProgram.AnalysisProgramId) });
    }

    [Fact]
    public void OrderAnalysisProgram_ShouldHaveOrderForeignKey_WithCascadeDelete()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(OrderAnalysisProgram));

        entityType.Should().NotBeNull();
        var orderFk = entityType!.GetForeignKeys().FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(Order));
        orderFk.Should().NotBeNull();
        orderFk!.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public void OrderAnalysisProgram_ShouldHaveAnalysisProgramForeignKey_WithCascadeDelete()
    {
        var entityType = _dbContext.Model.FindEntityType(typeof(OrderAnalysisProgram));

        entityType.Should().NotBeNull();
        var programFk = entityType!.GetForeignKeys().FirstOrDefault(fk => fk.PrincipalEntityType.ClrType == typeof(AnalysisProgram));
        programFk.Should().NotBeNull();
        programFk!.DeleteBehavior.Should().Be(DeleteBehavior.Cascade);
    }

    [Fact]
    public async Task OrderAnalysisProgram_ShouldPreventDuplicateEntriesForSameKey()
    {
        var orderId = Guid.NewGuid();
        var programId = Guid.NewGuid();

        _dbContext.OrderAnalysisPrograms.Add(new OrderAnalysisProgram
        {
            OrderId = orderId,
            AnalysisProgramId = programId,
        });
        await _dbContext.SaveChangesAsync();

        var act = async () =>
        {
            _dbContext.OrderAnalysisPrograms.Add(new OrderAnalysisProgram
            {
                OrderId = orderId,
                AnalysisProgramId = programId,
            });
            await _dbContext.SaveChangesAsync();
        };

        await act.Should().ThrowAsync<Exception>();
    }
}
