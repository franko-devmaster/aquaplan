using AquaPlan.Application.DTOs.ChangeRequests;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

internal class SamplingLocationChangeRequestService(
    AquaPlanDbContext dbContext,
    ILogger<SamplingLocationChangeRequestService> logger) : ISamplingLocationChangeRequestService
{
    public async Task<ChangeRequestDto> SubmitCreateRequestAsync(ChangeRequestCreateDto dto, string userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        await ValidateUserDistributorAccessAsync(userId, dto.DistributorId, cancellationToken);

        var request = new SamplingLocationChangeRequest
        {
            Id = Guid.NewGuid(),
            RequestType = ChangeRequestType.Create,
            Status = ChangeRequestStatus.Pending,
            DistributorId = dto.DistributorId,
            ProposedName = dto.Name,
            ProposedLocationCode = dto.LocationCode,
            ProposedLatitude = dto.Latitude,
            ProposedLongitude = dto.Longitude,
            ProposedDescription = dto.Description,
            RequestedById = userId,
            TenantId = tenantId,
        };

        dbContext.SamplingLocationChangeRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Change request {Id} (Create) submitted by {UserId}", request.Id, userId);

        return (await GetByIdAsync(request.Id, tenantId, cancellationToken))!;
    }

    public async Task<ChangeRequestDto> SubmitUpdateRequestAsync(Guid samplingLocationId, ChangeRequestUpdateDto dto, string userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var location = await dbContext.SamplingLocations
            .Where(sl => sl.Id == samplingLocationId && sl.Distributor!.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Sampling location {samplingLocationId} not found.");

        await ValidateUserDistributorAccessAsync(userId, location.DistributorId, cancellationToken);

        var request = new SamplingLocationChangeRequest
        {
            Id = Guid.NewGuid(),
            RequestType = ChangeRequestType.Update,
            Status = ChangeRequestStatus.Pending,
            SamplingLocationId = samplingLocationId,
            DistributorId = location.DistributorId,
            ProposedName = dto.Name,
            ProposedLocationCode = dto.LocationCode,
            ProposedLatitude = dto.Latitude,
            ProposedLongitude = dto.Longitude,
            ProposedDescription = dto.Description,
            RequestedById = userId,
            TenantId = tenantId,
        };

        dbContext.SamplingLocationChangeRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Change request {Id} (Update) submitted for location {LocationId} by {UserId}", request.Id, samplingLocationId, userId);

        return (await GetByIdAsync(request.Id, tenantId, cancellationToken))!;
    }

    public async Task<ChangeRequestDto> SubmitDeactivateRequestAsync(Guid samplingLocationId, string userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var location = await dbContext.SamplingLocations
            .Where(sl => sl.Id == samplingLocationId && sl.Distributor!.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw new KeyNotFoundException($"Sampling location {samplingLocationId} not found.");

        await ValidateUserDistributorAccessAsync(userId, location.DistributorId, cancellationToken);

        var request = new SamplingLocationChangeRequest
        {
            Id = Guid.NewGuid(),
            RequestType = ChangeRequestType.Deactivate,
            Status = ChangeRequestStatus.Pending,
            SamplingLocationId = samplingLocationId,
            DistributorId = location.DistributorId,
            RequestedById = userId,
            TenantId = tenantId,
        };

        dbContext.SamplingLocationChangeRequests.Add(request);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Change request {Id} (Deactivate) submitted for location {LocationId} by {UserId}", request.Id, samplingLocationId, userId);

        return (await GetByIdAsync(request.Id, tenantId, cancellationToken))!;
    }

    public async Task<IList<ChangeRequestDto>> GetMyRequestsAsync(string userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var entities = await QueryRequests()
            .Where(cr => cr.RequestedById == userId && cr.TenantId == tenantId)
            .OrderByDescending(cr => cr.RequestedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto).ToList();
    }

    public async Task<IList<ChangeRequestDto>> GetPendingRequestsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var entities = await QueryRequests()
            .Where(cr => cr.Status == ChangeRequestStatus.Pending && cr.TenantId == tenantId)
            .OrderBy(cr => cr.RequestedAt)
            .ToListAsync(cancellationToken);

        return entities.Select(MapToDto).ToList();
    }

    public async Task<ChangeRequestDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var entity = await QueryRequests()
            .Where(cr => cr.Id == id && cr.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        return entity is null ? null : MapToDto(entity);
    }

    public async Task<ChangeRequestDto?> ApproveAsync(Guid id, string? comment, string reviewerId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var request = await dbContext.SamplingLocationChangeRequests
            .Where(cr => cr.Id == id && cr.TenantId == tenantId && cr.Status == ChangeRequestStatus.Pending)
            .FirstOrDefaultAsync(cancellationToken);

        if (request is null)
        {
            return null;
        }

        request.Status = ChangeRequestStatus.Approved;
        request.ReviewedById = reviewerId;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewComment = comment;

        switch (request.RequestType)
        {
            case ChangeRequestType.Create:
                var newLocation = new SamplingLocation
                {
                    Id = Guid.NewGuid(),
                    Name = request.ProposedName!,
                    LocationCode = request.ProposedLocationCode!,
                    Latitude = request.ProposedLatitude,
                    Longitude = request.ProposedLongitude,
                    Description = request.ProposedDescription,
                    DistributorId = request.DistributorId,
                    IsActive = true,
                };
                dbContext.SamplingLocations.Add(newLocation);
                request.SamplingLocationId = newLocation.Id;
                break;

            case ChangeRequestType.Update:
                var locationToUpdate = await dbContext.SamplingLocations
                    .FirstOrDefaultAsync(sl => sl.Id == request.SamplingLocationId, cancellationToken);
                if (locationToUpdate is not null)
                {
                    locationToUpdate.Name = request.ProposedName!;
                    locationToUpdate.LocationCode = request.ProposedLocationCode!;
                    locationToUpdate.Latitude = request.ProposedLatitude;
                    locationToUpdate.Longitude = request.ProposedLongitude;
                    locationToUpdate.Description = request.ProposedDescription;
                }
                break;

            case ChangeRequestType.Deactivate:
                var locationToDeactivate = await dbContext.SamplingLocations
                    .FirstOrDefaultAsync(sl => sl.Id == request.SamplingLocationId, cancellationToken);
                if (locationToDeactivate is not null)
                {
                    locationToDeactivate.IsActive = false;
                }
                break;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Change request {Id} approved by {ReviewerId}", id, reviewerId);

        return await GetByIdAsync(id, tenantId, cancellationToken);
    }

    public async Task<ChangeRequestDto?> RejectAsync(Guid id, string comment, string reviewerId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var request = await dbContext.SamplingLocationChangeRequests
            .Where(cr => cr.Id == id && cr.TenantId == tenantId && cr.Status == ChangeRequestStatus.Pending)
            .FirstOrDefaultAsync(cancellationToken);

        if (request is null)
        {
            return null;
        }

        request.Status = ChangeRequestStatus.Rejected;
        request.ReviewedById = reviewerId;
        request.ReviewedAt = DateTime.UtcNow;
        request.ReviewComment = comment;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Change request {Id} rejected by {ReviewerId}", id, reviewerId);

        return await GetByIdAsync(id, tenantId, cancellationToken);
    }

    private IQueryable<SamplingLocationChangeRequest> QueryRequests()
    {
        return dbContext.SamplingLocationChangeRequests
            .Include(cr => cr.SamplingLocation)
            .Include(cr => cr.Distributor)
            .Include(cr => cr.RequestedBy)
            .Include(cr => cr.ReviewedBy);
    }

    private async Task ValidateUserDistributorAccessAsync(string userId, Guid distributorId, CancellationToken cancellationToken)
    {
        var hasAccess = await dbContext.UserDistributors
            .AnyAsync(ud => ud.UserId == userId && ud.DistributorId == distributorId, cancellationToken);

        if (!hasAccess)
        {
            throw new UnauthorizedAccessException($"User {userId} does not have access to distributor {distributorId}.");
        }
    }

    private static ChangeRequestDto MapToDto(SamplingLocationChangeRequest cr)
    {
        return new ChangeRequestDto(
            cr.Id,
            cr.RequestType,
            cr.Status,
            cr.SamplingLocationId,
            cr.SamplingLocation?.Name,
            cr.DistributorId,
            cr.Distributor?.Name,
            cr.ProposedName,
            cr.ProposedLocationCode,
            cr.ProposedLatitude,
            cr.ProposedLongitude,
            cr.ProposedDescription,
            cr.RequestedById,
            cr.RequestedBy != null ? $"{cr.RequestedBy.FirstName} {cr.RequestedBy.LastName}" : null,
            cr.RequestedAt,
            cr.ReviewedById,
            cr.ReviewedBy != null ? $"{cr.ReviewedBy.FirstName} {cr.ReviewedBy.LastName}" : null,
            cr.ReviewedAt,
            cr.ReviewComment);
    }
}
