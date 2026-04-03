using AquaPlan.Application.DTOs.Distributors;

namespace AquaPlan.Application.Services.Interfaces;

public interface IDistributorService
{
    Task<IList<DistributorListDto>> GetAllAsync(DistributorFilteringInputDto filter, Guid tenantId, CancellationToken cancellationToken = default);
    Task<DistributorDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<DistributorDto> CreateAsync(DistributorAddDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<DistributorDto?> UpdateAsync(Guid id, DistributorUpdateDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<DistributorDto?> ToggleStatusAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
}
