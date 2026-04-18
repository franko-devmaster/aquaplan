using AquaPlan.Application.DTOs.Delegations;
using AquaPlan.Application.DTOs.Distributors;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

internal class DelegationService(
    AquaPlanDbContext dbContext,
    ILogger<DelegationService> logger) : IDelegationService
{
    public async Task<List<DistributorDelegationDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.DistributorDelegations
            .Where(d => d.TenantId == tenantId)
            .Include(d => d.DelegatingDistributor)
            .Include(d => d.DelegatedToDistributor)
            .OrderByDescending(d => d.CreatedAt)
            .Select(d => new DistributorDelegationDto(
                d.Id,
                d.DelegatingDistributorId,
                d.DelegatingDistributor != null ? d.DelegatingDistributor.Name : string.Empty,
                d.DelegatedToDistributorId,
                d.DelegatedToDistributor != null ? d.DelegatedToDistributor.Name : string.Empty,
                d.ValidFrom,
                d.ValidTo,
                d.IsActive,
                d.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<DistributorDelegationDto> CreateAsync(DistributorDelegationCreateDto dto, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var delegation = new DistributorDelegation
        {
            Id = Guid.NewGuid(),
            DelegatingDistributorId = dto.DelegatingDistributorId,
            DelegatedToDistributorId = dto.DelegatedToDistributorId,
            ValidFrom = DateTime.SpecifyKind(dto.ValidFrom, DateTimeKind.Utc),
            ValidTo = dto.ValidTo.HasValue ? DateTime.SpecifyKind(dto.ValidTo.Value, DateTimeKind.Utc) : null,
            IsActive = true,
            TenantId = tenantId,
        };

        dbContext.DistributorDelegations.Add(delegation);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Delegation created: {DelegatingId} → {DelegatedToId}", dto.DelegatingDistributorId, dto.DelegatedToDistributorId);

        // Reload with navigation properties
        var created = await dbContext.DistributorDelegations
            .Include(d => d.DelegatingDistributor)
            .Include(d => d.DelegatedToDistributor)
            .FirstAsync(d => d.Id == delegation.Id, cancellationToken);

        return new DistributorDelegationDto(
            created.Id,
            created.DelegatingDistributorId,
            created.DelegatingDistributor?.Name ?? string.Empty,
            created.DelegatedToDistributorId,
            created.DelegatedToDistributor?.Name ?? string.Empty,
            created.ValidFrom,
            created.ValidTo,
            created.IsActive,
            created.CreatedAt);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var delegation = await dbContext.DistributorDelegations
            .FirstOrDefaultAsync(d => d.Id == id && d.TenantId == tenantId, cancellationToken);

        if (delegation is null)
        {
            return false;
        }

        dbContext.DistributorDelegations.Remove(delegation);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Delegation {Id} deleted", id);
        return true;
    }

    public async Task<List<Guid>> GetDelegatedDistributorIdsAsync(string userId, CancellationToken cancellationToken = default)
    {
        // Get distributors the user belongs to
        var userDistributorIds = await dbContext.UserDistributors
            .Where(ud => ud.UserId == userId)
            .Select(ud => ud.DistributorId)
            .ToListAsync(cancellationToken);

        // Get distributors that have delegated to the user's distributors
        var delegatedIds = await dbContext.DistributorDelegations
            .Where(d => d.IsActive
                && userDistributorIds.Contains(d.DelegatedToDistributorId)
                && d.ValidFrom <= DateTime.UtcNow
                && (d.ValidTo == null || d.ValidTo >= DateTime.UtcNow))
            .Select(d => d.DelegatingDistributorId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return delegatedIds;
    }

    public async Task<List<Guid>> GetAuthorizedDistributorIdsForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        // AQ-369 — own distributors + distributors that delegated to user's distributors
        var userDistributorIds = await dbContext.UserDistributors
            .Where(ud => ud.UserId == userId)
            .Select(ud => ud.DistributorId)
            .ToListAsync(cancellationToken);

        var delegatedIds = await dbContext.DistributorDelegations
            .Where(d => d.IsActive
                && userDistributorIds.Contains(d.DelegatedToDistributorId)
                && d.ValidFrom <= DateTime.UtcNow
                && (d.ValidTo == null || d.ValidTo >= DateTime.UtcNow))
            .Select(d => d.DelegatingDistributorId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return userDistributorIds.Union(delegatedIds).ToList();
    }

    public async Task<List<DistributorDto>> GetAuthorizedDistributorsForUserAsync(string userId, Guid tenantId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        // AQ-369 — admin sees all distributors of the tenant; others get own + delegating ones
        if (isAdmin)
        {
            return await dbContext.Distributors
                .Where(d => d.TenantId == tenantId && d.IsActive)
                .OrderBy(d => d.Name)
                .Select(d => new DistributorDto(
                    d.Id, d.Name, d.ShortName, d.CantonRegion, d.DistributionNetwork,
                    d.IsActive, d.CreatedAt))
                .ToListAsync(cancellationToken);
        }

        var authorizedIds = await GetAuthorizedDistributorIdsForUserAsync(userId, cancellationToken);
        return await dbContext.Distributors
            .Where(d => d.TenantId == tenantId && d.IsActive && authorizedIds.Contains(d.Id))
            .OrderBy(d => d.Name)
            .Select(d => new DistributorDto(
                d.Id, d.Name, d.ShortName, d.CantonRegion, d.DistributionNetwork,
                d.IsActive, d.CreatedAt))
            .ToListAsync(cancellationToken);
    }
}
