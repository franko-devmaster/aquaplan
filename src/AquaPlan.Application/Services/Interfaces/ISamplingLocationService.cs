using AquaPlan.Application.DTOs.SamplingLocations;

namespace AquaPlan.Application.Services.Interfaces;

public interface ISamplingLocationService
{
    Task<IList<SamplingLocationDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingLocationListDto> GetFilteredAsync(SamplingLocationFilteringInputDto filter, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IList<SamplingLocationDto>> GetByDistributorAsync(Guid distributorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IList<SamplingLocationDto>> GetForUserAsync(string userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingLocationDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingLocationDto> CreateAsync(SamplingLocationCreateDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingLocationDto?> UpdateAsync(Guid id, SamplingLocationUpdateDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<ToggleStatusResultDto?> ToggleStatusAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> IsLocationCodeUniqueAsync(string locationCode, Guid distributorId, Guid? excludeId, Guid tenantId, CancellationToken cancellationToken = default);
}
