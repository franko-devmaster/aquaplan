using AquaPlan.Application.DTOs.ChangeRequests;
using AquaPlan.Application.Exceptions;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

internal class SamplingLocationChangeRequestService(
    AquaPlanDbContext dbContext,
    IDelegationService delegationService,
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
                // Polish F-218 — enforce LocationCode uniqueness on the approval path too (the
                // direct creation path checks it; bypassing it here would create duplicates).
                await EnsureLocationCodeUniqueAsync(request.ProposedLocationCode!, request.DistributorId, null, tenantId, cancellationToken);
                var newLocation = new SamplingLocation
                {
                    Id = Guid.NewGuid(),
                    Name = request.ProposedName!,
                    LocationCode = request.ProposedLocationCode!,
                    Description = request.ProposedDescription,
                    DistributorId = request.DistributorId,
                    IsActive = true,
                    // Polish F-218 — the admin review IS the validation, so the approved LDP must be
                    // marked validated; otherwise it was IsActive=true but IsValidated=false (entity
                    // default) and never appeared in offline snapshots (filtered on IsValidated).
                    IsValidated = true,
                };
                dbContext.SamplingLocations.Add(newLocation);
                request.SamplingLocationId = newLocation.Id;
                break;

            case ChangeRequestType.Update:
                var locationToUpdate = await dbContext.SamplingLocations
                    .FirstOrDefaultAsync(sl => sl.Id == request.SamplingLocationId, cancellationToken);
                if (locationToUpdate is not null)
                {
                    // Polish F-218 — uniqueness check excludes the location being updated.
                    await EnsureLocationCodeUniqueAsync(request.ProposedLocationCode!, locationToUpdate.DistributorId, locationToUpdate.Id, tenantId, cancellationToken);
                    locationToUpdate.Name = request.ProposedName!;
                    locationToUpdate.LocationCode = request.ProposedLocationCode!;
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

    /// <summary>
    /// Polish F-218 — mirrors SamplingLocationService.IsLocationCodeUniqueAsync; throws a
    /// <see cref="BusinessRuleException"/> when the code already exists for the distributor in the
    /// tenant (optionally excluding the location being updated).
    /// </summary>
    private async Task EnsureLocationCodeUniqueAsync(string locationCode, Guid distributorId, Guid? excludeId, Guid tenantId, CancellationToken cancellationToken)
    {
        var query = dbContext.SamplingLocations
            .Where(sl => sl.LocationCode == locationCode
                && sl.DistributorId == distributorId
                && sl.Distributor!.TenantId == tenantId);

        if (excludeId.HasValue)
        {
            query = query.Where(sl => sl.Id != excludeId.Value);
        }

        if (await query.AnyAsync(cancellationToken))
        {
            throw new BusinessRuleException($"A sampling location with code '{locationCode}' already exists for this distributor.");
        }
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
        // Sprint Robustesse F-113 — authorization must use the same set as the rest of the app:
        // the user's primary distributor (AppUser.DistributorId), their UserDistributors links,
        // AND active delegations. The previous check only looked at UserDistributors, so users
        // whose sole link was the primary distributor got a spurious 403 and delegatees could not
        // submit requests for the delegating distributor.
        var authorizedIds = await delegationService.GetAuthorizedDistributorIdsForUserAsync(userId, cancellationToken);
        if (!authorizedIds.Contains(distributorId))
        {
            // Generic message — never leak internal ids to the client (audit F-203).
            throw new UnauthorizedAccessException("Access to this distributor is denied.");
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
