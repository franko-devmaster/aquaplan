using AquaPlan.Application.DTOs.SamplingLocations;
using AquaPlan.Application.Exceptions;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace AquaPlan.Infrastructure.Services;

internal class SamplingLocationService(
    AquaPlanDbContext dbContext,
    IDelegationService delegationService,
    ILogger<SamplingLocationService> logger) : ISamplingLocationService
{
    public async Task<IList<SamplingLocationDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.SamplingLocations
            .Where(sl => sl.Distributor!.TenantId == tenantId)
            .Include(sl => sl.Distributor)
            .Include(sl => sl.Sector)
            .OrderBy(sl => sl.Name)
            .Select(sl => MapToDto(sl))
            .ToListAsync(cancellationToken);
    }

    public async Task<SamplingLocationListDto> GetFilteredAsync(SamplingLocationFilteringInputDto filter, Guid tenantId, IReadOnlyCollection<Guid>? authorizedDistributorIds = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.SamplingLocations
            .Where(sl => sl.Distributor!.TenantId == tenantId)
            .Include(sl => sl.Distributor)
            .Include(sl => sl.Sector)
            .AsQueryable();

        // Polish F-205 — non-admins only see LDP of their authorized distributors.
        if (authorizedDistributorIds is not null)
        {
            var ids = authorizedDistributorIds.ToList();
            query = query.Where(sl => ids.Contains(sl.DistributorId));
        }

        if (filter.DistributorId.HasValue)
        {
            query = query.Where(sl => sl.DistributorId == filter.DistributorId.Value);
        }

        if (filter.SectorId.HasValue)
        {
            query = query.Where(sl => sl.SectorId == filter.SectorId.Value);
        }

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var search = filter.Search.ToLower();
            query = query.Where(sl =>
                sl.Name.ToLower().Contains(search) ||
                sl.LocationCode.ToLower().Contains(search));
        }

        if (filter.IsActive.HasValue)
        {
            query = query.Where(sl => sl.IsActive == filter.IsActive.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken);

        var items = await query
            .OrderBy(sl => sl.Name)
            .Skip((filter.Page - 1) * filter.PageSize)
            .Take(filter.PageSize)
            .Select(sl => MapToDto(sl))
            .ToListAsync(cancellationToken);

        return new SamplingLocationListDto(items, totalCount, filter.Page, filter.PageSize);
    }

    public async Task<IList<SamplingLocationDto>> GetByDistributorAsync(Guid distributorId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        return await dbContext.SamplingLocations
            .Where(sl => sl.DistributorId == distributorId && sl.Distributor!.TenantId == tenantId)
            .Include(sl => sl.Distributor)
            .Include(sl => sl.Sector)
            .OrderBy(sl => sl.Name)
            .Select(sl => MapToDto(sl))
            .ToListAsync(cancellationToken);
    }

    public async Task<IList<SamplingLocationDto>> GetForUserAsync(string userId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        // Include the user's primary distributor (AppUser.DistributorId), the many-to-many
        // UserDistributors links, and any DistributorDelegation the user benefits from.
        // Bug fix: we previously only looked at UserDistributors, hiding LDP from users
        // whose sole link was the primary AppUser.DistributorId.
        var authorizedDistributorIds = await delegationService
            .GetAuthorizedDistributorIdsForUserAsync(userId, cancellationToken);

        return await dbContext.SamplingLocations
            .Where(sl => authorizedDistributorIds.Contains(sl.DistributorId) && sl.Distributor!.TenantId == tenantId)
            .Include(sl => sl.Distributor)
            .Include(sl => sl.Sector)
            .OrderBy(sl => sl.Name)
            .Select(sl => MapToDto(sl))
            .ToListAsync(cancellationToken);
    }

    public async Task<SamplingLocationDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var location = await dbContext.SamplingLocations
            .Where(sl => sl.Id == id && sl.Distributor!.TenantId == tenantId)
            .Include(sl => sl.Distributor)
            .Include(sl => sl.Sector)
            .FirstOrDefaultAsync(cancellationToken);

        if (location is null)
        {
            return null;
        }

        var hasOrders = await dbContext.Orders
            .AnyAsync(o => o.SamplingLocationId == id, cancellationToken);

        return MapToDto(location) with { CanDelete = !hasOrders };
    }

    public async Task<SamplingLocationDto> CreateAsync(SamplingLocationCreateDto dto, Guid tenantId, bool isValidated = true, CancellationToken cancellationToken = default)
    {
        // Sprint Robustesse F-112 — the LDP's tenant is derived from its distributor, so the
        // distributor MUST belong to the caller's tenant; otherwise an LDP could be written into
        // another tenant. (Per-distributor authorization for non-admins is enforced at the
        // controller via GetAuthorizedDistributorIdsForUserAsync.)
        var distributorInTenant = await dbContext.Distributors
            .AnyAsync(d => d.Id == dto.DistributorId && d.TenantId == tenantId, cancellationToken);
        if (!distributorInTenant)
        {
            throw new BusinessRuleException("The distributor does not belong to your tenant.");
        }

        var isUnique = await IsLocationCodeUniqueAsync(dto.LocationCode, dto.DistributorId, null, tenantId, cancellationToken);
        if (!isUnique)
        {
            throw new BusinessRuleException($"A sampling location with code '{dto.LocationCode}' already exists for this distributor.");
        }

        var location = new SamplingLocation
        {
            Id = Guid.NewGuid(),
            Name = dto.Name,
            LocationCode = dto.LocationCode,
            Description = dto.Description,
            Address = dto.Address,
            AccessDescription = dto.AccessDescription,
            DistributorId = dto.DistributorId,
            SectorId = dto.SectorId,
            IsValidated = isValidated,
            IsActive = isValidated,
        };

        dbContext.SamplingLocations.Add(location);
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Sampling location {Name} ({Code}) created", dto.Name, dto.LocationCode);

        return (await GetByIdAsync(location.Id, tenantId, cancellationToken))!;
    }

    public async Task<SamplingLocationDto?> UpdateAsync(Guid id, SamplingLocationUpdateDto dto, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var location = await dbContext.SamplingLocations
            .Where(sl => sl.Id == id && sl.Distributor!.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (location is null)
        {
            return null;
        }

        var isUnique = await IsLocationCodeUniqueAsync(dto.LocationCode, location.DistributorId, id, tenantId, cancellationToken);
        if (!isUnique)
        {
            throw new BusinessRuleException($"A sampling location with code '{dto.LocationCode}' already exists for this distributor.");
        }

        location.Name = dto.Name;
        location.LocationCode = dto.LocationCode;
        location.Description = dto.Description;
        location.Address = dto.Address;
        location.AccessDescription = dto.AccessDescription;
        location.IsActive = dto.IsActive;
        location.SectorId = dto.SectorId;

        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Sampling location {Id} updated", id);

        return await GetByIdAsync(id, tenantId, cancellationToken);
    }

    public async Task<ToggleStatusResultDto?> ToggleStatusAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var location = await dbContext.SamplingLocations
            .Where(sl => sl.Id == id && sl.Distributor!.TenantId == tenantId)
            .FirstOrDefaultAsync(cancellationToken);

        if (location is null)
        {
            return null;
        }

        location.IsActive = !location.IsActive;
        await dbContext.SaveChangesAsync(cancellationToken);

        logger.LogInformation("Sampling location {Id} status toggled to {IsActive}", id, location.IsActive);

        var dto = (await GetByIdAsync(id, tenantId, cancellationToken))!;

        // Warning placeholder: when SamplingLocation is referenced by active orders/samplings,
        // add a warning message here. Currently no FK exists from Order/Sampling to SamplingLocation.
        var hasActiveReferences = false;
        string? warning = null;

        return new ToggleStatusResultDto(dto, hasActiveReferences, warning);
    }

    public async Task<bool> IsLocationCodeUniqueAsync(string locationCode, Guid distributorId, Guid? excludeId, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var query = dbContext.SamplingLocations
            .Where(sl => sl.LocationCode == locationCode
                && sl.DistributorId == distributorId
                && sl.Distributor!.TenantId == tenantId);

        if (excludeId.HasValue)
        {
            query = query.Where(sl => sl.Id != excludeId.Value);
        }

        return !await query.AnyAsync(cancellationToken);
    }

    public async Task<byte[]> ExportPdfAsync(Guid tenantId, Guid? distributorId = null, CancellationToken cancellationToken = default)
    {
        var query = dbContext.SamplingLocations
            .Where(sl => sl.Distributor!.TenantId == tenantId && sl.IsActive)
            .Include(sl => sl.Distributor)
            .AsQueryable();

        if (distributorId.HasValue)
        {
            query = query.Where(sl => sl.DistributorId == distributorId.Value);
        }

        var locations = await query
            .OrderBy(sl => sl.Distributor!.Name)
            .ThenBy(sl => sl.LocationCode)
            .ToListAsync(cancellationToken);

        QuestPDF.Settings.License = LicenseType.Community;

        var document = Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(1.5f, Unit.Centimetre);
                page.DefaultTextStyle(x => x.FontSize(9));

                page.Header().Element(header =>
                {
                    header.Column(col =>
                    {
                        col.Item().Text("Liste des lieux de prélèvement")
                            .Bold().FontSize(16).FontColor(Colors.Blue.Darken2);
                        col.Item().Text($"Générée le {DateTime.Now:dd.MM.yyyy}")
                            .FontSize(8).FontColor(Colors.Grey.Darken1);
                        col.Item().PaddingBottom(10);
                    });
                });

                page.Content().Table(table =>
                {
                    table.ColumnsDefinition(columns =>
                    {
                        columns.RelativeColumn(1.2f);  // Code
                        columns.RelativeColumn(2f);     // Nom
                        columns.RelativeColumn(2f);     // Distributeur
                        columns.RelativeColumn(3f);     // Description
                    });

                    // Header row
                    table.Header(h =>
                    {
                        void HeaderCell(IContainer container, string text)
                        {
                            container
                                .Background(Colors.Blue.Darken2)
                                .Padding(4)
                                .Text(text).Bold().FontColor(Colors.White).FontSize(9);
                        }

                        HeaderCell(h.Cell(), "Code");
                        HeaderCell(h.Cell(), "Nom");
                        HeaderCell(h.Cell(), "Distributeur");
                        HeaderCell(h.Cell(), "Description");
                    });

                    // Data rows
                    var rowIndex = 0;
                    foreach (var loc in locations)
                    {
                        var bgColor = rowIndex % 2 == 0 ? Colors.White : Colors.Grey.Lighten4;

                        void DataCell(IContainer container, string text)
                        {
                            container
                                .Background(bgColor)
                                .BorderBottom(0.5f)
                                .BorderColor(Colors.Grey.Lighten2)
                                .Padding(4)
                                .Text(text).FontSize(8);
                        }

                        DataCell(table.Cell(), loc.LocationCode);
                        DataCell(table.Cell(), loc.Name);
                        DataCell(table.Cell(), loc.Distributor?.Name ?? "-");
                        DataCell(table.Cell(), loc.Description ?? "-");

                        rowIndex++;
                    }
                });

                page.Footer().AlignCenter().Text(text =>
                {
                    text.Span("AquaPlan — Page ");
                    text.CurrentPageNumber();
                    text.Span(" / ");
                    text.TotalPages();
                });
            });
        });

        using var stream = new MemoryStream();
        document.GeneratePdf(stream);
        return stream.ToArray();
    }

    public async Task<SamplingLocationDto?> ValidateAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var location = await dbContext.SamplingLocations
            .Include(sl => sl.Distributor)
            .Include(sl => sl.Sector)
            .Where(sl => sl.Distributor!.TenantId == tenantId && sl.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (location is null)
        {
            return null;
        }

        location.IsValidated = true;
        location.IsActive = true;
        await dbContext.SaveChangesAsync(cancellationToken);
        return MapToDto(location);
    }

    public async Task<IList<SamplingLocationDto>> GetUnvalidatedAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var locations = await dbContext.SamplingLocations
            .Include(sl => sl.Distributor)
            .Include(sl => sl.Sector)
            .Where(sl => sl.Distributor!.TenantId == tenantId && !sl.IsValidated)
            .OrderByDescending(sl => sl.CreatedAt)
            .ToListAsync(cancellationToken);

        return locations.Select(MapToDto).ToList();
    }

    public async Task<bool> DeleteAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default)
    {
        var hasOrders = await dbContext.Orders
            .AnyAsync(o => o.SamplingLocationId == id, cancellationToken);
        if (hasOrders)
        {
            throw new BusinessRuleException("Cannot delete a sampling location that is referenced by orders. Deactivate it instead.");
        }

        var location = await dbContext.SamplingLocations
            .Include(sl => sl.Distributor)
            .Where(sl => sl.Distributor!.TenantId == tenantId && sl.Id == id)
            .FirstOrDefaultAsync(cancellationToken);

        if (location is null)
        {
            return false;
        }

        dbContext.SamplingLocations.Remove(location);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static SamplingLocationDto MapToDto(SamplingLocation sl)
    {
        return new SamplingLocationDto(
            sl.Id, sl.Name, sl.LocationCode,
            sl.Description, sl.Address, sl.AccessDescription,
            sl.IsActive, sl.IsValidated, sl.DistributorId,
            sl.Distributor != null ? sl.Distributor.Name : null,
            sl.SectorId,
            sl.Sector != null ? sl.Sector.Name : null,
            sl.CreatedAt);
    }
}
