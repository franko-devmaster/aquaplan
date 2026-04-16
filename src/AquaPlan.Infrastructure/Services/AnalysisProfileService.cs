using AquaPlan.Application.DTOs.AnalysisProfiles;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

internal class AnalysisProfileService(
    AquaPlanDbContext dbContext,
    ILogger<AnalysisProfileService> logger) : IAnalysisProfileService
{
    public async Task<IList<AnalysisProfileListDto>> GetAllAsync(Guid tenantId, AnalysisProfileFilteringInputDto? filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.AnalysisProfiles
            .Include(p => p.Container)
            .Where(p => p.TenantId == tenantId);

        if (filter is not null)
        {
            if (!string.IsNullOrWhiteSpace(filter.Search))
            {
                var search = filter.Search.ToLower();
                query = query.Where(p => p.Name.ToLower().Contains(search) || p.Code.ToLower().Contains(search));
            }

            if (filter.Category.HasValue)
            {
                query = query.Where(p => p.Category == filter.Category.Value);
            }

            if (filter.IsActive.HasValue)
            {
                query = query.Where(p => p.IsActive == filter.IsActive.Value);
            }
        }

        return await query
            .OrderBy(p => p.Code)
            .Select(p => new AnalysisProfileListDto(
                p.Id, p.Code, p.Name, p.Category, p.IsActive, p.ContainerId, p.Container!.Code))
            .ToListAsync(cancellationToken);
    }

    public async Task<AnalysisProfileDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.AnalysisProfiles
            .Include(p => p.Container)
            .Where(p => p.Id == id && p.TenantId == tenantId)
            .Select(p => new AnalysisProfileDto(
                p.Id, p.Code, p.Name, p.Description, p.Category, p.IsActive,
                p.ContainerId, p.Container!.Code, p.Container!.Name, p.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<AnalysisProfileDto> CreateAsync(AnalysisProfileAddDto dto, Guid tenantId, CancellationToken cancellationToken = default)
    {
        await EnsureContainerExistsAndActiveAsync(dto.ContainerId, tenantId, cancellationToken);

        var profile = new AnalysisProfile
        {
            Id = Guid.NewGuid(),
            Code = dto.Code,
            Name = dto.Name,
            Description = dto.Description,
            Category = dto.Category,
            TenantId = tenantId,
            ContainerId = dto.ContainerId,
        };

        dbContext.AnalysisProfiles.Add(profile);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Analysis profile {Code} created for tenant {TenantId}", dto.Code, tenantId);

        return (await GetByIdAsync(profile.Id, tenantId, cancellationToken))!;
    }

    public async Task<AnalysisProfileDto?> UpdateAsync(Guid id, AnalysisProfileUpdateDto dto, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.AnalysisProfiles
            .Where(p => p.Id == id && p.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            return null;
        }

        if (profile.ContainerId != dto.ContainerId)
        {
            await EnsureContainerExistsAndActiveAsync(dto.ContainerId, tenantId, cancellationToken);
        }

        profile.Code = dto.Code;
        profile.Name = dto.Name;
        profile.Description = dto.Description;
        profile.Category = dto.Category;
        profile.IsActive = dto.IsActive;
        profile.ContainerId = dto.ContainerId;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Analysis profile {Id} updated", id);

        return await GetByIdAsync(id, tenantId, cancellationToken);
    }

    public async Task<AnalysisProfileDto?> ToggleStatusAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var profile = await dbContext.AnalysisProfiles
            .Where(p => p.Id == id && p.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            return null;
        }

        profile.IsActive = !profile.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Analysis profile {Id} status toggled to {IsActive}", id, profile.IsActive);

        return await GetByIdAsync(id, tenantId, cancellationToken);
    }

    private async Task EnsureContainerExistsAndActiveAsync(Guid containerId, Guid tenantId, CancellationToken cancellationToken)
    {
        var container = await dbContext.Containers
            .Where(c => c.Id == containerId && c.TenantId == tenantId)
            .Select(c => new { c.Id, c.IsActive })
            .FirstOrDefaultAsync(cancellationToken);

        if (container is null)
        {
            throw new InvalidOperationException($"Container '{containerId}' does not exist for this tenant.");
        }

        if (!container.IsActive)
        {
            throw new InvalidOperationException("Cannot assign an inactive container to an analysis profile.");
        }
    }
}
