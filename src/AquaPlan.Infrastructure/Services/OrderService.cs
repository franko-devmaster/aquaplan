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
    public async Task<IList<OrderListDto>> GetOrdersForUserAsync(string userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        // User sees orders where they are the creator OR assigned préleveur,
        // AND the order belongs to a distributor they are linked to
        var userDistributorIds = await dbContext.UserDistributors
            .Where(ud => ud.UserId == userId)
            .Select(ud => ud.DistributorId)
            .ToListAsync(cancellationToken);

        return await dbContext.Orders
            .Where(o => o.TenantId == tenantId
                && userDistributorIds.Contains(o.DistributorId)
                && (o.CreatedById == userId || o.PreleveurId == userId))
            .Include(o => o.CreatedBy)
            .Include(o => o.Preleveur)
            .Include(o => o.Distributor)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderListDto(
                o.Id, o.OrderNumber, o.Status, o.IsUnplanned,
                o.CreatedById, o.CreatedBy != null ? o.CreatedBy.FirstName + " " + o.CreatedBy.LastName : null,
                o.PreleveurId, o.Preleveur != null ? o.Preleveur.FirstName + " " + o.Preleveur.LastName : null,
                o.DistributorId, o.Distributor != null ? o.Distributor.Name : string.Empty,
                o.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<IList<OrderListDto>> GetAllOrdersAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.Orders
            .Where(o => o.TenantId == tenantId)
            .Include(o => o.CreatedBy)
            .Include(o => o.Preleveur)
            .Include(o => o.Distributor)
            .OrderByDescending(o => o.CreatedAt)
            .Select(o => new OrderListDto(
                o.Id, o.OrderNumber, o.Status, o.IsUnplanned,
                o.CreatedById, o.CreatedBy != null ? o.CreatedBy.FirstName + " " + o.CreatedBy.LastName : null,
                o.PreleveurId, o.Preleveur != null ? o.Preleveur.FirstName + " " + o.Preleveur.LastName : null,
                o.DistributorId, o.Distributor != null ? o.Distributor.Name : string.Empty,
                o.CreatedAt))
            .ToListAsync(cancellationToken);
    }

    public async Task<OrderDetailDto?> GetOrderByIdAsync(Guid orderId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(o => o.CreatedBy)
            .Include(o => o.Preleveur)
            .Include(o => o.Distributor)
            .Include(o => o.Sampling)
                .ThenInclude(s => s!.Preleveur)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.TenantId == tenantId, cancellationToken);

        if (order is null)
        {
            return null;
        }

        SamplingDto? samplingDto = null;
        if (order.Sampling is not null)
        {
            var s = order.Sampling;
            samplingDto = new SamplingDto(
                s.Id, s.OrderId, s.PreleveurId,
                s.Preleveur is not null ? s.Preleveur.FirstName + " " + s.Preleveur.LastName : null,
                s.SamplingDateTime, s.Temperature, s.Weather,
                s.LocationLat, s.LocationLng, s.Notes,
                s.IsValidated, s.ValidatedAt, s.CreatedAt);
        }

        return new OrderDetailDto(
            order.Id, order.OrderNumber, order.Status, order.IsUnplanned,
            order.CreatedById, order.CreatedBy is not null ? order.CreatedBy.FirstName + " " + order.CreatedBy.LastName : null,
            order.PreleveurId, order.Preleveur is not null ? order.Preleveur.FirstName + " " + order.Preleveur.LastName : null,
            order.DistributorId, order.Distributor is not null ? order.Distributor.Name : string.Empty,
            order.TenantId, order.CreatedAt, order.UpdatedAt, samplingDto);
    }

    public async Task<OrderDetailDto> CreateOrderAsync(OrderCreateDto dto, string createdById, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var orderNumber = await GenerateOrderNumberAsync(cancellationToken);

        var order = new Order
        {
            Id = Guid.NewGuid(),
            OrderNumber = orderNumber,
            Status = dto.IsUnplanned ? OrderStatus.InProgress : OrderStatus.Draft,
            IsUnplanned = dto.IsUnplanned,
            CreatedById = createdById,
            PreleveurId = dto.PreleveurId,
            DistributorId = dto.DistributorId,
            TenantId = tenantId,
        };

        dbContext.Orders.Add(order);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Order {OrderNumber} created by {CreatedBy}", orderNumber, createdById);

        return (await GetOrderByIdAsync(order.Id, tenantId, cancellationToken))!;
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
        order.Status = OrderStatus.Assigned;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = updatedBy;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Order {OrderId} assigned to préleveur {PreleveurId}", orderId, dto.PreleveurId);

        return await GetOrderByIdAsync(orderId, tenantId, cancellationToken);
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
