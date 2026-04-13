using AquaPlan.Application.DTOs.Sectors;

namespace AquaPlan.Application.Services.Interfaces;

public interface ISectorService
{
    Task<IList<SectorListDto>> GetAllAsync(SectorFilteringInputDto filter, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SectorDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SectorDto> CreateAsync(SectorAddDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SectorDto?> UpdateAsync(Guid id, SectorUpdateDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SectorDto?> ToggleStatusAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
}
