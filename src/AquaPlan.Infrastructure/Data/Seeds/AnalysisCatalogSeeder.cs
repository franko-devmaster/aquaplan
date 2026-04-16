using AquaPlan.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Data.Seeds;

public static class AnalysisCatalogSeeder
{
    public record ContainerSeed(string Code, string Name, string Material, int VolumeMl, string Color);

    public static readonly IReadOnlyList<ContainerSeed> DefaultContainers = new List<ContainerSeed>
    {
        new("BACT-V250", "Bouteille verre stérile microbiologie", "Verre borosilicaté", 250, "Transparent"),
        new("CHEM-PET500", "Bouteille PET chimie", "PET", 500, "Transparent"),
        new("CHEM-PEHD250", "Flacon PEHD traces organiques", "PEHD", 250, "Blanc opaque"),
        new("PHY-V100", "Flacon verre paramètres physiques", "Verre", 100, "Ambré"),
        new("PEST-V1000", "Bouteille verre ambré pesticides/micropolluants", "Verre ambré", 1000, "Ambré"),
        new("ISOT-V60", "Flacon verre isotopes/COV (septum)", "Verre", 60, "Transparent"),
    };

    public static async Task SeedAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AquaPlanDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AquaPlanDbContext>>();

        var tenants = await dbContext.Tenants.ToListAsync();

        foreach (var tenant in tenants)
        {
            await SeedContainersForTenantAsync(dbContext, tenant.Id, logger);
        }
    }

    private static async Task SeedContainersForTenantAsync(AquaPlanDbContext dbContext, Guid tenantId, ILogger logger)
    {
        var existingCodes = await dbContext.Containers
            .Where(c => c.TenantId == tenantId)
            .Select(c => c.Code)
            .ToListAsync();

        var missing = DefaultContainers.Where(c => !existingCodes.Contains(c.Code)).ToList();
        if (missing.Count == 0)
        {
            return;
        }

        foreach (var seed in missing)
        {
            dbContext.Containers.Add(new Container
            {
                Id = Guid.NewGuid(),
                Code = seed.Code,
                Name = seed.Name,
                Material = seed.Material,
                VolumeMl = seed.VolumeMl,
                Color = seed.Color,
                IsActive = true,
                TenantId = tenantId,
            });
        }

        await dbContext.SaveChangesAsync();
        logger.LogInformation("Seeded {Count} containers for tenant {TenantId}", missing.Count, tenantId);
    }
}
