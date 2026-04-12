using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

internal class OrderService(
    AquaPlanDbContext dbContext,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<OrderPagedResultDto> GetOrdersFilteredAsync(
        string userId, Guid tenantId, OrderFilterDto filter, bool isAdmin,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Orders
            .Where(o => o.TenantId == tenantId)
            .AsQueryable();

        // Non-admin: filter by user's accessible distributors (own + delegated) + own orders
        if (!isAdmin)
        {
            var userDistributorIds = await dbContext.UserDistributors
                .Where(ud => ud.UserId == userId)
                .Select(ud => ud.DistributorId)
                .ToListAsync(cancellationToken);

            // Include distributors that have delegated to user's distributors
            var delegatedIds = await dbContext.DistributorDelegations
                .Where(d => d.IsActive
                    && userDistributorIds.Contains(d.DelegatedToDistributorId)
                    && d.ValidFrom <= DateTime.UtcNow
                    && (d.ValidTo == null || d.ValidTo >= DateTime.UtcNow))
                .Select(d => d.DelegatingDistributorId)
                .Distinct()
                .ToListAsync(cancellationToken);

            var allAccessibleIds = userDistributorIds.Union(delegatedIds).ToList();

            query = query.Where(o =>
                allAccessibleIds.Contains(o.DistributorId)
                && (o.CreatedById == userId || o.PreleveurId == userId));
        }

        // Filter by statuses
        if (filter.Statuses is { Count: > 0 })
        {
            query = query.Where(o => filter.Statuses.Contains(o.Status));
        }

        // Filter unassigned
        if (filter.IsUnassigned == true)
        {
            query = query.Where(o => o.PreleveurId == null);
        }

        // Filter by distributor (admin filter)
        if (filter.DistributorId.HasValue)
        {
            query = query.Where(o => o.DistributorId == filter.DistributorId.Value);
        }

        // Filter by préleveur (admin filter)
        if (!string.IsNullOrWhiteSpace(filter.PreleveurId))
        {
            query = query.Where(o => o.PreleveurId == filter.PreleveurId);
        }

        // Filter by date range (ensure UTC kind for PostgreSQL compatibility)
        if (filter.DateFrom.HasValue)
        {
            var dateFrom = DateTime.SpecifyKind(filter.DateFrom.Value, DateTimeKind.Utc);
            query = query.Where(o => o.PlannedDate >= dateFrom);
        }
        if (filter.DateTo.HasValue)
        {
            var dateTo = DateTime.SpecifyKind(filter.DateTo.Value, DateTimeKind.Utc);
            query = query.Where(o => o.PlannedDate <= dateTo);
        }

        // Search (order number, distributor name, LDP name, préleveur name)
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.ToLower();
            query = query.Where(o =>
                o.OrderNumber.ToLower().Contains(search)
                || (o.Distributor != null && o.Distributor.Name.ToLower().Contains(search))
                || (o.SamplingLocation != null && o.SamplingLocation.Name.ToLower().Contains(search))
                || (o.Preleveur != null && (o.Preleveur.FirstName + " " + o.Preleveur.LastName).ToLower().Contains(search)));
        }

        var totalCount = await query.CountAsync(cancellationToken);

        // Sorting
        query = filter.SortBy?.ToLower() switch
        {
            "ordernumber" => filter.SortDescending ? query.OrderByDescending(o => o.OrderNumber) : query.OrderBy(o => o.OrderNumber),
            "status" => filter.SortDescending ? query.OrderByDescending(o => o.Status) : query.OrderBy(o => o.Status),
            "planneddate" => filter.SortDescending ? query.OrderByDescending(o => o.PlannedDate) : query.OrderBy(o => o.PlannedDate),
            "distributor" => filter.SortDescending ? query.OrderByDescending(o => o.Distributor!.Name) : query.OrderBy(o => o.Distributor!.Name),
            _ => filter.SortDescending ? query.OrderByDescending(o => o.CreatedAt) : query.OrderBy(o => o.CreatedAt),
        };

        // Pagination
        var items = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Include(o => o.CreatedBy)
            .Include(o => o.Preleveur)
            .Include(o => o.Distributor)
            .Include(o => o.SamplingLocation)
            .Select(o => new OrderListDto(
                o.Id, o.OrderNumber, o.Status, o.IsUnplanned, o.UnplannedReason,
                o.CreatedById,
                o.CreatedBy != null ? o.CreatedBy.FirstName + " " + o.CreatedBy.LastName : null,
                o.PreleveurId,
                o.Preleveur != null ? o.Preleveur.FirstName + " " + o.Preleveur.LastName : null,
                o.DistributorId,
                o.Distributor != null ? o.Distributor.Name : string.Empty,
                o.SamplingLocationId,
                o.SamplingLocation != null ? o.SamplingLocation.Name : null,
                o.PlannedDate,
                o.IsDelegated,
                o.CreatedAt))
            .ToListAsync(cancellationToken);

        return new OrderPagedResultDto(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<OrderDetailDto?> GetOrderByIdAsync(Guid orderId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(o => o.CreatedBy)
            .Include(o => o.Preleveur)
            .Include(o => o.Distributor)
            .Include(o => o.SamplingLocation)
            .Include(o => o.OrderAnalysisProfiles)
                .ThenInclude(oap => oap.AnalysisProfile)
            .Include(o => o.Sampling)
                .ThenInclude(s => s!.Preleveur)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        return MapToDetailDto(order);
    }

    public async Task<OrderDetailDto> CreateOrderAsync(OrderCreateDto dto, string createdById, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var orderNumber = await GenerateOrderNumberAsync(cancellationToken);

        // Validation: unplanned orders must have a reason
        if (dto.IsUnplanned && dto.UnplannedReason is null)
        {
            throw new InvalidOperationException("An unplanned order must have an UnplannedReason.");
        }

        var initialStatus = dto.IsUnplanned ? OrderStatus.InProgress : OrderStatus.Draft;

        // If a préleveur is assigned at creation and it's not unplanned, auto-transition to Assigned
        if (!dto.IsUnplanned && !string.IsNullOrEmpty(dto.PreleveurId))
        {
            initialStatus = OrderStatus.Assigned;
        }

        // Check if the order's distributor is a delegated one (not user's own)
        var userOwnDistributorIds = await dbContext.UserDistributors
            .Where(ud => ud.UserId == createdById)
            .Select(ud => ud.DistributorId)
            .ToListAsync(cancellationToken);
        var isDelegated = !userOwnDistributorIds.Contains(dto.DistributorId);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            Status = initialStatus,
            IsUnplanned = dto.IsUnplanned,
            UnplannedReason = dto.IsUnplanned ? dto.UnplannedReason : null,
            UnplannedReasonDetails = dto.IsUnplanned ? dto.UnplannedReasonDetails : null,
            IsDelegated = isDelegated,
            CreatedById = createdById,
            PreleveurId = dto.PreleveurId,
            DistributorId = dto.DistributorId,
            SamplingLocationId = dto.SamplingLocationId,
            PlannedDate = dto.PlannedDate,
            Notes = dto.Notes,
            TenantId = tenantId,
            StatusChangedAt = DateTime.UtcNow,
            StatusChangedBy = createdById,
        };

        dbContext.Orders.Add(order);

        // Add analysis profiles
        if (dto.AnalysisProfileIds is { Count: > 0 })
        {
            foreach (var profileId in dto.AnalysisProfileIds)
            {
                dbContext.OrderAnalysisProfiles.Add(new OrderAnalysisProfile
                {
                    OrderId = order.Id,
                    AnalysisProfileId = profileId,
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Order {OrderNumber} created by {CreatedBy}", orderNumber, createdById);

        return (await GetOrderByIdAsync(order.Id, tenantId, cancellationToken))!;
    }

    public async Task<OrderDetailDto?> UpdateOrderAsync(Guid orderId, OrderUpdateDto dto, string updatedBy, Guid tenantId, bool isAdmin = false, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(o => o.OrderAnalysisProfiles)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        // Business rule: admin can edit non-terminal orders; regular users only Draft/Assigned
        var terminalStatuses = new[] { OrderStatus.Completed, OrderStatus.Cancelled };
        if (isAdmin)
        {
            if (terminalStatuses.Contains(order.Status))
            {
                throw new InvalidOperationException($"Cannot modify order in terminal status {order.Status}.");
            }
        }
        else if (order.Status != OrderStatus.Draft && order.Status != OrderStatus.Assigned)
        {
            throw new InvalidOperationException($"Cannot modify order in status {order.Status}. Only Draft and Assigned orders can be modified.");
        }

        order.SamplingLocationId = dto.SamplingLocationId;
        order.PlannedDate = dto.PlannedDate;
        order.Notes = dto.Notes;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = updatedBy;

        // Handle préleveur assignment change
        if (dto.PreleveurId != order.PreleveurId)
        {
            order.PreleveurId = dto.PreleveurId;
            if (!string.IsNullOrEmpty(dto.PreleveurId) && order.Status == OrderStatus.Draft)
            {
                order.Status = OrderStatus.Assigned;
                order.StatusChangedAt = DateTime.UtcNow;
                order.StatusChangedBy = updatedBy;
            }
            else if (string.IsNullOrEmpty(dto.PreleveurId) && order.Status == OrderStatus.Assigned)
            {
                order.Status = OrderStatus.Draft;
                order.StatusChangedAt = DateTime.UtcNow;
                order.StatusChangedBy = updatedBy;
            }
        }

        // Replace analysis profiles
        if (dto.AnalysisProfileIds is not null)
        {
            dbContext.OrderAnalysisProfiles.RemoveRange(order.OrderAnalysisProfiles);
            foreach (var profileId in dto.AnalysisProfileIds)
            {
                dbContext.OrderAnalysisProfiles.Add(new OrderAnalysisProfile
                {
                    OrderId = orderId,
                    AnalysisProfileId = profileId,
                });
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Order {OrderId} updated by {UpdatedBy}", orderId, updatedBy);

        return await GetOrderByIdAsync(orderId, tenantId, cancellationToken);
    }

    public async Task<bool> DeleteOrderAsync(Guid orderId, string deletedBy, Guid tenantId, bool isAdmin = false, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId, cancellationToken);

        if (order is null)
        {
            return false;
        }

        // Business rule: admin can delete non-treated orders (< SamplingCompleted); regular users only Draft/Assigned
        if (isAdmin)
        {
            if (order.Status >= OrderStatus.SamplingCompleted)
            {
                throw new InvalidOperationException($"Cannot delete order in status {order.Status}. Admin can only delete orders before sampling is completed.");
            }
        }
        else if (order.Status != OrderStatus.Draft && order.Status != OrderStatus.Assigned)
        {
            throw new InvalidOperationException($"Cannot delete order in status {order.Status}. Only Draft and Assigned orders can be deleted.");
        }

        dbContext.Orders.Remove(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Order {OrderId} deleted by {DeletedBy}", orderId, deletedBy);

        return true;
    }

    public async Task<OrderDetailDto?> AssignPreleveurAsync(Guid orderId, OrderAssignDto dto, string updatedBy, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        order.PreleveurId = dto.PreleveurId;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = updatedBy;

        // Auto-transition Draft → Assigned when préleveur is assigned
        if (order.Status == OrderStatus.Draft && !string.IsNullOrEmpty(dto.PreleveurId))
        {
            order.Status = OrderStatus.Assigned;
            order.StatusChangedAt = DateTime.UtcNow;
            order.StatusChangedBy = updatedBy;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Order {OrderId} assigned to préleveur {PreleveurId}", orderId, dto.PreleveurId);

        return await GetOrderByIdAsync(orderId, tenantId, cancellationToken);
    }

    public async Task<byte[]> ExportOrdersCsvAsync(Guid tenantId, OrderFilterDto filter, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Orders
            .Where(o => o.TenantId == tenantId)
            .Include(o => o.CreatedBy)
            .Include(o => o.Preleveur)
            .Include(o => o.Distributor)
            .Include(o => o.SamplingLocation)
            .AsQueryable();

        // Apply same filters as GetOrdersFilteredAsync
        if (filter.Statuses is { Count: > 0 })
        {
            query = query.Where(o => filter.Statuses.Contains(o.Status));
        }
        if (filter.DistributorId.HasValue)
        {
            query = query.Where(o => o.DistributorId == filter.DistributorId.Value);
        }
        if (!string.IsNullOrWhiteSpace(filter.PreleveurId))
        {
            query = query.Where(o => o.PreleveurId == filter.PreleveurId);
        }
        if (filter.DateFrom.HasValue)
        {
            var dateFrom = DateTime.SpecifyKind(filter.DateFrom.Value, DateTimeKind.Utc);
            query = query.Where(o => o.PlannedDate >= dateFrom);
        }
        if (filter.DateTo.HasValue)
        {
            var dateTo = DateTime.SpecifyKind(filter.DateTo.Value, DateTimeKind.Utc);
            query = query.Where(o => o.PlannedDate <= dateTo);
        }
        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.ToLower();
            query = query.Where(o =>
                o.OrderNumber.ToLower().Contains(search)
                || (o.Distributor != null && o.Distributor.Name.ToLower().Contains(search)));
        }

        var orders = await query
            .OrderByDescending(o => o.CreatedAt)
            .ToListAsync(cancellationToken);

        using var ms = new MemoryStream();
        using var writer = new StreamWriter(ms, System.Text.Encoding.UTF8);

        // Header
        await writer.WriteLineAsync("OrderNumber;Status;IsUnplanned;UnplannedReason;Distributor;SamplingLocation;Preleveur;PlannedDate;CreatedBy;CreatedAt");

        // Rows
        foreach (var o in orders)
        {
            var preleveurName = o.Preleveur is not null ? $"{o.Preleveur.FirstName} {o.Preleveur.LastName}" : "";
            var createdByName = o.CreatedBy is not null ? $"{o.CreatedBy.FirstName} {o.CreatedBy.LastName}" : "";
            var line = string.Join(";",
                o.OrderNumber,
                o.Status,
                o.IsUnplanned ? "Yes" : "No",
                o.UnplannedReason?.ToString() ?? "",
                o.Distributor?.Name ?? "",
                o.SamplingLocation?.Name ?? "",
                preleveurName,
                o.PlannedDate?.ToString("yyyy-MM-dd") ?? "",
                createdByName,
                o.CreatedAt.ToString("yyyy-MM-dd HH:mm"));
            await writer.WriteLineAsync(line);
        }

        await writer.FlushAsync(cancellationToken);
        return ms.ToArray();
    }

    public async Task<bool> UserHasDistributorAccessAsync(string userId, Guid distributorId, CancellationToken cancellationToken = default)
    {
        return await dbContext.UserDistributors
            .AnyAsync(ud => ud.UserId == userId && ud.DistributorId == distributorId, cancellationToken);
    }

    public async Task<bool> UserCanAccessOrderAsync(string userId, Guid orderId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId, cancellationToken);

        if (order is null)
        {
            return false;
        }

        if (order.CreatedById == userId || order.PreleveurId == userId)
        {
            return true;
        }

        if (await UserHasDistributorAccessAsync(userId, order.DistributorId, cancellationToken))
        {
            return true;
        }

        // Check delegation: does user belong to a distributor that has been delegated access?
        var userDistributorIds = await dbContext.UserDistributors
            .Where(ud => ud.UserId == userId)
            .Select(ud => ud.DistributorId)
            .ToListAsync(cancellationToken);

        return await dbContext.DistributorDelegations
            .AnyAsync(d => d.IsActive
                && d.DelegatingDistributorId == order.DistributorId
                && userDistributorIds.Contains(d.DelegatedToDistributorId)
                && d.ValidFrom <= DateTime.UtcNow
                && (d.ValidTo == null || d.ValidTo >= DateTime.UtcNow), cancellationToken);
    }

    private static OrderDetailDto MapToDetailDto(Order order)
    {
        SamplingDto? samplingDto = null;
        if (order.Sampling is not null)
        {
            var s = order.Sampling;
            samplingDto = new SamplingDto(
                s.Id, s.OrderId, s.PreleveurId,
                s.Preleveur is not null ? s.Preleveur.FirstName + " " + s.Preleveur.LastName : null,
                s.SamplingDateTime, s.Temperature, s.Weather,
                s.LocationLat, s.LocationLng, s.Notes,
                s.SampleBarcode, s.BarcodeScannedAt,
                s.IsValidated, s.ValidatedAt, s.CreatedAt);
        }

        var analysisProfiles = order.OrderAnalysisProfiles
            .Where(oap => oap.AnalysisProfile is not null)
            .Select(oap => new OrderAnalysisProfileDto(
                oap.AnalysisProfileId,
                oap.AnalysisProfile!.Code,
                oap.AnalysisProfile.Name))
            .ToList();

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
            order.PlannedDate,
            order.Notes,
            order.IsDelegated,
            analysisProfiles,
            order.TenantId, order.CreatedAt, order.UpdatedAt, samplingDto);
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow;
        var prefix = $"ORD-{today:yyyyMMdd}";
        var count = await dbContext.Orders
            .CountAsync(o => o.OrderNumber.StartsWith(prefix), cancellationToken);
        return $"{prefix}-{(count + 1):D4}";
    }
}
