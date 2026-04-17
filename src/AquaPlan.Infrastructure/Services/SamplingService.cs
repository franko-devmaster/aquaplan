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
            .Include(s => s.Containers)
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
            Notes = dto.Notes,
            HasWaterSoftener = dto.HasWaterSoftener,
            IsChlorinated = dto.IsChlorinated,
            SampleBarcode = dto.SampleBarcode,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.Samplings.Add(sampling);

        if (dto.Containers is { Count: > 0 })
        {
            await ValidateBarcodeUniquenessAsync(dto.Containers, sampling.Id, tenantId, cancellationToken);

            foreach (var containerInput in dto.Containers)
            {
                var barcode = string.IsNullOrWhiteSpace(containerInput.Barcode) ? null : containerInput.Barcode.Trim();
                sampling.Containers.Add(new SamplingContainer
                {
                    Id = Guid.NewGuid(),
                    SamplingId = sampling.Id,
                    ContainerId = containerInput.ContainerId,
                    Barcode = barcode,
                    BarcodeScannedAt = containerInput.BarcodeScannedAt,
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow,
                });
            }
        }

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
                .ThenInclude(s => s!.Containers)
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
        sampling.Notes = dto.Notes;
        sampling.HasWaterSoftener = dto.HasWaterSoftener;
        sampling.IsChlorinated = dto.IsChlorinated;
        sampling.SampleBarcode = dto.SampleBarcode;
        sampling.UpdatedAt = DateTime.UtcNow;

        if (dto.Containers is not null)
        {
            await ValidateBarcodeUniquenessAsync(dto.Containers, sampling.Id, tenantId, cancellationToken);
            UpsertContainers(sampling, dto.Containers, tenantId);
        }

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
                .ThenInclude(s => s!.Containers)
            .Include(o => o.SamplingRound)
            .Include(o => o.OrderAnalysisPrograms)
                .ThenInclude(oap => oap.AnalysisProgram!)
                    .ThenInclude(ap => ap.AnalysisProgramProfiles)
                        .ThenInclude(app => app.AnalysisProfile!)
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

        // Every required container must have a non-empty barcode before completion.
        var requiredContainerIds = order.OrderAnalysisPrograms
            .Where(oap => oap.AnalysisProgram is not null)
            .SelectMany(oap => oap.AnalysisProgram!.AnalysisProgramProfiles)
            .Where(app => app.AnalysisProfile is not null)
            .Select(app => app.AnalysisProfile!.ContainerId)
            .Distinct()
            .ToList();

        if (requiredContainerIds.Count > 0)
        {
            var recordedBarcodes = order.Sampling.Containers
                .Where(c => !string.IsNullOrWhiteSpace(c.Barcode))
                .Select(c => c.ContainerId)
                .ToHashSet();
            var missing = requiredContainerIds.Where(id => !recordedBarcodes.Contains(id)).ToList();
            if (missing.Count > 0)
            {
                throw new InvalidOperationException(
                    "Cannot complete sampling: a barcode is missing for at least one required container.");
            }
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
                .ThenInclude(s => s!.Containers)
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

        // Check barcode uniqueness within tenant (legacy field)
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
            .Include(s => s.Containers)
            .Where(s => s.SampleBarcode == barcode && s.Order!.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (sampling is null) return null;

        return MapToDto(sampling);
    }

    private async Task ValidateBarcodeUniquenessAsync(
        IList<SamplingContainerInputDto> inputs, Guid samplingId, Guid tenantId,
        CancellationToken cancellationToken)
    {
        // Duplicate barcodes within the payload itself
        var withinPayload = inputs
            .Where(i => !string.IsNullOrWhiteSpace(i.Barcode))
            .GroupBy(i => i.Barcode!.Trim())
            .FirstOrDefault(g => g.Count() > 1);
        if (withinPayload is not null)
        {
            throw new InvalidOperationException(
                $"Barcode '{withinPayload.Key}' is used more than once in the same sampling.");
        }

        var nonNullBarcodes = inputs
            .Where(i => !string.IsNullOrWhiteSpace(i.Barcode))
            .Select(i => i.Barcode!.Trim())
            .Distinct()
            .ToList();

        if (nonNullBarcodes.Count == 0) return;

        var collision = await dbContext.SamplingContainers
            .Where(sc => sc.TenantId == tenantId
                && sc.SamplingId != samplingId
                && sc.Barcode != null
                && nonNullBarcodes.Contains(sc.Barcode))
            .Select(sc => sc.Barcode)
            .FirstOrDefaultAsync(cancellationToken);

        if (collision is not null)
        {
            throw new InvalidOperationException(
                $"Barcode '{collision}' is already used by another sampling of the same tenant.");
        }
    }

    private void UpsertContainers(
        Sampling sampling, IList<SamplingContainerInputDto> inputs, Guid tenantId)
    {
        var inputIds = inputs.Select(i => i.ContainerId).ToHashSet();

        // Remove rows that are no longer in the payload
        var toRemove = sampling.Containers.Where(c => !inputIds.Contains(c.ContainerId)).ToList();
        foreach (var removed in toRemove)
        {
            dbContext.SamplingContainers.Remove(removed);
        }

        foreach (var input in inputs)
        {
            var normalizedBarcode = string.IsNullOrWhiteSpace(input.Barcode) ? null : input.Barcode.Trim();
            var existing = sampling.Containers.FirstOrDefault(c => c.ContainerId == input.ContainerId);
            if (existing is null)
            {
                dbContext.SamplingContainers.Add(new SamplingContainer
                {
                    Id = Guid.NewGuid(),
                    SamplingId = sampling.Id,
                    ContainerId = input.ContainerId,
                    Barcode = normalizedBarcode,
                    BarcodeScannedAt = input.BarcodeScannedAt,
                    TenantId = tenantId,
                    CreatedAt = DateTime.UtcNow,
                });
            }
            else
            {
                existing.Barcode = normalizedBarcode;
                if (input.BarcodeScannedAt.HasValue)
                {
                    existing.BarcodeScannedAt = input.BarcodeScannedAt;
                }
            }
        }
    }

    private static SamplingDto MapToDto(Sampling sampling)
    {
        var containers = sampling.Containers
            .Select(c => new SamplingContainerDto(c.Id, c.ContainerId, c.Barcode, c.BarcodeScannedAt))
            .ToList();

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
            sampling.Notes,
            sampling.HasWaterSoftener,
            sampling.IsChlorinated,
            sampling.SampleBarcode,
            sampling.BarcodeScannedAt,
            sampling.IsValidated,
            sampling.ValidatedAt,
            sampling.CreatedAt,
            containers);
    }
}
