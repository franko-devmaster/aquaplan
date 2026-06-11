using AquaPlan.Application.DTOs.MockLims;
using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.Exceptions;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AquaPlan.Infrastructure.Services;

internal class OrderService(
    AquaPlanDbContext dbContext,
    IOrderAuditService auditService,
    ISamplingRoundService samplingRoundService,
    IMockLimsService mockLimsService,
    INotificationService notificationService,
    IDelegationService delegationService,
    IOptions<MockLimsOptions> mockLimsOptions,
    ILogger<OrderService> logger) : IOrderService
{
    public async Task<OrderPagedResultDto> GetOrdersFilteredAsync(
        string userId, Guid tenantId, OrderFilterDto filter, bool isAdmin, bool isPreleveurOnly,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.Orders
            .Where(o => o.TenantId == tenantId)
            .AsQueryable();

        // AQ-420 — Role-based visibility (mirrors the AQ-398 rule applied to rounds):
        //   • Admin: full tenant view (no extra filter).
        //   • Préleveur (sole role): only orders where they are the assigned préleveur.
        //   • Mandataire (Requérant / Requérant-Préleveur): every order of the
        //     distributors they are authorized on (own + active delegations),
        //     regardless of who created the order.
        if (!isAdmin)
        {
            if (isPreleveurOnly)
            {
                query = query.Where(o => o.PreleveurId == userId);
            }
            else
            {
                var authorizedDistributorIds = await delegationService
                    .GetAuthorizedDistributorIdsForUserAsync(userId, cancellationToken);
                query = query.Where(o => authorizedDistributorIds.Contains(o.DistributorId));
            }
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

        if (filter.HasNoRound == true)
        {
            query = query.Where(o => o.SamplingRoundId == null);
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

        // AQ-31 — filter by aggregated results conformity status.
        if (filter.ResultsStatus.HasValue)
        {
            query = filter.ResultsStatus.Value switch
            {
                ResultsStatus.Conform => query.Where(o =>
                    o.Status == OrderStatus.Done
                    && dbContext.SamplingResults.Any(r => r.OrderId == o.Id)
                    && !dbContext.SamplingResults.Any(r => r.OrderId == o.Id && !r.IsConform)),
                ResultsStatus.NonConform => query.Where(o =>
                    o.Status == OrderStatus.Done
                    && dbContext.SamplingResults.Any(r => r.OrderId == o.Id && !r.IsConform)),
                _ => query.Where(o =>
                    o.Status != OrderStatus.Done
                    || !dbContext.SamplingResults.Any(r => r.OrderId == o.Id)),
            };
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

        // Pagination — project with aggregated counters so we can derive ResultsStatus below.
        var rows = await query
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Include(o => o.CreatedBy)
            .Include(o => o.Preleveur)
            .Include(o => o.Distributor)
            .Include(o => o.SamplingLocation)
            .Select(o => new
            {
                o.Id,
                o.OrderNumber,
                o.Status,
                o.IsUnplanned,
                o.UnplannedReason,
                o.CreatedById,
                CreatedByName = o.CreatedBy != null ? o.CreatedBy.FirstName + " " + o.CreatedBy.LastName : null,
                o.PreleveurId,
                PreleveurName = o.Preleveur != null ? o.Preleveur.FirstName + " " + o.Preleveur.LastName : null,
                o.DistributorId,
                DistributorName = o.Distributor != null ? o.Distributor.Name : string.Empty,
                o.SamplingLocationId,
                SamplingLocationName = o.SamplingLocation != null ? o.SamplingLocation.Name : null,
                o.PlannedDate,
                o.IsDelegated,
                o.CreatedAt,
                TotalResults = dbContext.SamplingResults.Count(r => r.OrderId == o.Id),
                NonConformResults = dbContext.SamplingResults.Count(r => r.OrderId == o.Id && !r.IsConform),
            })
            .ToListAsync(cancellationToken);

        var items = rows
            .Select(r => new OrderListDto(
                r.Id, r.OrderNumber, r.Status, r.IsUnplanned, r.UnplannedReason,
                r.CreatedById, r.CreatedByName,
                r.PreleveurId, r.PreleveurName,
                r.DistributorId, r.DistributorName,
                r.SamplingLocationId, r.SamplingLocationName,
                r.PlannedDate, r.IsDelegated, r.CreatedAt,
                DeriveResultsStatus(r.Status, r.TotalResults, r.NonConformResults)))
            .ToList();

        return new OrderPagedResultDto(items, totalCount, filter.Page, filter.PageSize);
    }

    /// <summary>
    /// AQ-31 — Aggregates an order's conformity status from its sampling results.
    /// </summary>
    private static ResultsStatus DeriveResultsStatus(OrderStatus status, int totalResults, int nonConformResults)
    {
        if (status != OrderStatus.Done || totalResults == 0)
        {
            return ResultsStatus.NotReceived;
        }
        return nonConformResults == 0 ? ResultsStatus.Conform : ResultsStatus.NonConform;
    }

    public async Task<OrderDetailDto?> GetOrderByIdAsync(Guid orderId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
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
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        // AQ-31 — aggregate conformity counters for the detail view.
        var totalResults = await dbContext.SamplingResults.CountAsync(r => r.OrderId == orderId, cancellationToken);
        var nonConformResults = await dbContext.SamplingResults.CountAsync(r => r.OrderId == orderId && !r.IsConform, cancellationToken);

        return MapToDetailDto(order, DeriveResultsStatus(order.Status, totalResults, nonConformResults));
    }

    public async Task<OrderDetailDto> CreateOrderAsync(OrderCreateDto dto, string createdById, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var orderNumber = await GenerateOrderNumberAsync(cancellationToken);

        // Validation: unplanned orders must have a reason
        if (dto.IsUnplanned && dto.UnplannedReason is null)
        {
            throw new InvalidOperationException("An unplanned order must have an UnplannedReason.");
        }

        var initialStatus = dto.IsUnplanned ? OrderStatus.InProgress : OrderStatus.New;

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
            PlannedDate = dto.PlannedDate.HasValue
                ? DateTime.SpecifyKind(dto.PlannedDate.Value, DateTimeKind.Utc)
                : null,
            Notes = dto.Notes,
            TenantId = tenantId,
            StatusChangedAt = DateTime.UtcNow,
            StatusChangedBy = createdById,
        };

        dbContext.Orders.Add(order);

        // Add analysis programs
        if (dto.AnalysisProgramIds is { Count: > 0 })
        {
            foreach (var programId in dto.AnalysisProgramIds)
            {
                dbContext.OrderAnalysisPrograms.Add(new OrderAnalysisProgram
                {
                    OrderId = order.Id,
                    AnalysisProgramId = programId,
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
            .Include(o => o.OrderAnalysisPrograms)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        // AQ-371 — reject writes when the parent round is locked by someone else.
        await samplingRoundService.EnsureRoundNotLockedForWriteAsync(order.SamplingRoundId, updatedBy, isAdmin, tenantId, cancellationToken);

        // Business rule: admin can edit non-terminal orders; regular users only New
        var terminalStatuses = new[] { OrderStatus.Done, OrderStatus.Cancelled };
        if (isAdmin)
        {
            if (terminalStatuses.Contains(order.Status))
            {
                throw new InvalidOperationException($"Cannot modify order in terminal status {order.Status}.");
            }
        }
        else if (order.Status != OrderStatus.New)
        {
            throw new InvalidOperationException($"Cannot modify order in status {order.Status}. Only New orders can be modified.");
        }

        order.SamplingLocationId = dto.SamplingLocationId;
        order.PlannedDate = dto.PlannedDate.HasValue
            ? DateTime.SpecifyKind(dto.PlannedDate.Value, DateTimeKind.Utc)
            : null;
        order.Notes = dto.Notes;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = updatedBy;

        // Handle préleveur assignment change
        if (dto.PreleveurId != order.PreleveurId)
        {
            order.PreleveurId = dto.PreleveurId;
        }

        // Replace analysis programs
        if (dto.AnalysisProgramIds is not null)
        {
            dbContext.OrderAnalysisPrograms.RemoveRange(order.OrderAnalysisPrograms);
            foreach (var programId in dto.AnalysisProgramIds)
            {
                dbContext.OrderAnalysisPrograms.Add(new OrderAnalysisProgram
                {
                    OrderId = orderId,
                    AnalysisProgramId = programId,
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

        // AQ-371 — reject writes when the parent round is locked by someone else.
        await samplingRoundService.EnsureRoundNotLockedForWriteAsync(order.SamplingRoundId, deletedBy, isAdmin, tenantId, cancellationToken);

        // Business rule: admin can delete non-completed orders (< Completed); regular users only New
        if (isAdmin)
        {
            if (order.Status >= OrderStatus.Completed)
            {
                throw new InvalidOperationException($"Cannot delete order in status {order.Status}. Admin can only delete orders before sampling is completed.");
            }
        }
        else if (order.Status != OrderStatus.New)
        {
            throw new InvalidOperationException($"Cannot delete order in status {order.Status}. Only New orders can be deleted.");
        }

        dbContext.Orders.Remove(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Order {OrderId} deleted by {DeletedBy}", orderId, deletedBy);

        return true;
    }

    public async Task<OrderDetailDto?> AssignPreleveurAsync(Guid orderId, OrderAssignDto dto, string updatedBy, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(o => o.SamplingLocation)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        var previousPreleveurId = order.PreleveurId;
        order.PreleveurId = dto.PreleveurId;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = updatedBy;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Order {OrderId} assigned to préleveur {PreleveurId}", orderId, dto.PreleveurId);

        // AQ-43 — notify the newly assigned préleveur (only when the assignee actually changed).
        if (!string.IsNullOrWhiteSpace(dto.PreleveurId) && dto.PreleveurId != previousPreleveurId)
        {
            var locationLabel = order.SamplingLocation?.Name ?? order.OrderNumber;
            var dateLabel = order.PlannedDate?.ToString("dd/MM/yyyy") ?? "date non planifiée";
            try
            {
                await notificationService.CreateAsync(
                    dto.PreleveurId,
                    NotificationType.OrderAssigned,
                    "Nouveau mandat",
                    $"Mandat « {locationLabel} » ({order.OrderNumber}) prévu le {dateLabel} vous a été assigné.",
                    tenantId,
                    relatedEntityType: "Order",
                    relatedEntityId: order.Id,
                    isUrgent: false,
                    cancellationToken);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed to create OrderAssigned notification for order {OrderId}", order.Id);
            }
        }

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

    public async Task<IList<RequiredContainerDto>?> GetRequiredContainersAsync(
        Guid orderId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(o => o.OrderAnalysisPrograms)
                .ThenInclude(oap => oap.AnalysisProgram!)
                    .ThenInclude(ap => ap.AnalysisProgramProfiles)
                        .ThenInclude(app => app.AnalysisProfile!)
                            .ThenInclude(ap => ap.Container)
            .Include(o => o.Sampling)
                .ThenInclude(s => s!.Containers)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        // A mandate has a single canonical barcode shared by every container (Sampling.SampleBarcode).
        var existingBarcode = order.Sampling?.SampleBarcode;

        var containers = order.OrderAnalysisPrograms
            .Where(oap => oap.AnalysisProgram is not null)
            .SelectMany(oap => oap.AnalysisProgram!.AnalysisProgramProfiles)
            .Where(app => app.AnalysisProfile?.Container is not null)
            .Select(app => app.AnalysisProfile!.Container!)
            .GroupBy(c => c.Id)
            .Select(g => g.First())
            .OrderBy(c => c.Code)
            .Select(c => new RequiredContainerDto(
                c.Id,
                c.Code,
                c.Name,
                c.Material,
                c.VolumeMl,
                existingBarcode))
            .ToList();

        return containers;
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

    private static OrderDetailDto MapToDetailDto(Order order, ResultsStatus resultsStatus = ResultsStatus.NotReceived)
    {
        SamplingDto? samplingDto = null;
        if (order.Sampling is not null)
        {
            var s = order.Sampling;
            // Every container shares the canonical mandate barcode (Sampling.SampleBarcode).
            var containers = s.Containers
                .Select(c => new SamplingContainerDto(c.Id, c.ContainerId, s.SampleBarcode, c.BarcodeScannedAt))
                .ToList();
            samplingDto = new SamplingDto(
                s.Id, s.OrderId, s.PreleveurId,
                s.Preleveur is not null ? s.Preleveur.FirstName + " " + s.Preleveur.LastName : null,
                s.SamplingDateTime, s.Temperature, s.Weather,
                s.Notes,
                s.HasWaterSoftener, s.IsChlorinated,
                s.SampleBarcode, s.BarcodeScannedAt,
                s.IsValidated, s.ValidatedAt, s.CreatedAt,
                containers);
        }

        var analysisPrograms = order.OrderAnalysisPrograms
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
            analysisPrograms,
            order.TenantId, order.CreatedAt, order.UpdatedAt, samplingDto,
            order.SamplingRoundId,
            isRoundLocked,
            round?.LockedById,
            lockedByName,
            resultsStatus);
    }

    public async Task<BulkTransitionResultDto> BulkValidateAsync(string userId, Guid tenantId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        return await BulkTransitionAsync(
            userId, tenantId, isAdmin, OrderStatus.InProgress, OrderStatus.Completed, cancellationToken);
    }

    public async Task<BulkTransitionResultDto> BulkTransmitAsync(string userId, Guid tenantId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        return await BulkTransitionAsync(
            userId, tenantId, isAdmin, OrderStatus.Completed, OrderStatus.Transmitted, cancellationToken);
    }

    /// <summary>
    /// AQ-406 — finalize every eligible order of the tenant in a single step:
    /// first InProgress → Completed (validation), then Completed → Transmitted
    /// (LIMS transmission). Chaining the two transitions means orders that
    /// were InProgress at call time end up Transmitted when the call returns,
    /// matching the user's expectation that "Tout transmettre" also validates.
    /// </summary>
    public async Task<BulkFinalizeResultDto> BulkFinalizeAsync(string userId, Guid tenantId, bool isAdmin, CancellationToken cancellationToken = default)
    {
        var validated = await BulkTransitionAsync(
            userId, tenantId, isAdmin, OrderStatus.InProgress, OrderStatus.Completed, cancellationToken);
        var transmitted = await BulkTransitionAsync(
            userId, tenantId, isAdmin, OrderStatus.Completed, OrderStatus.Transmitted, cancellationToken);

        logger.LogInformation(
            "Bulk finalize by {UserId} in tenant {TenantId}: {Validated} validated, {Transmitted} transmitted",
            userId, tenantId, validated.Affected, transmitted.Affected);

        return new BulkFinalizeResultDto(validated.Affected, transmitted.Affected);
    }

    /// <summary>
    /// AQ-31 — Aggregate counters for the home dashboard, scoped to the user's visibility rules.
    /// AQ-420 — Same role-based scoping as GetOrdersFilteredAsync (admin / préleveur-only /
    /// mandataire). The previous "and created-by-me" restriction hid orders of the user's
    /// own distributor that were created by another requérant.
    /// </summary>
    public async Task<OrderDashboardSummaryDto> GetDashboardSummaryAsync(
        string userId, Guid tenantId, bool isAdmin, bool isPreleveurOnly, CancellationToken cancellationToken = default)
    {
        var query = dbContext.Orders
            .Where(o => o.TenantId == tenantId)
            .AsQueryable();

        if (!isAdmin)
        {
            if (isPreleveurOnly)
            {
                query = query.Where(o => o.PreleveurId == userId);
            }
            else
            {
                var authorizedDistributorIds = await delegationService
                    .GetAuthorizedDistributorIdsForUserAsync(userId, cancellationToken);
                query = query.Where(o => authorizedDistributorIds.Contains(o.DistributorId));
            }
        }

        var stats = await query
            .Select(o => new
            {
                o.Status,
                TotalResults = dbContext.SamplingResults.Count(r => r.OrderId == o.Id),
                NonConformResults = dbContext.SamplingResults.Count(r => r.OrderId == o.Id && !r.IsConform),
            })
            .ToListAsync(cancellationToken);

        var conform = 0;
        var nonConform = 0;
        var pending = 0;
        foreach (var s in stats)
        {
            var status = DeriveResultsStatus(s.Status, s.TotalResults, s.NonConformResults);
            switch (status)
            {
                case ResultsStatus.Conform:
                    conform++;
                    break;
                case ResultsStatus.NonConform:
                    nonConform++;
                    break;
                default:
                    pending++;
                    break;
            }
        }

        return new OrderDashboardSummaryDto(conform, nonConform, pending, stats.Count);
    }

    private async Task<BulkTransitionResultDto> BulkTransitionAsync(
        string userId, Guid tenantId, bool isAdmin, OrderStatus fromStatus, OrderStatus toStatus,
        CancellationToken cancellationToken)
    {
        var query = dbContext.Orders
            .Where(o => o.TenantId == tenantId && o.Status == fromStatus);

        // Sprint Sec F-005 — same scoping as GetOrdersFilteredAsync (AQ-398/AQ-420):
        // non-admin callers only transition the orders of the distributors they are
        // authorized on (own distributor + active delegations), never the whole tenant.
        if (!isAdmin)
        {
            var authorizedDistributorIds = await delegationService
                .GetAuthorizedDistributorIdsForUserAsync(userId, cancellationToken);
            query = query.Where(o => authorizedDistributorIds.Contains(o.DistributorId));
        }

        var orders = await query.ToListAsync(cancellationToken);

        if (orders.Count == 0)
        {
            return new BulkTransitionResultDto(0);
        }

        // AQ-371 — skip orders whose parent round is locked by another user.
        var lockedRoundIds = await dbContext.SamplingRounds
            .Where(r => r.TenantId == tenantId && r.IsLocked && r.LockedById != userId)
            .Select(r => (Guid?)r.Id)
            .ToListAsync(cancellationToken);
        if (lockedRoundIds.Count > 0)
        {
            orders = orders.Where(o => !lockedRoundIds.Contains(o.SamplingRoundId)).ToList();
            if (orders.Count == 0)
            {
                return new BulkTransitionResultDto(0);
            }
        }

        var now = DateTime.UtcNow;
        var roundIds = new HashSet<Guid>();
        var transmittingToLims = toStatus == OrderStatus.Transmitted;
        foreach (var order in orders)
        {
            order.Status = toStatus;
            order.StatusChangedAt = now;
            order.StatusChangedBy = userId;
            order.UpdatedAt = now;
            order.UpdatedBy = userId;
            if (transmittingToLims)
            {
                order.TransmittedAt = now;
            }
            if (order.SamplingRoundId.HasValue)
            {
                roundIds.Add(order.SamplingRoundId.Value);
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // AQ-33 — when orders transition to Transmitted and the Mock LIMS feature flag
        // is enabled, forward them to the internal Mock LIMS to close the integration loop.
        if (transmittingToLims && mockLimsOptions.Value.Enabled)
        {
            await TransmitOrdersToMockLimsAsync(orders, tenantId, cancellationToken);
        }

        // Audit log per order (same pattern as OrderStatusService)
        foreach (var order in orders)
        {
            await auditService.LogAsync(
                order.Id,
                "StatusTransitioned",
                $"Status changed from {fromStatus} to {toStatus} (bulk)",
                fromStatus.ToString(),
                toStatus.ToString(),
                userId,
                tenantId,
                cancellationToken);
        }

        // Auto-complete rounds where all orders reached terminal states (same as OrderStatusService)
        if (roundIds.Count > 0)
        {
            var rounds = await dbContext.SamplingRounds
                .Include(r => r.Orders)
                .Where(r => roundIds.Contains(r.Id) && r.Status == SamplingRoundStatus.InProgress)
                .ToListAsync(cancellationToken);

            var completed = false;
            foreach (var round in rounds)
            {
                if (round.Orders.All(o => o.Status is OrderStatus.Transmitted or OrderStatus.Done or OrderStatus.Cancelled))
                {
                    round.Status = SamplingRoundStatus.Completed;
                    round.CompletedAt = now;
                    round.UpdatedAt = now;
                    round.UpdatedBy = userId;
                    completed = true;
                }
            }
            if (completed)
            {
                await dbContext.SaveChangesAsync(cancellationToken);
            }
        }

        logger.LogInformation(
            "Bulk transitioned {Count} orders from {FromStatus} to {ToStatus} by {UserId} in tenant {TenantId}",
            orders.Count, fromStatus, toStatus, userId, tenantId);

        return new BulkTransitionResultDto(orders.Count);
    }

    /// <summary>
    /// AQ-33 — Forward transmitted orders to the Mock LIMS and persist the returned LimsOrderId.
    /// Failures are logged and swallowed per order (best-effort, do not block the bulk transition).
    /// </summary>
    private async Task TransmitOrdersToMockLimsAsync(
        IList<Order> orders,
        Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (orders.Count == 0)
        {
            return;
        }

        // Load the analysis programs + profiles so we can compose the parameter list for the Mock.
        var orderIds = orders.Select(o => o.Id).ToList();
        var programsByOrder = await dbContext.OrderAnalysisPrograms
            .Where(oap => orderIds.Contains(oap.OrderId))
            .Include(oap => oap.AnalysisProgram!)
                .ThenInclude(p => p.AnalysisProgramProfiles)
                    .ThenInclude(app => app.AnalysisProfile)
            .ToListAsync(cancellationToken);

        var persistNeeded = false;
        foreach (var order in orders)
        {
            if (order.LimsOrderId.HasValue)
            {
                continue; // idempotence — already transmitted previously
            }

            var parameters = programsByOrder
                .Where(oap => oap.OrderId == order.Id && oap.AnalysisProgram is not null)
                .SelectMany(oap => oap.AnalysisProgram!.AnalysisProgramProfiles)
                .Where(app => app.AnalysisProfile is not null)
                .Select(app => app.AnalysisProfile!.Code)
                .Distinct()
                .ToList();

            if (parameters.Count == 0)
            {
                // Default set if the mandate has no analysis profiles linked yet.
                parameters = MockLimsParameterCatalog.All.Select(p => p.Code).ToList();
            }
            else
            {
                // Profiles may not map 1:1 to Mock parameter codes — keep only those that do, fall back to catalog otherwise.
                var recognized = parameters.Where(code => MockLimsParameterCatalog.FindByCode(code) is not null).ToList();
                if (recognized.Count == 0)
                {
                    parameters = MockLimsParameterCatalog.All.Select(p => p.Code).ToList();
                }
                else
                {
                    parameters = recognized;
                }
            }

            var dto = new MockLimsOrderCreateDto(
                OrderReference: order.OrderNumber,
                SamplingDate: order.Sampling?.SamplingDateTime ?? order.PlannedDate ?? DateTime.UtcNow,
                Parameters: parameters,
                SourceOrderId: order.Id);

            try
            {
                var response = await mockLimsService.ReceiveOrderAsync(dto, tenantId, cancellationToken);
                order.LimsOrderId = response.LimsOrderId;
                persistNeeded = true;
                logger.LogInformation(
                    "Order {OrderNumber} forwarded to Mock LIMS -> LimsOrderId={LimsOrderId}",
                    order.OrderNumber, response.LimsOrderId);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex,
                    "Failed to forward order {OrderNumber} to Mock LIMS; will remain without LimsOrderId",
                    order.OrderNumber);
            }
        }

        if (persistNeeded)
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<string> GenerateOrderNumberAsync(CancellationToken cancellationToken)
    {
        var today = DateTime.UtcNow;
        var prefix = $"ORD-{today:yyyyMMdd}";
        var maxNumber = await dbContext.Orders
            .Where(o => o.OrderNumber.StartsWith(prefix))
            .Select(o => o.OrderNumber)
            .MaxAsync(cancellationToken) as string;

        var nextSeq = 1;
        if (maxNumber is not null)
        {
            var lastPart = maxNumber[(prefix.Length + 1)..];
            nextSeq = int.Parse(lastPart) + 1;
        }

        return $"{prefix}-{nextSeq:D4}";
    }
}
