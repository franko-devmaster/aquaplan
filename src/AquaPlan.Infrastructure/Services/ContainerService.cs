using AquaPlan.Application.DTOs.Containers;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

internal class ContainerService(
    AquaPlanDbContext dbContext,
    ILogger<ContainerService> logger) : IContainerService
{
    public async Task<IList<ContainerListDto>> GetAllAsync(Guid tenantId, ContainerFilteringInputDto? filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Containers
            .Where(c => c.TenantId == tenantId);

        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.ToLower();
                query = query.Where(c => c.Name.ToLower().Contains(search) || c.Code.ToLower().Contains(search));
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(c => c.IsActive == filter.IsActive.Value);
            }
        }

        return await query
            .OrderBy(c => c.Code)
            .Select(c => new ContainerListDto(
                c.Id, c.Code, c.Name, c.Material, c.VolumeMl, c.Color, c.IsActive))
            .ToListAsync(cancellationToken);
    }

    public async Task<ContainerDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Containers
            .Where(c => c.Id == id && c.TenantId == tenantId)
            .Select(c => new ContainerDto(
                c.Id, c.Code, c.Name, c.Material, c.VolumeMl, c.Color, c.IsActive, c.CreatedAt, c.UpdatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<ContainerDto> CreateAsync(ContainerAddDto dto, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var codeExists = await dbContext.Containers
            .AnyAsync(c => c.TenantId == tenantId && c.Code == dto.Code, cancellationToken);
        if (codeExists)
        {
            throw new InvalidOperationException($"A container with code '{dto.Code}' already exists for this tenant.");
        }

        var container = new Container
        {
            Id = Guid.NewGuid(),
            Code = dto.Code,
            Name = dto.Name,
            Material = dto.Material,
            VolumeMl = dto.VolumeMl,
            Color = dto.Color,
            TenantId = tenantId,
        };

        dbContext.Containers.Add(container);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Container {Code} created for tenant {TenantId}", dto.Code, tenantId);

        return (await GetByIdAsync(container.Id, tenantId, cancellationToken))!;
    }

    public async Task<ContainerDto?> UpdateAsync(Guid id, ContainerUpdateDto dto, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var container = await dbContext.Containers
            .Where(c => c.Id == id && c.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (container is null)
        {
            return null;
        }

        if (container.Code != dto.Code)
        {
            var codeExists = await dbContext.Containers
                .AnyAsync(c => c.TenantId == tenantId && c.Code == dto.Code && c.Id != id, cancellationToken);
            if (codeExists)
            {
                throw new InvalidOperationException($"A container with code '{dto.Code}' already exists for this tenant.");
            }
        }

        container.Code = dto.Code;
        container.Name = dto.Name;
        container.Material = dto.Material;
        container.VolumeMl = dto.VolumeMl;
        container.Color = dto.Color;
        container.IsActive = dto.IsActive;
        container.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Container {Id} updated", id);

        return await GetByIdAsync(id, tenantId, cancellationToken);
    }

    public async Task<ContainerDto?> ToggleStatusAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var container = await dbContext.Containers
            .Where(c => c.Id == id && c.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (container is null)
        {
            return null;
        }

        container.IsActive = !container.IsActive;
        container.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Container {Id} status toggled to {IsActive}", id, container.IsActive);

        return await GetByIdAsync(id, tenantId, cancellationToken);
    }
}
