using AquaPlan.Application.DTOs.SamplingLocations;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

internal class SamplingLocationService(
    AquaPlanDbContext dbContext,
    ILogger<SamplingLocationService> logger) : ISamplingLocationService
{
    public async Task<IList<SamplingLocationDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.SamplingLocations
            .Where(sl => sl.Distributor!.TenantId == tenantId)
            .Include(sl => sl.Distributor)
            .OrderBy(sl => sl.Name)
            .Select(sl => MapToDto(sl))
            .ToListAsync(cancellationToken);
    }

    public async Task<SamplingLocationListDto> GetFilteredAsync(SamplingLocationFilteringInputDto filter, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var query = dbContext.SamplingLocations
            .Where(sl => sl.Distributor!.TenantId == tenantId)
            .Include(sl => sl.Distributor)
            .AsQueryable();

        if (filter.DistributorId.HasValue)
        {
            query = query.Where(sl => sl.DistributorId == filter.DistributorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.ToLower();
            query = query.Where(sl =>
                sl.Name.ToLower().Contains(search) ||
                sl.LocationCode.ToLower().Contains(search));
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(sl => sl.IsActive == filter.IsActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(sl => sl.Name)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(sl => MapToDto(sl))
            .ToListAsync(cancellationToken);

        return new SamplingLocationListDto(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<IList<SamplingLocationDto>> GetByDistributorAsync(Guid distributorId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.SamplingLocations
            .Where(sl => sl.DistributorId == distributorId && sl.Distributor!.TenantId == tenantId)
            .Include(sl => sl.Distributor)
            .OrderBy(sl => sl.Name)
            .Select(sl => MapToDto(sl))
            .ToListAsync(cancellationToken);
    }

    public async Task<IList<SamplingLocationDto>> GetForUserAsync(string userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var userDistributorIds = await dbContext.UserDistributors
            .Where(ud => ud.UserId == userId)
            .Select(ud => ud.DistributorId)
            .ToListAsync(cancellationToken);

        return await dbContext.SamplingLocations
            .Where(sl => userDistributorIds.Contains(sl.DistributorId) && sl.Distributor!.TenantId == tenantId)
            .Include(sl => sl.Distributor)
            .OrderBy(sl => sl.Name)
            .Select(sl => MapToDto(sl))
            .ToListAsync(cancellationToken);
    }

    public async Task<SamplingLocationDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.SamplingLocations
            .Where(sl => sl.Id == id && sl.Distributor!.TenantId == tenantId)
            .Include(sl => sl.Distributor)
            .Select(sl => MapToDto(sl))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SamplingLocationDto> CreateAsync(SamplingLocationCreateDto dto, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var isUnique = await IsLocationCodeUniqueAsync(dto.LocationCode, dto.DistributorId, null, tenantId, cancellationToken);
        if (!isUnique)
        {
            throw new InvalidOperationException($"A sampling location with code '{dto.LocationCode}' already exists for this distributor.");
        }

        var location = new SamplingLocation
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            LocationCode = dto.LocationCode,
            Latitude = dto.Latitude,
            Longitude = dto.Longitude,
            Description = dto.Description,
            DistributorId = dto.DistributorId,
        };

        dbContext.SamplingLocations.Add(location);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Sampling location {Name} ({Code}) created", dto.Name, dto.LocationCode);

        return (await GetByIdAsync(location.Id, tenantId, cancellationToken))!;
    }

    public async Task<SamplingLocationDto?> UpdateAsync(Guid id, SamplingLocationUpdateDto dto, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var location = await dbContext.SamplingLocations
            .Where(sl => sl.Id == id && sl.Distributor!.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (location is null)
        {
            return null;
        }

        var isUnique = await IsLocationCodeUniqueAsync(dto.LocationCode, location.DistributorId, id, tenantId, cancellationToken);
        if (!isUnique)
        {
            throw new InvalidOperationException($"A sampling location with code '{dto.LocationCode}' already exists for this distributor.");
        }

        location.Name = dto.Name;
        location.LocationCode = dto.LocationCode;
        location.Latitude = dto.Latitude;
        location.Longitude = dto.Longitude;
        location.Description = dto.Description;
        location.IsActive = dto.IsActive;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Sampling location {Id} updated", id);

        return await GetByIdAsync(id, tenantId, cancellationToken);
    }

    public async Task<ToggleStatusResultDto?> ToggleStatusAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var location = await dbContext.SamplingLocations
            .Where(sl => sl.Id == id && sl.Distributor!.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (location is null)
        {
            return null;
        }

        location.IsActive = !location.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Sampling location {Id} status toggled to {IsActive}", id, location.IsActive);

        var dto = (await GetByIdAsync(id, tenantId, cancellationToken))!;

        // Warning placeholder: when SamplingLocation is referenced by active orders/samplings,
        // add a warning message here. Currently no FK exists from Order/Sampling to SamplingLocation.
        var hasActiveReferences = false;
        string? warning = null;

        return new ToggleStatusResultDto(dto, hasActiveReferences, warning);
    }

    public async Task<bool> IsLocationCodeUniqueAsync(string locationCode, Guid distributorId, Guid? excludeId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var query = dbContext.SamplingLocations
            .Where(sl => sl.LocationCode == locationCode
                && sl.DistributorId == distributorId
                && sl.Distributor!.TenantId == tenantId);

        if (excludeId.HasValue)
        {
            query = query.Where(sl => sl.Id != excludeId.Value);
        }

        return !await query.AnyAsync(cancellationToken);
    }

    private static SamplingLocationDto MapToDto(SamplingLocation sl)
    {
        return new SamplingLocationDto(
            sl.Id, sl.Name, sl.LocationCode, sl.Latitude, sl.Longitude,
            sl.Description, sl.IsActive, sl.DistributorId,
            sl.Distributor != null ? sl.Distributor.Name : null,
            sl.CreatedAt);
    }
}
