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
    public async Task<IList<SamplingLocationDto>> GetByDistributorAsync(Guid distributorId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.SamplingLocations
            .Where(sl => sl.DistributorId == distributorId && sl.Distributor!.TenantId == tenantId)
            .Include(sl => sl.Distributor)
            .OrderBy(sl => sl.Name)
            .Select(sl => new SamplingLocationDto(
                sl.Id, sl.Name, sl.LocationCode, sl.Latitude, sl.Longitude,
                sl.Description, sl.IsActive, sl.DistributorId,
                sl.Distributor != null ? sl.Distributor.Name : null,
                sl.CreatedAt))
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
            .Select(sl => new SamplingLocationDto(
                sl.Id, sl.Name, sl.LocationCode, sl.Latitude, sl.Longitude,
                sl.Description, sl.IsActive, sl.DistributorId,
                sl.Distributor != null ? sl.Distributor.Name : null,
                sl.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<SamplingLocationDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.SamplingLocations
            .Where(sl => sl.Id == id && sl.Distributor!.TenantId == tenantId)
            .Include(sl => sl.Distributor)
            .Select(sl => new SamplingLocationDto(
                sl.Id, sl.Name, sl.LocationCode, sl.Latitude, sl.Longitude,
                sl.Description, sl.IsActive, sl.DistributorId,
                sl.Distributor != null ? sl.Distributor.Name : null,
                sl.CreatedAt))
            .FirstOrDefaultAsync(cancellationToken);
    }

    public async Task<SamplingLocationDto> CreateAsync(SamplingLocationCreateDto dto, Guid tenantId, CancellationToken cancellationToken = default)
    {
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
}
