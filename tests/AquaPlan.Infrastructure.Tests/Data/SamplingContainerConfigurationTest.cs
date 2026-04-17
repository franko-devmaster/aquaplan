using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AquaPlan.Infrastructure.Tests.Data;

public class SamplingContainerConfigurationTest : IDisposable
{
    private readonly AquaPlanDbContext _dbContext;

    public SamplingContainerConfigurationTest()
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
    public void DbContext_ShouldExposeSamplingContainersDbSet()
    {
        _dbContext.SamplingContainers.Should().NotBeNull();
    }

    [Fact]
    public void Model_ShouldConfigureSamplingContainerEntity()
    {
        var entity = _dbContext.Model.FindEntityType(typeof(SamplingContainer));

        entity.Should().NotBeNull();
        entity!.FindPrimaryKey()!.Properties.Should().ContainSingle()
            .Which.Name.Should().Be(nameof(SamplingContainer.Id));
    }

    [Fact]
    public void Model_ShouldConfigureUniqueIndex_OnSamplingAndContainer()
    {
        var entity = _dbContext.Model.FindEntityType(typeof(SamplingContainer))!;

        var index = entity.GetIndexes().FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(SamplingContainer.SamplingId),
                nameof(SamplingContainer.ContainerId)
            }));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public void Model_ShouldConfigureFilteredUniqueIndex_OnTenantAndBarcode()
    {
        var entity = _dbContext.Model.FindEntityType(typeof(SamplingContainer))!;

        var index = entity.GetIndexes().FirstOrDefault(i =>
            i.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(SamplingContainer.TenantId),
                nameof(SamplingContainer.Barcode)
            }));

        index.Should().NotBeNull();
        index!.IsUnique.Should().BeTrue();
    }

    [Fact]
    public async Task DbContext_ShouldPersistSamplingContainer()
    {
        var tenantId = Guid.NewGuid();
        var tenant = new Tenant { Id = tenantId, Name = "T", Code = "T" };
        _dbContext.Tenants.Add(tenant);

        var container = new Container
        {
            Id = Guid.NewGuid(),
            Code = "BACT-V250",
            Name = "Bouteille microbio",
            Material = "Verre",
            VolumeMl = 250,
            Color = "Transparent",
            IsActive = true,
            TenantId = tenantId,
        };
        _dbContext.Containers.Add(container);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = "ORD-TEST-1",
            CreatedById = "u1",
            DistributorId = Guid.NewGuid(),
            TenantId = tenantId,
        };
        _dbContext.Orders.Add(order);

        var sampling = new Sampling
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            PreleveurId = "u1",
            SamplingDateTime = DateTime.UtcNow,
        };
        _dbContext.Samplings.Add(sampling);

        var sc = new SamplingContainer
        {
            Id = Guid.NewGuid(),
            SamplingId = sampling.Id,
            ContainerId = container.Id,
            Barcode = "LAB-001",
            TenantId = tenantId,
        };
        _dbContext.SamplingContainers.Add(sc);

        await _dbContext.SaveChangesAsync();

        var reloaded = await _dbContext.SamplingContainers.FindAsync(sc.Id);
        reloaded.Should().NotBeNull();
        reloaded!.Barcode.Should().Be("LAB-001");
    }
}
