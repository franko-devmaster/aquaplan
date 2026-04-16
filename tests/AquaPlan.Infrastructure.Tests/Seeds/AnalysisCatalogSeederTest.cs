using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using AquaPlan.Infrastructure.Data.Seeds;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Tests.Seeds;

public class AnalysisCatalogSeederTest
{
    [Fact]
    public async Task SeedAsync_ShouldCreateSixContainersPerTenant()
    {
        using var serviceProvider = BuildServiceProvider(Guid.NewGuid().ToString());
        await SeedTwoTenantsAsync(serviceProvider);

        await AnalysisCatalogSeeder.SeedAsync(serviceProvider);

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AquaPlanDbContext>();
        var tenantIds = await db.Tenants.Select(t => t.Id).ToListAsync();

        foreach (var tenantId in tenantIds)
        {
            var containers = await db.Containers
                .Where(c => c.TenantId == tenantId)
                .ToListAsync();
            containers.Should().HaveCount(6);
            containers.Select(c => c.Code).Should().BeEquivalentTo(new[]
            {
                "BACT-V250", "CHEM-PET500", "CHEM-PEHD250", "PHY-V100", "PEST-V1000", "ISOT-V60",
            });
        }
    }

    [Fact]
    public async Task SeedAsync_WhenContainersAlreadyExist_ShouldBeIdempotent()
    {
        using var serviceProvider = BuildServiceProvider(Guid.NewGuid().ToString());
        await SeedTwoTenantsAsync(serviceProvider);

        await AnalysisCatalogSeeder.SeedAsync(serviceProvider);
        await AnalysisCatalogSeeder.SeedAsync(serviceProvider);

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AquaPlanDbContext>();
        var total = await db.Containers.CountAsync();

        total.Should().Be(12);
    }

    [Fact]
    public async Task SeedAsync_ShouldIsolateContainersPerTenant()
    {
        using var serviceProvider = BuildServiceProvider(Guid.NewGuid().ToString());
        var (tenantAId, tenantBId) = await SeedTwoTenantsAsync(serviceProvider);

        await AnalysisCatalogSeeder.SeedAsync(serviceProvider);

        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AquaPlanDbContext>();
        var tenantAContainers = await db.Containers.Where(c => c.TenantId == tenantAId).ToListAsync();
        var tenantBContainers = await db.Containers.Where(c => c.TenantId == tenantBId).ToListAsync();

        tenantAContainers.Should().HaveCount(6);
        tenantBContainers.Should().HaveCount(6);
        tenantAContainers.Select(c => c.Id).Should().NotIntersectWith(tenantBContainers.Select(c => c.Id));
    }

    private static ServiceProvider BuildServiceProvider(string dbName)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<AquaPlanDbContext>(options => options.UseInMemoryDatabase(dbName));
        return services.BuildServiceProvider();
    }

    private static async Task<(Guid tenantA, Guid tenantB)> SeedTwoTenantsAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AquaPlanDbContext>();

        var tenantA = new Tenant { Id = Guid.NewGuid(), Name = "Tenant A", Code = "A" };
        var tenantB = new Tenant { Id = Guid.NewGuid(), Name = "Tenant B", Code = "B" };
        db.Tenants.AddRange(tenantA, tenantB);
        await db.SaveChangesAsync();

        return (tenantA.Id, tenantB.Id);
    }
}
