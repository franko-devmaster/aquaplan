using AquaPlan.Application.DTOs.Distributors;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

internal class DistributorService(
    AquaPlanDbContext dbContext,
    ILogger<DistributorService> logger) : IDistributorService
{
    public async Task<IList<DistributorListDto>> GetAllAsync(DistributorFilteringInputDto filter, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Distributors
            .Where(d => d.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            query = query.Where(d => d.Name.Contains(filter.Name));
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(d => d.IsActive == filter.IsActive.Value);
        }

        return await query
            .OrderBy(d => d.Name)
            .Select(d => new DistributorListDto(
                d.Id, d.Name, d.ShortName, d.CantonRegion, d.DistributionNetwork,
                d.IsActive, d.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<DistributorDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Distributors
            .Where(d => d.Id == id && d.TenantId == tenantId)
            .Select(d => new DistributorDto(
                d.Id, d.Name, d.ShortName, d.CantonRegion, d.DistributionNetwork,
                d.IsActive, d.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<DistributorDto> CreateAsync(DistributorAddDto dto, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var nameExists = await dbContext.Distributors
            .AnyAsync(d => d.TenantId == tenantId && d.Name == dto.Name, cancellationToken);

        if (nameExists)
        {
            throw new InvalidOperationException($"A distributor with the name '{dto.Name}' already exists for this tenant.");
        }

        var distributor = new Distributor
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            ShortName = dto.ShortName,
            CantonRegion = dto.CantonRegion,
            DistributionNetwork = dto.DistributionNetwork,
            TenantId = tenantId,
        };

        dbContext.Distributors.Add(distributor);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Distributor {Name} created with ID {Id}", dto.Name, distributor.Id);

        return (await GetByIdAsync(distributor.Id, tenantId, cancellationToken))!;
    }

    public async Task<DistributorDto?> UpdateAsync(Guid id, DistributorUpdateDto dto, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var distributor = await dbContext.Distributors
            .Where(d => d.Id == id && d.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (distributor is null)
        {
            return null;
        }

        var nameExists = await dbContext.Distributors
            .AnyAsync(d => d.TenantId == tenantId && d.Name == dto.Name && d.Id != id, cancellationToken);

        if (nameExists)
        {
            throw new InvalidOperationException($"A distributor with the name '{dto.Name}' already exists for this tenant.");
        }

        distributor.Name = dto.Name;
        distributor.ShortName = dto.ShortName;
        distributor.CantonRegion = dto.CantonRegion;
        distributor.DistributionNetwork = dto.DistributionNetwork;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Distributor {Id} updated", id);

        return await GetByIdAsync(id, tenantId, cancellationToken);
    }

    public async Task<DistributorDto?> ToggleStatusAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var distributor = await dbContext.Distributors
            .Where(d => d.Id == id && d.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (distributor is null)
        {
            return null;
        }

        distributor.IsActive = !distributor.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Distributor {Id} status toggled to {IsActive}", id, distributor.IsActive);

        return await GetByIdAsync(id, tenantId, cancellationToken);
    }
}
