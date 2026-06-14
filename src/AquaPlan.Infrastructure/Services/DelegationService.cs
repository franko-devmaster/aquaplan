using AquaPlan.Application.DTOs.Delegations;
using AquaPlan.Application.Exceptions;
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
        // Polish F-219 — validate the delegation before persisting:
        //   (1) the two distributors must differ (a self-delegation is meaningless),
        //   (2) both must exist in the caller's tenant (FK alone allows cross-tenant references
        //       that would silently widen authorization, since GetAuthorizedDistributorIds does not
        //       re-filter by tenant on the Orders queries),
        //   (3) the validity window must be coherent (ValidFrom <= ValidTo),
        //   (4) no overlapping active delegation already exists for the same pair.
        if (dto.DelegatingDistributorId == dto.DelegatedToDistributorId)
        {
            throw new BusinessRuleException("A distributor cannot delegate to itself.");
        }

        var validFrom = DateTime.SpecifyKind(dto.ValidFrom, DateTimeKind.Utc);
        DateTime? validTo = dto.ValidTo.HasValue ? DateTime.SpecifyKind(dto.ValidTo.Value, DateTimeKind.Utc) : null;
        if (validTo.HasValue && validTo.Value < validFrom)
        {
            throw new BusinessRuleException("The delegation end date must be on or after the start date.");
        }

        var distributorsInTenant = await dbContext.Distributors
            .CountAsync(d => d.TenantId == tenantId
                && (d.Id == dto.DelegatingDistributorId || d.Id == dto.DelegatedToDistributorId), cancellationToken);
        if (distributorsInTenant != 2)
        {
            throw new BusinessRuleException("Both distributors must belong to your tenant.");
        }

        var hasActiveDuplicate = await dbContext.DistributorDelegations
            .AnyAsync(d => d.TenantId == tenantId
                && d.IsActive
                && d.DelegatingDistributorId == dto.DelegatingDistributorId
                && d.DelegatedToDistributorId == dto.DelegatedToDistributorId, cancellationToken);
        if (hasActiveDuplicate)
        {
            throw new BusinessRuleException("An active delegation already exists for these distributors.");
        }

        var delegation = new DistributorDelegation
        {
            Id = Guid.NewGuid(),
            DelegatingDistributorId = dto.DelegatingDistributorId,
            DelegatedToDistributorId = dto.DelegatedToDistributorId,
            ValidFrom = validFrom,
            ValidTo = validTo,
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

    public async Task<bool> UserHasDistributorAccessAsync(string userId, Guid distributorId, CancellationToken cancellationToken = default)
    {
        // Polish F-227 — single source of truth, built on the canonical authorized set.
        var authorizedIds = await GetAuthorizedDistributorIdsForUserAsync(userId, cancellationToken);
        return authorizedIds.Contains(distributorId);
    }

    public async Task<List<Guid>> GetAuthorizedDistributorIdsForUserAsync(string userId, CancellationToken cancellationToken = default)
    {
        // AQ-369 — own distributors + distributors that delegated to user's distributors.
        // Own distributors include both AppUser.DistributorId (primary) and UserDistributors
        // (secondary many-to-many). Bug fix: we previously ignored AppUser.DistributorId, so
        // users with only the primary link saw no data.
        var userDistributorIds = await GetOwnDistributorIdsAsync(userId, cancellationToken);

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

    private async Task<List<Guid>> GetOwnDistributorIdsAsync(string userId, CancellationToken cancellationToken)
    {
        var primaryDistributorId = await dbContext.Users
            .Where(u => u.Id == userId)
            .Select(u => u.DistributorId)
            .FirstOrDefaultAsync(cancellationToken);

        var linkedDistributorIds = await dbContext.UserDistributors
            .Where(ud => ud.UserId == userId)
            .Select(ud => ud.DistributorId)
            .ToListAsync(cancellationToken);

        if (primaryDistributorId.HasValue && !linkedDistributorIds.Contains(primaryDistributorId.Value))
        {
            linkedDistributorIds.Add(primaryDistributorId.Value);
        }

        return linkedDistributorIds;
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
