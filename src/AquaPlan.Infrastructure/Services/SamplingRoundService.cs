using AquaPlan.Application.DTOs.SamplingRounds;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

internal class SamplingRoundService(
    AquaPlanDbContext dbContext,
    ILogger<SamplingRoundService> logger) : ISamplingRoundService
{
    public async Task<SamplingRoundDetailDto> CreateAsync(
        SamplingRoundCreateDto dto, string createdById, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var round = new SamplingRound
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            Description = dto.Description,
            Deadline = dto.Deadline.HasValue ? DateTime.SpecifyKind(dto.Deadline.Value, DateTimeKind.Utc) : null,
            Notes = dto.Notes,
            DistributorId = dto.DistributorId,
            Status = SamplingRoundStatus.Draft,
            TenantId = tenantId,
            CreatedById = createdById,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.SamplingRounds.Add(round);
        await dbContext.SaveChangesAsync(cancellationToken);

        return (await GetByIdAsync(round.Id, tenantId, cancellationToken))!;
    }

    public async Task<SamplingRoundDetailDto?> GetByIdAsync(
        Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var round = await dbContext.SamplingRounds
            .Include(sr => sr.Preleveur)
            .Include(sr => sr.Distributor)
            .Include(sr => sr.CreatedBy)
            .Include(sr => sr.Orders.OrderBy(o => o.SortOrder))
                .ThenInclude(o => o.SamplingLocation)
                    .ThenInclude(l => l!.Sector)
            .Include(sr => sr.Orders)
                .ThenInclude(o => o.OriginalSamplingLocation)
            .Include(sr => sr.Orders)
                .ThenInclude(o => o.OrderAnalysisProfiles)
                    .ThenInclude(oap => oap.AnalysisProfile)
            .Where(sr => sr.TenantId == tenantId && sr.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (round is null) return null;

        return MapToDetailDto(round);
    }

    public async Task<SamplingRoundPagedResultDto> GetFilteredAsync(
        string userId, Guid tenantId, SamplingRoundFilterDto filter, bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.SamplingRounds
            .Include(sr => sr.Preleveur)
            .Include(sr => sr.Distributor)
            .Include(sr => sr.Orders)
            .Where(sr => sr.TenantId == tenantId);

        if (!isAdmin)
        {
            var distributorIds = await dbContext.UserDistributors
                .Where(ud => ud.UserId == userId)
                .Select(ud => ud.DistributorId)
                .ToListAsync(cancellationToken);

            query = query.Where(sr => distributorIds.Contains(sr.DistributorId) || sr.PreleveurId == userId);
        }

        if (filter.Status.HasValue)
        {
            query = query.Where(sr => sr.Status == filter.Status.Value);
        }

        if (filter.DistributorId.HasValue)
        {
            query = query.Where(sr => sr.DistributorId == filter.DistributorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.PreleveurId))
        {
            query = query.Where(sr => sr.PreleveurId == filter.PreleveurId);
        }

        if (filter.DeadlineFrom.HasValue)
        {
            var from = DateTime.SpecifyKind(filter.DeadlineFrom.Value, DateTimeKind.Utc);
            query = query.Where(sr => sr.Deadline >= from);
        }

        if (filter.DeadlineTo.HasValue)
        {
            var to = DateTime.SpecifyKind(filter.DeadlineTo.Value, DateTimeKind.Utc);
            query = query.Where(sr => sr.Deadline <= to);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.ToLower();
            query = query.Where(sr => sr.Name.ToLower().Contains(search));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderByDescending(sr => sr.CreatedAt)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(sr => new SamplingRoundListDto(
                sr.Id,
                sr.Name,
                sr.Description,
                sr.Deadline,
                sr.Status,
                sr.PreleveurId,
                sr.Preleveur != null ? sr.Preleveur.FirstName + " " + sr.Preleveur.LastName : null,
                sr.DistributorId,
                sr.Distributor!.Name,
                sr.Distributor.ShortName,
                sr.Notes,
                sr.Orders.Count,
                sr.Orders.Count(o => o.Status >= OrderStatus.Completed && o.Status != OrderStatus.Cancelled),
                sr.CreatedAt))
            .ToListAsync(cancellationToken);

        return new SamplingRoundPagedResultDto(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<SamplingRoundDetailDto?> UpdateAsync(
        Guid id, SamplingRoundUpdateDto dto, string updatedBy, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var round = await dbContext.SamplingRounds
            .Where(sr => sr.TenantId == tenantId && sr.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (round is null) return null;

        if (round.Status is SamplingRoundStatus.InProgress or SamplingRoundStatus.Completed or SamplingRoundStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot update a sampling round in status {round.Status}");
        }

        round.Name = dto.Name;
        round.Description = dto.Description;
        round.Deadline = dto.Deadline.HasValue ? DateTime.SpecifyKind(dto.Deadline.Value, DateTimeKind.Utc) : null;
        round.Notes = dto.Notes;
        round.UpdatedAt = DateTime.UtcNow;
        round.UpdatedBy = updatedBy;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, tenantId, cancellationToken);
    }

    public async Task<bool> DeleteAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var round = await dbContext.SamplingRounds
            .Include(sr => sr.Orders)
            .Where(sr => sr.TenantId == tenantId && sr.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (round is null) return false;

        if (round.Status != SamplingRoundStatus.Draft)
        {
            throw new InvalidOperationException("Only Draft sampling rounds can be deleted");
        }

        foreach (var order in round.Orders.ToList())
        {
            dbContext.Orders.Remove(order);
        }

        dbContext.SamplingRounds.Remove(round);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SamplingRoundDetailDto?> AssignPreleveurAsync(
        Guid id, SamplingRoundAssignDto dto, string updatedBy, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var round = await dbContext.SamplingRounds
            .Include(sr => sr.Orders)
            .Where(sr => sr.TenantId == tenantId && sr.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (round is null) return null;

        if (round.Status is SamplingRoundStatus.InProgress or SamplingRoundStatus.Completed or SamplingRoundStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot assign a préleveur to a sampling round in status {round.Status}");
        }

        if (round.Orders.Count == 0)
        {
            throw new InvalidOperationException("Cannot assign a préleveur to a sampling round with no orders");
        }

        round.PreleveurId = dto.PreleveurId;
        round.Status = SamplingRoundStatus.Assigned;
        round.UpdatedAt = DateTime.UtcNow;
        round.UpdatedBy = updatedBy;

        foreach (var order in round.Orders)
        {
            order.PreleveurId = dto.PreleveurId;
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = updatedBy;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, tenantId, cancellationToken);
    }

    public async Task<SamplingRoundDetailDto?> ValidateAsync(
        Guid id, string userId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var round = await dbContext.SamplingRounds
            .Include(r => r.Preleveur)
            .Include(r => r.Distributor)
            .Include(r => r.CreatedBy)
            .Include(r => r.Orders)
                .ThenInclude(o => o.SamplingLocation)
                    .ThenInclude(l => l!.Sector)
            .Include(r => r.Orders)
                .ThenInclude(o => o.OriginalSamplingLocation)
            .Include(r => r.Orders)
                .ThenInclude(o => o.OrderAnalysisProfiles)
                    .ThenInclude(oap => oap.AnalysisProfile)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, cancellationToken);

        if (round is null) return null;

        if (round.Status != SamplingRoundStatus.Assigned)
        {
            throw new InvalidOperationException("Only assigned rounds can be validated");
        }

        round.Status = SamplingRoundStatus.Validated;
        round.UpdatedAt = DateTime.UtcNow;
        round.UpdatedBy = userId;

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapToDetailDto(round);
    }

    public async Task<SamplingRoundDetailDto?> CancelAsync(
        Guid id, string updatedBy, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var round = await dbContext.SamplingRounds
            .Include(sr => sr.Orders)
            .Where(sr => sr.TenantId == tenantId && sr.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (round is null) return null;

        if (round.Status is SamplingRoundStatus.InProgress or SamplingRoundStatus.Completed or SamplingRoundStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot cancel a sampling round in status {round.Status}");
        }

        round.Status = SamplingRoundStatus.Cancelled;
        round.UpdatedAt = DateTime.UtcNow;
        round.UpdatedBy = updatedBy;

        foreach (var order in round.Orders.Where(o => o.Status != OrderStatus.Cancelled))
        {
            order.Status = OrderStatus.Cancelled;
            order.StatusChangedAt = DateTime.UtcNow;
            order.StatusChangedBy = updatedBy;
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = updatedBy;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, tenantId, cancellationToken);
    }

    public async Task<SamplingRoundDetailDto?> AddOrderAsync(
        Guid roundId, Guid orderId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var round = await dbContext.SamplingRounds
            .Include(sr => sr.Orders)
            .Where(sr => sr.TenantId == tenantId && sr.Id == roundId)
            .FirstOrDefaultAsync(cancellationToken);

        if (round is null) return null;

        if (round.Status is SamplingRoundStatus.InProgress or SamplingRoundStatus.Completed or SamplingRoundStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot modify orders in a sampling round in status {round.Status}");
        }

        var order = await dbContext.Orders
            .Where(o => o.TenantId == tenantId && o.Id == orderId)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            throw new InvalidOperationException($"Order {orderId} not found");
        }

        if (order.DistributorId != round.DistributorId)
        {
            throw new InvalidOperationException("The order must belong to the same distributor as the sampling round");
        }

        if (order.SamplingRoundId is not null && order.SamplingRoundId != Guid.Empty && order.SamplingRoundId != roundId)
        {
            throw new InvalidOperationException("This order is already assigned to another sampling round");
        }

        var maxSortOrder = round.Orders.Any() ? round.Orders.Max(o => o.SortOrder) : -1;

        order.SamplingRoundId = roundId;
        order.SortOrder = maxSortOrder + 1;
        order.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(roundId, tenantId, cancellationToken);
    }

    public async Task<bool> RemoveOrderAsync(
        Guid roundId, Guid orderId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var round = await dbContext.SamplingRounds
            .Where(sr => sr.TenantId == tenantId && sr.Id == roundId)
            .FirstOrDefaultAsync(cancellationToken);

        if (round is null) return false;

        if (round.Status != SamplingRoundStatus.Draft)
        {
            throw new InvalidOperationException("Can only remove orders from Draft sampling rounds");
        }

        var order = await dbContext.Orders
            .Where(o => o.TenantId == tenantId && o.Id == orderId && o.SamplingRoundId == roundId)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null) return false;

        dbContext.Orders.Remove(order);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SamplingRoundDetailDto?> ReorderAsync(
        Guid roundId, SamplingRoundReorderDto dto, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var round = await dbContext.SamplingRounds
            .Include(sr => sr.Orders)
            .Where(sr => sr.TenantId == tenantId && sr.Id == roundId)
            .FirstOrDefaultAsync(cancellationToken);

        if (round is null) return null;

        if (round.Status is SamplingRoundStatus.InProgress or SamplingRoundStatus.Completed or SamplingRoundStatus.Cancelled)
        {
            throw new InvalidOperationException($"Cannot reorder orders in a sampling round in status {round.Status}");
        }

        foreach (var position in dto.Positions)
        {
            var order = round.Orders.FirstOrDefault(o => o.Id == position.OrderId);
            if (order is not null)
            {
                order.SortOrder = position.SortOrder;
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(roundId, tenantId, cancellationToken);
    }

    public async Task<bool> ReplaceLocationAsync(
        Guid orderId, LocationReplacementDto dto, string userId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(o => o.SamplingRound)
            .Where(o => o.TenantId == tenantId && o.Id == orderId)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null) return false;

        if (order.SamplingRound?.PreleveurId != userId)
        {
            throw new InvalidOperationException("Only the assigned préleveur can replace a sampling location");
        }

        if (order.Status is not (OrderStatus.New or OrderStatus.InProgress))
        {
            throw new InvalidOperationException($"Cannot replace location on an order in status {order.Status}");
        }

        var newLocation = await dbContext.SamplingLocations
            .Where(sl => sl.Id == dto.NewSamplingLocationId && sl.IsActive)
            .FirstOrDefaultAsync(cancellationToken);

        if (newLocation is null)
        {
            throw new InvalidOperationException("The replacement sampling location must be active");
        }

        if (newLocation.DistributorId != order.DistributorId)
        {
            throw new InvalidOperationException("The replacement sampling location must belong to the same distributor");
        }

        if (order.OriginalSamplingLocationId is null)
        {
            order.OriginalSamplingLocationId = order.SamplingLocationId;
        }
        order.SamplingLocationId = dto.NewSamplingLocationId;
        order.LocationReplacementReason = dto.Reason;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = userId;

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> StartOrderAsync(
        Guid orderId, string userId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(o => o.SamplingRound)
            .Where(o => o.TenantId == tenantId && o.Id == orderId)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null) return false;

        if (order.SamplingRound?.PreleveurId != userId)
        {
            throw new InvalidOperationException("Only the assigned préleveur can start an order");
        }

        if (order.Status != OrderStatus.New)
        {
            throw new InvalidOperationException($"Cannot start an order in status {order.Status}");
        }

        order.Status = OrderStatus.InProgress;
        order.StatusChangedAt = DateTime.UtcNow;
        order.StatusChangedBy = userId;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = userId;

        if (order.SamplingRound!.Status == SamplingRoundStatus.Validated)
        {
            order.SamplingRound.Status = SamplingRoundStatus.InProgress;
            order.SamplingRound.UpdatedAt = DateTime.UtcNow;
            order.SamplingRound.UpdatedBy = userId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> UpdateSamplerCommentAsync(
        Guid orderId, SamplerCommentDto dto, string userId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(o => o.SamplingRound)
            .Where(o => o.TenantId == tenantId && o.Id == orderId)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null) return false;

        if (order.SamplingRound?.PreleveurId != userId)
        {
            throw new InvalidOperationException("Only the assigned préleveur can add comments");
        }

        order.SamplerComment = dto.Comment;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = userId;

        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<SamplingRoundDetailDto?> TransmitAllAsync(
        Guid id, string userId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var round = await dbContext.SamplingRounds
            .Include(r => r.Preleveur)
            .Include(r => r.Distributor)
            .Include(r => r.CreatedBy)
            .Include(r => r.Orders)
                .ThenInclude(o => o.SamplingLocation)
                    .ThenInclude(l => l!.Sector)
            .Include(r => r.Orders)
                .ThenInclude(o => o.OriginalSamplingLocation)
            .Include(r => r.Orders)
                .ThenInclude(o => o.OrderAnalysisProfiles)
                    .ThenInclude(oap => oap.AnalysisProfile)
            .FirstOrDefaultAsync(r => r.Id == id && r.TenantId == tenantId, cancellationToken);

        if (round is null) return null;

        var completedOrders = round.Orders.Where(o => o.Status == OrderStatus.Completed).ToList();
        if (completedOrders.Count == 0)
        {
            throw new InvalidOperationException("No completed orders to transmit");
        }

        foreach (var order in completedOrders)
        {
            order.Status = OrderStatus.Transmitted;
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = userId;
        }

        // If all orders are now transmitted or done, mark round as completed
        if (round.Orders.All(o => o.Status == OrderStatus.Transmitted || o.Status == OrderStatus.Done || o.Status == OrderStatus.Cancelled))
        {
            round.Status = SamplingRoundStatus.Completed;
            round.CompletedAt = DateTime.UtcNow;
        }

        round.UpdatedAt = DateTime.UtcNow;
        round.UpdatedBy = userId;

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapToDetailDto(round);
    }

    private static SamplingRoundDetailDto MapToDetailDto(SamplingRound round)
    {
        return new SamplingRoundDetailDto(
            round.Id,
            round.Name,
            round.Description,
            round.Deadline,
            round.Status,
            round.PreleveurId,
            round.Preleveur is not null ? $"{round.Preleveur.FirstName} {round.Preleveur.LastName}" : null,
            round.DistributorId,
            round.Distributor?.Name ?? string.Empty,
            round.Distributor?.ShortName,
            round.Notes,
            round.CreatedById,
            round.CreatedBy is not null ? $"{round.CreatedBy.FirstName} {round.CreatedBy.LastName}" : string.Empty,
            round.CreatedAt,
            round.UpdatedAt,
            round.CompletedAt,
            round.Orders.OrderBy(o => o.SortOrder).Select(o => new SamplingRoundOrderDto(
                o.Id,
                o.OrderNumber,
                o.Status,
                o.SortOrder,
                o.SamplingLocationId,
                o.SamplingLocation?.Name,
                o.SamplingLocation?.LocationCode,
                o.SamplingLocation?.Sector?.Name,
                o.OriginalSamplingLocationId,
                o.OriginalSamplingLocation?.Name,
                o.LocationReplacementReason,
                o.SamplerComment,
                o.Notes,
                o.OrderAnalysisProfiles.Select(oap => oap.AnalysisProfile?.Name ?? string.Empty).ToList()
            )).ToList());
    }
}
