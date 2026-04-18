using AquaPlan.Application.DTOs.AnalysisPrograms;
using AquaPlan.Application.DTOs.AnalysisProfiles;
using AquaPlan.Application.DTOs.Containers;
using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.DTOs.SamplingLocations;
using AquaPlan.Application.DTOs.SamplingRounds;
using AquaPlan.Application.Exceptions;
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
            .Include(sr => sr.LockedBy)
            .Include(sr => sr.Orders.OrderBy(o => o.SortOrder))
                .ThenInclude(o => o.SamplingLocation)
                    .ThenInclude(l => l!.Sector)
            .Include(sr => sr.Orders)
                .ThenInclude(o => o.OriginalSamplingLocation)
            .Include(sr => sr.Orders)
                .ThenInclude(o => o.OrderAnalysisPrograms)
                    .ThenInclude(oap => oap.AnalysisProgram!)
                        .ThenInclude(ap => ap.AnalysisProgramProfiles)
                            .ThenInclude(app => app.AnalysisProfile!)
                                .ThenInclude(profile => profile.Container)
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

            query = query.Where(sr =>
                distributorIds.Contains(sr.DistributorId) ||
                (sr.PreleveurId == userId && sr.Status != SamplingRoundStatus.Draft));
        }

        if (filter.Statuses is { Count: > 0 })
        {
            // AQ-362 — support multi-value statuses=Draft&statuses=Assigned.
            var statuses = filter.Statuses.ToList();
            query = query.Where(sr => statuses.Contains(sr.Status));
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
                sr.CreatedAt,
                sr.IsLocked,
                sr.LockedById,
                sr.LockedBy != null ? sr.LockedBy.FirstName + " " + sr.LockedBy.LastName : null,
                sr.LockedAt))
            .ToListAsync(cancellationToken);

        return new SamplingRoundPagedResultDto(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<SamplingRoundDetailDto?> UpdateAsync(
        Guid id, SamplingRoundUpdateDto dto, string updatedBy, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // AQ-371 — reject updates on locked rounds with a structured 409.
        await EnsureRoundNotLockedForWriteAsync(id, updatedBy, isAdmin: false, tenantId, cancellationToken);

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

    public async Task<SamplingRoundDetailDto?> RevertToDraftAsync(
        Guid id, string userId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var round = await dbContext.SamplingRounds
            .Include(sr => sr.Orders)
            .Where(sr => sr.TenantId == tenantId && sr.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (round is null) return null;

        if (round.Status != SamplingRoundStatus.Assigned)
        {
            throw new InvalidOperationException("Only assigned rounds can be reverted to draft");
        }

        round.Status = SamplingRoundStatus.Draft;
        round.PreleveurId = null;
        round.UpdatedAt = DateTime.UtcNow;
        round.UpdatedBy = userId;

        foreach (var order in round.Orders)
        {
            order.PreleveurId = null;
            order.UpdatedAt = DateTime.UtcNow;
            order.UpdatedBy = userId;
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        return await GetByIdAsync(id, tenantId, cancellationToken);
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
        // AQ-370 — cancelling the round releases any pending lock.
        round.IsLocked = false;
        round.LockedById = null;
        round.LockedAt = null;

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
        // AQ-371 — reject order-management on locked rounds with a structured 409.
        // currentUserId unknown here, so use empty to force lock failure when locked.
        await EnsureRoundNotLockedForWriteAsync(roundId, currentUserId: string.Empty, isAdmin: false, tenantId, cancellationToken);

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
        bool isAdmin = false, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(o => o.SamplingRound)
            .Where(o => o.TenantId == tenantId && o.Id == orderId)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null) return false;

        if (!isAdmin && order.SamplingRound?.PreleveurId != userId)
        {
            throw new InvalidOperationException("Only the assigned préleveur or an administrator can replace a sampling location");
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

        if (order.SamplingRound!.Status == SamplingRoundStatus.Assigned)
        {
            // AQ-370 — starting the first order also locks the round for the préleveur.
            order.SamplingRound.Status = SamplingRoundStatus.InProgress;
            order.SamplingRound.IsLocked = true;
            order.SamplingRound.LockedById = userId;
            order.SamplingRound.LockedAt = DateTime.UtcNow;
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
                .ThenInclude(o => o.OrderAnalysisPrograms)
                    .ThenInclude(oap => oap.AnalysisProgram!)
                        .ThenInclude(ap => ap.AnalysisProgramProfiles)
                            .ThenInclude(app => app.AnalysisProfile!)
                                .ThenInclude(profile => profile.Container)
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
            // AQ-370 — completing the round releases the préleveur's lock.
            round.IsLocked = false;
            round.LockedById = null;
            round.LockedAt = null;
        }

        round.UpdatedAt = DateTime.UtcNow;
        round.UpdatedBy = userId;

        await dbContext.SaveChangesAsync(cancellationToken);
        return MapToDetailDto(round);
    }

    // --- AQ-370 / AQ-372 lock lifecycle ---

    public async Task<SamplingRoundDetailDto?> StartAsync(
        Guid id, string userId, Guid tenantId, bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var round = await dbContext.SamplingRounds
            .Where(sr => sr.TenantId == tenantId && sr.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (round is null) return null;

        if (round.Status != SamplingRoundStatus.Assigned)
        {
            throw new InvalidOperationException($"Cannot start a sampling round in status {round.Status}. Round must be Assigned.");
        }

        if (!isAdmin && round.PreleveurId != userId)
        {
            throw new InvalidOperationException("Only the assigned préleveur can start the round.");
        }

        round.Status = SamplingRoundStatus.InProgress;
        round.IsLocked = true;
        round.LockedById = round.PreleveurId ?? userId;
        round.LockedAt = DateTime.UtcNow;
        round.UpdatedAt = DateTime.UtcNow;
        round.UpdatedBy = userId;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Round {RoundId} started and locked by {LockedById}",
            round.Id, round.LockedById);

        return await GetByIdAsync(id, tenantId, cancellationToken);
    }

    public async Task<SamplingRoundDetailDto?> ForceUnlockAsync(
        Guid id, string adminUserId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var round = await dbContext.SamplingRounds
            .Where(sr => sr.TenantId == tenantId && sr.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (round is null) return null;

        if (!round.IsLocked)
        {
            throw new InvalidOperationException("Sampling round is not locked.");
        }

        var previousLockedById = round.LockedById;

        round.Status = SamplingRoundStatus.Assigned;
        round.IsLocked = false;
        round.LockedById = null;
        round.LockedAt = null;
        round.UpdatedAt = DateTime.UtcNow;
        round.UpdatedBy = adminUserId;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogWarning(
            "Admin {AdminId} force-unlocked round {RoundId} previously locked by {PreviousLockedById}",
            adminUserId, round.Id, previousLockedById);

        return await GetByIdAsync(id, tenantId, cancellationToken);
    }

    public async Task EnsureRoundNotLockedForWriteAsync(
        Guid? roundId, string currentUserId, bool isAdmin, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        if (roundId is null || roundId == Guid.Empty)
        {
            return;
        }

        var round = await dbContext.SamplingRounds
            .Include(sr => sr.LockedBy)
            .Where(sr => sr.TenantId == tenantId && sr.Id == roundId.Value)
            .FirstOrDefaultAsync(cancellationToken);

        if (round is null || !round.IsLocked)
        {
            return;
        }

        // The lock holder (and only them) can write. Admins are also blocked to keep
        // the data consistent with the offline préleveur session (AQ-371 Option B).
        if (round.LockedById == currentUserId)
        {
            return;
        }

        // Admins are explicitly blocked from standard write operations here; they must
        // go through the dedicated force-unlock endpoint (AQ-372).
        _ = isAdmin;

        var lockedByName = round.LockedBy is not null
            ? $"{round.LockedBy.FirstName} {round.LockedBy.LastName}"
            : null;

        throw new RoundLockedException(round.Id, round.LockedById, lockedByName, round.LockedAt);
    }

    // --- AQ-373 offline snapshot ---

    public async Task<OfflineSnapshotDto?> GetOfflineSnapshotAsync(
        Guid roundId, string currentUserId, bool isAdmin, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        // Load the round first (minimal projection) so we can authorize before
        // running the more expensive joins.
        var authRound = await dbContext.SamplingRounds
            .AsNoTracking()
            .Where(sr => sr.TenantId == tenantId && sr.Id == roundId)
            .Select(sr => new { sr.Id, sr.PreleveurId, sr.DistributorId })
            .FirstOrDefaultAsync(cancellationToken);

        if (authRound is null)
        {
            return null;
        }

        if (!isAdmin && authRound.PreleveurId != currentUserId)
        {
            throw new UnauthorizedAccessException(
                "Only the assigned préleveur or an administrator can download a round snapshot.");
        }

        // Round detail — reuse existing loader that already includes all the
        // graphs we need (orders, locations, programs, profiles, containers,
        // locked-by).
        var roundDto = await GetByIdAsync(roundId, tenantId, cancellationToken);
        if (roundDto is null)
        {
            return null;
        }

        // Orders with their full graph for offline rendering.
        var orders = await dbContext.Orders
            .AsNoTracking()
            .Include(o => o.CreatedBy)
            .Include(o => o.Preleveur)
            .Include(o => o.Distributor)
            .Include(o => o.SamplingLocation)
                .ThenInclude(sl => sl!.Sector)
            .Include(o => o.OrderAnalysisPrograms)
                .ThenInclude(oap => oap.AnalysisProgram)
            .Include(o => o.Sampling)
                .ThenInclude(s => s!.Preleveur)
            .Include(o => o.Sampling)
                .ThenInclude(s => s!.Containers)
            .Include(o => o.SamplingRound)
                .ThenInclude(r => r!.LockedBy)
            .Where(o => o.TenantId == tenantId && o.SamplingRoundId == roundId)
            .OrderBy(o => o.SortOrder)
            .ToListAsync(cancellationToken);

        var orderDtos = orders.Select(MapOrderToDetailDto).ToList();

        // Distributor's active + validated sampling locations for on-field
        // replacement. Include distributor/sector for display.
        var samplingLocations = await dbContext.SamplingLocations
            .AsNoTracking()
            .Include(sl => sl.Distributor)
            .Include(sl => sl.Sector)
            .Where(sl => sl.DistributorId == authRound.DistributorId
                && sl.IsActive
                && sl.IsValidated
                && sl.Distributor!.TenantId == tenantId)
            .OrderBy(sl => sl.Name)
            .ToListAsync(cancellationToken);

        var locationDtos = samplingLocations.Select(sl => new SamplingLocationDto(
            sl.Id, sl.Name, sl.LocationCode,
            sl.Description, sl.Address, sl.AccessDescription,
            sl.IsActive, sl.IsValidated, sl.DistributorId,
            sl.Distributor?.Name,
            sl.SectorId,
            sl.Sector?.Name,
            sl.CreatedAt)).ToList();

        // Only the analysis programs referenced by the round's orders (KISS —
        // limits payload size even on large tenants). Dedup by Id.
        var programIds = orders
            .SelectMany(o => o.OrderAnalysisPrograms)
            .Select(oap => oap.AnalysisProgramId)
            .Distinct()
            .ToList();

        var programs = await dbContext.AnalysisPrograms
            .AsNoTracking()
            .Include(p => p.AnalysisProgramProfiles)
                .ThenInclude(pp => pp.AnalysisProfile!)
                    .ThenInclude(profile => profile.Container)
            .Where(p => p.TenantId == tenantId && programIds.Contains(p.Id))
            .OrderBy(p => p.Code)
            .ToListAsync(cancellationToken);

        var programDtos = programs.Select(MapAnalysisProgramToDto).ToList();

        // Distinct profiles referenced by those programs.
        var profiles = programs
            .SelectMany(p => p.AnalysisProgramProfiles)
            .Where(pp => pp.AnalysisProfile is not null)
            .Select(pp => pp.AnalysisProfile!)
            .GroupBy(p => p.Id)
            .Select(g => g.First())
            .OrderBy(p => p.Code)
            .ToList();

        var profileDtos = profiles.Select(p => new AnalysisProfileDto(
            p.Id, p.Code, p.Name, p.Description,
            p.Category, p.IsActive,
            p.ContainerId,
            p.Container?.Code ?? string.Empty,
            p.Container?.Name ?? string.Empty,
            p.CreatedAt)).ToList();

        // Distinct containers referenced by those profiles.
        var containers = profiles
            .Where(p => p.Container is not null)
            .Select(p => p.Container!)
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .OrderBy(c => c.Code)
            .ToList();

        var containerDtos = containers.Select(c => new ContainerDto(
            c.Id, c.Code, c.Name, c.Material, c.VolumeMl, c.Color,
            c.IsActive, c.CreatedAt, c.UpdatedAt)).ToList();

        logger.LogInformation(
            "Offline snapshot built for round {RoundId} — {OrderCount} orders, {LocationCount} locations, {ProgramCount} programs",
            roundId, orderDtos.Count, locationDtos.Count, programDtos.Count);

        return new OfflineSnapshotDto(
            roundDto,
            orderDtos,
            locationDtos,
            programDtos,
            profileDtos,
            containerDtos,
            DateTime.UtcNow);
    }

    private static OrderDetailDto MapOrderToDetailDto(Order order)
    {
        SamplingDto? samplingDto = null;
        if (order.Sampling is not null)
        {
            var s = order.Sampling;
            var sContainers = s.Containers
                .Select(c => new SamplingContainerDto(c.Id, c.ContainerId, s.SampleBarcode, c.BarcodeScannedAt))
                .ToList();
            samplingDto = new SamplingDto(
                s.Id, s.OrderId, s.PreleveurId,
                s.Preleveur is not null ? s.Preleveur.FirstName + " " + s.Preleveur.LastName : null,
                s.SamplingDateTime, s.Temperature, s.Weather,
                s.Notes, s.HasWaterSoftener, s.IsChlorinated,
                s.SampleBarcode, s.BarcodeScannedAt,
                s.IsValidated, s.ValidatedAt, s.CreatedAt,
                sContainers);
        }

        var programs = order.OrderAnalysisPrograms
            .Where(oap => oap.AnalysisProgram is not null)
            .Select(oap => new OrderAnalysisProgramDto(
                oap.AnalysisProgramId,
                oap.AnalysisProgram!.Code,
                oap.AnalysisProgram.Name))
            .ToList();

        var round = order.SamplingRound;
        var isRoundLocked = round is not null && round.IsLocked;
        var lockedByName = round?.LockedBy is not null
            ? $"{round.LockedBy.FirstName} {round.LockedBy.LastName}"
            : null;

        return new OrderDetailDto(
            order.Id, order.OrderNumber, order.Status, order.IsUnplanned,
            order.UnplannedReason, order.UnplannedReasonDetails,
            order.CreatedById,
            order.CreatedBy is not null ? order.CreatedBy.FirstName + " " + order.CreatedBy.LastName : null,
            order.PreleveurId,
            order.Preleveur is not null ? order.Preleveur.FirstName + " " + order.Preleveur.LastName : null,
            order.DistributorId,
            order.Distributor is not null ? order.Distributor.Name : string.Empty,
            order.SamplingLocationId,
            order.SamplingLocation is not null ? order.SamplingLocation.Name : null,
            order.SamplingLocation?.Sector?.Name,
            order.PlannedDate,
            order.Notes,
            order.IsDelegated,
            programs,
            order.TenantId, order.CreatedAt, order.UpdatedAt, samplingDto,
            order.SamplingRoundId,
            isRoundLocked,
            round?.LockedById,
            lockedByName);
    }

    private static AnalysisProgramDto MapAnalysisProgramToDto(AnalysisProgram program)
    {
        var profiles = program.AnalysisProgramProfiles
            .Where(pp => pp.AnalysisProfile is not null)
            .Select(pp => new AnalysisProfileListDto(
                pp.AnalysisProfile!.Id,
                pp.AnalysisProfile.Code,
                pp.AnalysisProfile.Name,
                pp.AnalysisProfile.Category,
                pp.AnalysisProfile.IsActive,
                pp.AnalysisProfile.ContainerId,
                pp.AnalysisProfile.Container?.Code ?? string.Empty))
            .OrderBy(p => p.Code)
            .ToList();

        var requiredContainers = program.AnalysisProgramProfiles
            .Where(pp => pp.AnalysisProfile?.Container is not null)
            .GroupBy(pp => pp.AnalysisProfile!.ContainerId)
            .Select(g =>
            {
                var container = g.First().AnalysisProfile!.Container!;
                return new ProgramContainerDto(
                    container.Id, container.Code, container.Name,
                    container.Material, container.VolumeMl, g.Count());
            })
            .OrderBy(c => c.Code)
            .ToList();

        return new AnalysisProgramDto(
            program.Id, program.Code, program.Name, program.Description,
            program.IsActive, program.CreatedAt,
            profiles, requiredContainers);
    }

    private static SamplingRoundDetailDto MapToDetailDto(SamplingRound round)
    {
        var containerSummary = BuildContainerSummary(round);

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
                o.OrderAnalysisPrograms.Select(oap => oap.AnalysisProgram?.Name ?? string.Empty).ToList()
            )).ToList(),
            containerSummary,
            round.IsLocked,
            round.LockedById,
            round.LockedBy is not null ? $"{round.LockedBy.FirstName} {round.LockedBy.LastName}" : null,
            round.LockedAt);
    }

    private static IList<RoundContainerSummaryDto> BuildContainerSummary(SamplingRound round)
    {
        var perOrderContainers = round.Orders
            .Where(o => o.Status != OrderStatus.Cancelled)
            .SelectMany(o => o.OrderAnalysisPrograms
                .Where(oap => oap.AnalysisProgram is not null)
                .SelectMany(oap => oap.AnalysisProgram!.AnalysisProgramProfiles)
                .Where(app => app.AnalysisProfile?.Container is not null)
                .Select(app => app.AnalysisProfile!.Container!)
                .GroupBy(c => c.Id)
                .Select(g => g.First()));

        return perOrderContainers
            .GroupBy(c => c.Id)
            .Select(g =>
            {
                var first = g.First();
                return new RoundContainerSummaryDto(
                    first.Id,
                    first.Code,
                    first.Name,
                    first.Material,
                    first.VolumeMl,
                    g.Count());
            })
            .OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }
}
