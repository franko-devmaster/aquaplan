using AquaPlan.Application.DTOs.Sectors;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

internal class SectorService(
    AquaPlanDbContext dbContext,
    ILogger<SectorService> logger) : ISectorService
{
    public async Task<IList<SectorListDto>> GetAllAsync(SectorFilteringInputDto filter, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Sectors
            .Where(s => s.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(filter.Name))
        {
            query = query.Where(s => s.Name.Contains(filter.Name));
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(s => s.IsActive == filter.IsActive.Value);
        }

        return await query
            .OrderBy(s => s.Name)
            .Select(s => new SectorListDto(
                s.Id, s.Name, s.Code, s.Description,
                s.IsActive, s.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<SectorDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Sectors
            .Where(s => s.Id == id && s.TenantId == tenantId)
            .Select(s => new SectorDto(
                s.Id, s.Name, s.Code, s.Description,
                s.IsActive, s.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SectorDto> CreateAsync(SectorAddDto dto, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var nameExists = await dbContext.Sectors
            .AnyAsync(s => s.TenantId == tenantId && s.Name == dto.Name, cancellationToken);

        if (nameExists)
        {
            throw new InvalidOperationException($"A sector with the name '{dto.Name}' already exists for this tenant.");
        }

        var sector = new Sector
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Code = dto.Code,
            Description = dto.Description,
            TenantId = tenantId,
        };

        dbContext.Sectors.Add(sector);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Sector {Name} created with ID {Id}", dto.Name, sector.Id);

        return (await GetByIdAsync(sector.Id, tenantId, cancellationToken))!;
    }

    public async Task<SectorDto?> UpdateAsync(Guid id, SectorUpdateDto dto, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var sector = await dbContext.Sectors
            .Where(s => s.Id == id && s.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (sector is null)
        {
            return null;
        }

        var nameExists = await dbContext.Sectors
            .AnyAsync(s => s.TenantId == tenantId && s.Name == dto.Name && s.Id != id, cancellationToken);

        if (nameExists)
        {
            throw new InvalidOperationException($"A sector with the name '{dto.Name}' already exists for this tenant.");
        }

        sector.Name = dto.Name;
        sector.Code = dto.Code;
        sector.Description = dto.Description;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Sector {Id} updated", id);

        return await GetByIdAsync(id, tenantId, cancellationToken);
    }

    public async Task<SectorDto?> ToggleStatusAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var sector = await dbContext.Sectors
            .Where(s => s.Id == id && s.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (sector is null)
        {
            return null;
        }

        sector.IsActive = !sector.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Sector {Id} status toggled to {IsActive}", id, sector.IsActive);

        return await GetByIdAsync(id, tenantId, cancellationToken);
    }
}
