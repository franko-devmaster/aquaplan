using AquaPlan.Application.DTOs.Sectors;

namespace AquaPlan.Application.Services.Interfaces;

public interface ISectorService
{
    /// <summary>
    /// AQ-418 — non-admin users only see sectors belonging to their authorized
    /// distributors (own + active delegations). Admins see every sector of the tenant.
    /// </summary>
    Task<IList<SectorListDto>> GetAllAsync(SectorFilteringInputDto filter, Guid tenantId, string userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<SectorDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SectorDto> CreateAsync(SectorAddDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SectorDto?> UpdateAsync(Guid id, SectorUpdateDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SectorDto?> ToggleStatusAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
}
