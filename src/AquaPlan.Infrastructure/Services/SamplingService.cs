using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.DTOs.Samplings;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace AquaPlan.Infrastructure.Services;

internal class SamplingService(
    AquaPlanDbContext dbContext,
    IOrderAuditService auditService,
    ILogger<SamplingService> logger) : ISamplingService
{
    public async Task<SamplingDto?> GetByOrderIdAsync(
        Guid orderId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var sampling = await dbContext.Samplings
            .Include(s => s.Preleveur)
            .Where(s => s.OrderId == orderId && s.Order!.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (sampling is null) return null;

        return MapToDto(sampling);
    }

    public async Task<SamplingDto> CreateAsync(
        SamplingCreateDto dto, string preleveurId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(o => o.Sampling)
            .Include(o => o.SamplingRound)
            .Where(o => o.Id == dto.OrderId && o.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null)
        {
            throw new InvalidOperationException("Order not found.");
        }

        if (order.Status != OrderStatus.InProgress)
        {
            throw new InvalidOperationException($"Cannot create sampling for order in status {order.Status}. Order must be InProgress.");
        }

        // Verify the user is the assigned préleveur (via the round)
        var assignedPreleveurId = order.SamplingRound?.PreleveurId ?? order.PreleveurId;
        if (assignedPreleveurId != preleveurId)
        {
            throw new UnauthorizedAccessException("Only the assigned préleveur can create sampling data.");
        }

        if (order.Sampling is not null)
        {
            throw new InvalidOperationException("Sampling data already exists for this order. Use update instead.");
        }

        var sampling = new Sampling
        {
            Id = Guid.NewGuid(),
            OrderId = dto.OrderId,
            PreleveurId = preleveurId,
            SamplingDateTime = DateTime.SpecifyKind(dto.SamplingDateTime, DateTimeKind.Utc),
            Temperature = dto.Temperature,
            Weather = dto.Weather,
            LocationLat = dto.LocationLat,
            LocationLng = dto.LocationLng,
            Notes = dto.Notes,
            HasWaterSoftener = dto.HasWaterSoftener,
            IsChlorinated = dto.IsChlorinated,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.Samplings.Add(sampling);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Sampling {SamplingId} created for order {OrderId} by préleveur {PreleveurId}",
            sampling.Id, dto.OrderId, preleveurId);

        await auditService.LogAsync(dto.OrderId, "SamplingCreated", "Données de prélèvement saisies",
            null, $"T={dto.Temperature}°C, Météo={dto.Weather}", preleveurId, tenantId, cancellationToken);

        // Reload with navigation properties
        await dbContext.Entry(sampling).Reference(s => s.Preleveur).LoadAsync(cancellationToken);

        return MapToDto(sampling);
    }

    public async Task<SamplingDto?> UpdateAsync(
        Guid orderId, SamplingCreateDto dto, string preleveurId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(o => o.Sampling)
            .Include(o => o.SamplingRound)
            .Where(o => o.Id == orderId && o.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null) return null;

        if (order.Status != OrderStatus.InProgress)
        {
            throw new InvalidOperationException($"Cannot update sampling for order in status {order.Status}. Order must be InProgress.");
        }

        var assignedPreleveurId = order.SamplingRound?.PreleveurId ?? order.PreleveurId;
        if (assignedPreleveurId != preleveurId)
        {
            throw new UnauthorizedAccessException("Only the assigned préleveur can update sampling data.");
        }

        var sampling = order.Sampling;
        if (sampling is null) return null;

        sampling.SamplingDateTime = DateTime.SpecifyKind(dto.SamplingDateTime, DateTimeKind.Utc);
        sampling.Temperature = dto.Temperature;
        sampling.Weather = dto.Weather;
        sampling.LocationLat = dto.LocationLat;
        sampling.LocationLng = dto.LocationLng;
        sampling.Notes = dto.Notes;
        sampling.HasWaterSoftener = dto.HasWaterSoftener;
        sampling.IsChlorinated = dto.IsChlorinated;
        sampling.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Sampling updated for order {OrderId} by préleveur {PreleveurId}",
            orderId, preleveurId);

        await auditService.LogAsync(orderId, "SamplingUpdated", "Données de prélèvement modifiées",
            null, $"T={dto.Temperature}°C, Météo={dto.Weather}", preleveurId, tenantId, cancellationToken);

        await dbContext.Entry(sampling).Reference(s => s.Preleveur).LoadAsync(cancellationToken);

        return MapToDto(sampling);
    }

    public async Task<bool> CompleteAsync(
        Guid orderId, string preleveurId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(o => o.Sampling)
            .Include(o => o.SamplingRound)
            .Where(o => o.Id == orderId && o.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null) return false;

        if (order.Status != OrderStatus.InProgress)
        {
            throw new InvalidOperationException($"Cannot complete sampling for order in status {order.Status}. Order must be InProgress.");
        }

        var assignedPreleveurId = order.SamplingRound?.PreleveurId ?? order.PreleveurId;
        if (assignedPreleveurId != preleveurId)
        {
            throw new UnauthorizedAccessException("Only the assigned préleveur can complete sampling.");
        }

        if (order.Sampling is null)
        {
            throw new InvalidOperationException("Cannot complete sampling: no sampling data recorded yet.");
        }

        order.Status = OrderStatus.Completed;
        order.StatusChangedAt = DateTime.UtcNow;
        order.StatusChangedBy = preleveurId;
        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = preleveurId;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Order {OrderId} sampling completed by préleveur {PreleveurId}",
            orderId, preleveurId);

        await auditService.LogAsync(orderId, "SamplingCompleted", "Prélèvement terminé",
            "InProgress", "Completed", preleveurId, tenantId, cancellationToken);

        return true;
    }

    public async Task<bool> ValidateAsync(
        Guid orderId, string validatorId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(o => o.Sampling)
            .Where(o => o.Id == orderId && o.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null) return false;

        if (order.Status != OrderStatus.Completed)
        {
            throw new InvalidOperationException($"Cannot validate sampling for order in status {order.Status}. Order must be Completed.");
        }

        var sampling = order.Sampling;
        if (sampling is null)
        {
            throw new InvalidOperationException("Cannot validate: no sampling data found.");
        }

        sampling.IsValidated = true;
        sampling.ValidatedAt = DateTime.UtcNow;
        sampling.UpdatedAt = DateTime.UtcNow;

        order.UpdatedAt = DateTime.UtcNow;
        order.UpdatedBy = validatorId;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Sampling validated for order {OrderId} by {ValidatorId}",
            orderId, validatorId);

        await auditService.LogAsync(orderId, "SamplingValidated", "Prélèvement validé",
            "Completed", "Completed", validatorId, tenantId, cancellationToken);

        return true;
    }

    public async Task<SamplingDto?> ScanBarcodeAsync(
        Guid orderId, string barcode, string preleveurId, Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var order = await dbContext.Orders
            .Include(o => o.Sampling)
            .Include(o => o.SamplingRound)
            .Where(o => o.Id == orderId && o.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (order is null) return null;

        if (order.Status != OrderStatus.InProgress && order.Status != OrderStatus.Completed)
        {
            throw new InvalidOperationException($"Cannot scan barcode for order in status {order.Status}.");
        }

        var assignedPreleveurId = order.SamplingRound?.PreleveurId ?? order.PreleveurId;
        if (assignedPreleveurId != preleveurId)
        {
            throw new UnauthorizedAccessException("Only the assigned préleveur can scan a barcode.");
        }

        var sampling = order.Sampling;
        if (sampling is null)
        {
            throw new InvalidOperationException("Cannot scan barcode: no sampling data recorded yet. Create sampling first.");
        }

        // Check barcode uniqueness within tenant
        var existingBarcode = await dbContext.Samplings
            .Where(s => s.SampleBarcode == barcode && s.Order!.TenantId == tenantId && s.Id != sampling.Id)
            .AnyAsync(cancellationToken);

        if (existingBarcode)
        {
            throw new InvalidOperationException($"Barcode '{barcode}' is already associated with another sampling.");
        }

        sampling.SampleBarcode = barcode;
        sampling.BarcodeScannedAt = DateTime.UtcNow;
        sampling.UpdatedAt = DateTime.UtcNow;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Barcode {Barcode} scanned for order {OrderId} by préleveur {PreleveurId}",
            barcode, orderId, preleveurId);

        await auditService.LogAsync(orderId, "BarcodeScanned", $"Code-barres scanné: {barcode}",
            null, barcode, preleveurId, tenantId, cancellationToken);

        await dbContext.Entry(sampling).Reference(s => s.Preleveur).LoadAsync(cancellationToken);

        return MapToDto(sampling);
    }

    public async Task<SamplingDto?> GetByBarcodeAsync(
        string barcode, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var sampling = await dbContext.Samplings
            .Include(s => s.Preleveur)
            .Where(s => s.SampleBarcode == barcode && s.Order!.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (sampling is null) return null;

        return MapToDto(sampling);
    }

    private static SamplingDto MapToDto(Sampling sampling)
    {
        return new SamplingDto(
            sampling.Id,
            sampling.OrderId,
            sampling.PreleveurId,
            sampling.Preleveur is not null
                ? $"{sampling.Preleveur.FirstName} {sampling.Preleveur.LastName}"
                : null,
            sampling.SamplingDateTime,
            sampling.Temperature,
            sampling.Weather,
            sampling.LocationLat,
            sampling.LocationLng,
            sampling.Notes,
            sampling.HasWaterSoftener,
            sampling.IsChlorinated,
            sampling.SampleBarcode,
            sampling.BarcodeScannedAt,
            sampling.IsValidated,
            sampling.ValidatedAt,
            sampling.CreatedAt);
    }
}
