using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.Exceptions;
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
            throw new BusinessRuleException("Order not found.");
        }

        if (order.Status != OrderStatus.InProgress)
        {
            throw new BusinessRuleException($"Cannot create sampling for order in status {order.Status}. Order must be InProgress.");
        }

        // Verify the user is the assigned préleveur (via the round)
        var assignedPreleveurId = order.SamplingRound?.PreleveurId ?? order.PreleveurId;
        if (assignedPreleveurId != preleveurId)
        {
            throw new UnauthorizedAccessException("Only the assigned préleveur can create sampling data.");
        }

        if (order.Sampling is not null)
        {
            throw new BusinessRuleException("Sampling data already exists for this order. Use update instead.");
        }

        // Derive canonical barcode from containers (if any) or from dto.SampleBarcode.
        var canonicalBarcode = ResolveCanonicalBarcode(dto);
        await EnsureBarcodeUniqueAcrossTenantAsync(canonicalBarcode, null, tenantId, cancellationToken);

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
            SampleBarcode = canonicalBarcode,
            BarcodeScannedAt = canonicalBarcode is not null ? DateTime.UtcNow : null,
            TenantId = tenantId,
            CreatedAt = DateTime.UtcNow,
        };

        dbContext.Samplings.Add(sampling);

        if (dto.Containers is { Count: > 0 })
        {
            foreach (var containerInput in dto.Containers)
            {
                sampling.Containers.Add(new SamplingContainer
                {
                    Id = Guid.NewGuid(),
                    SamplingId = sampling.Id,
                    ContainerId = containerInput.ContainerId,
                    BarcodeScannedAt = containerInput.BarcodeScannedAt,
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
            throw new BusinessRuleException($"Cannot update sampling for order in status {order.Status}. Order must be InProgress.");
        }

        var assignedPreleveurId = order.SamplingRound?.PreleveurId ?? order.PreleveurId;
        if (assignedPreleveurId != preleveurId)
        {
            throw new UnauthorizedAccessException("Only the assigned préleveur can update sampling data.");
        }

        var sampling = order.Sampling;
        if (sampling is null) return null;

        var canonicalBarcode = ResolveCanonicalBarcode(dto);
        await EnsureBarcodeUniqueAcrossTenantAsync(canonicalBarcode, sampling.Id, tenantId, cancellationToken);

        sampling.SamplingDateTime = DateTime.SpecifyKind(dto.SamplingDateTime, DateTimeKind.Utc);
        sampling.Temperature = dto.Temperature;
        sampling.Weather = dto.Weather;
        sampling.Notes = dto.Notes;
        sampling.HasWaterSoftener = dto.HasWaterSoftener;
        sampling.IsChlorinated = dto.IsChlorinated;

        if (canonicalBarcode is not null && sampling.SampleBarcode != canonicalBarcode)
        {
            sampling.SampleBarcode = canonicalBarcode;
            sampling.BarcodeScannedAt = DateTime.UtcNow;
        }
        else if (canonicalBarcode is null)
        {
            sampling.SampleBarcode = null;
            sampling.BarcodeScannedAt = null;
        }

        sampling.UpdatedAt = DateTime.UtcNow;

        if (dto.Containers is not null)
        {
            UpsertContainers(sampling, dto.Containers);
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
            throw new BusinessRuleException($"Cannot complete sampling for order in status {order.Status}. Order must be InProgress.");
        }

        var assignedPreleveurId = order.SamplingRound?.PreleveurId ?? order.PreleveurId;
        if (assignedPreleveurId != preleveurId)
        {
            throw new UnauthorizedAccessException("Only the assigned préleveur can complete sampling.");
        }

        if (order.Sampling is null)
        {
            throw new BusinessRuleException("Cannot complete sampling: no sampling data recorded yet.");
        }

        // A mandate can only be completed once a canonical barcode has been captured.
        if (string.IsNullOrWhiteSpace(order.Sampling.SampleBarcode))
        {
            throw new BusinessRuleException(
                "Cannot complete sampling: a barcode is missing for at least one required container.");
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
            throw new BusinessRuleException($"Cannot validate sampling for order in status {order.Status}. Order must be Completed.");
        }

        var sampling = order.Sampling;
        if (sampling is null)
        {
            throw new BusinessRuleException("Cannot validate: no sampling data found.");
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
            throw new BusinessRuleException($"Cannot scan barcode for order in status {order.Status}.");
        }

        var assignedPreleveurId = order.SamplingRound?.PreleveurId ?? order.PreleveurId;
        if (assignedPreleveurId != preleveurId)
        {
            throw new UnauthorizedAccessException("Only the assigned préleveur can scan a barcode.");
        }

        var sampling = order.Sampling;
        if (sampling is null)
        {
            throw new BusinessRuleException("Cannot scan barcode: no sampling data recorded yet. Create sampling first.");
        }

        await EnsureBarcodeUniqueAcrossTenantAsync(barcode, sampling.Id, tenantId, cancellationToken);

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
            .Where(s => s.SampleBarcode == barcode && s.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (sampling is null) return null;

        return MapToDto(sampling);
    }

    /// <summary>
    /// Extracts the canonical barcode for a mandate.
    /// Rule: every container of the same mandate must share the same barcode
    /// (it is the same sample, conditioned into multiple vials).
    /// Returns the shared barcode, or null if all inputs are empty.
    /// Throws when container barcodes diverge.
    /// </summary>
    private static string? ResolveCanonicalBarcode(SamplingCreateDto dto)
    {
        var distinctContainerBarcodes = dto.Containers?
            .Select(c => string.IsNullOrWhiteSpace(c.Barcode) ? null : c.Barcode.Trim())
            .Where(b => b is not null)
            .Distinct()
            .ToList() ?? new List<string?>();

        if (distinctContainerBarcodes.Count > 1)
        {
            throw new BusinessRuleException(
                "Tous les codes-barres d'un mandat doivent être identiques (même prélèvement, flacons multiples).");
        }

        var fromContainers = distinctContainerBarcodes.SingleOrDefault();
        var fromDto = string.IsNullOrWhiteSpace(dto.SampleBarcode) ? null : dto.SampleBarcode.Trim();

        // If both are provided, they must match.
        if (fromContainers is not null && fromDto is not null && fromContainers != fromDto)
        {
            throw new BusinessRuleException(
                "Tous les codes-barres d'un mandat doivent être identiques (même prélèvement, flacons multiples).");
        }

        return fromContainers ?? fromDto;
    }

    /// <summary>
    /// Ensures a barcode is not already used by another Sampling in the same tenant.
    /// </summary>
    private async Task EnsureBarcodeUniqueAcrossTenantAsync(
        string? barcode, Guid? currentSamplingId, Guid tenantId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(barcode)) return;

        var trimmed = barcode.Trim();
        var exists = await dbContext.Samplings
            .AnyAsync(s => s.TenantId == tenantId
                && s.SampleBarcode == trimmed
                && (currentSamplingId == null || s.Id != currentSamplingId),
                cancellationToken);

        if (exists)
        {
            throw new BusinessRuleException(
                $"Ce code-barres '{trimmed}' est déjà utilisé par un autre mandat.");
        }
    }

    private void UpsertContainers(
        Sampling sampling, IList<SamplingContainerInputDto> inputs)
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
            var existing = sampling.Containers.FirstOrDefault(c => c.ContainerId == input.ContainerId);
            if (existing is null)
            {
                dbContext.SamplingContainers.Add(new SamplingContainer
                {
                    Id = Guid.NewGuid(),
                    SamplingId = sampling.Id,
                    ContainerId = input.ContainerId,
                    BarcodeScannedAt = input.BarcodeScannedAt,
                    CreatedAt = DateTime.UtcNow,
                });
            }
            else if (input.BarcodeScannedAt.HasValue)
            {
                existing.BarcodeScannedAt = input.BarcodeScannedAt;
            }
        }
    }

    private static SamplingDto MapToDto(Sampling sampling)
    {
        // Every container of a mandate shares the same canonical barcode (Sampling.SampleBarcode).
        var containers = sampling.Containers
            .Select(c => new SamplingContainerDto(c.Id, c.ContainerId, sampling.SampleBarcode, c.BarcodeScannedAt))
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
