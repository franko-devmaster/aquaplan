using AquaPlan.Application.DTOs.Delegations;

namespace AquaPlan.Application.Services.Interfaces;

public interface IDelegationService
{
    Task<List<DistributorDelegationDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<DistributorDelegationDto> CreateAsync(DistributorDelegationCreateDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetDelegatedDistributorIdsAsync(string userId, CancellationToken cancellationToken = default);
}
