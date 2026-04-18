using AquaPlan.Application.DTOs.Delegations;
using AquaPlan.Application.DTOs.Distributors;

namespace AquaPlan.Application.Services.Interfaces;

public interface IDelegationService
{
    Task<List<DistributorDelegationDto>> GetAllAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<DistributorDelegationDto> CreateAsync(DistributorDelegationCreateDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<List<Guid>> GetDelegatedDistributorIdsAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// AQ-369 — returns the list of distributor IDs on which the user is authorized to
    /// create orders/rounds: their own DistributorId plus any DistributorDelegation
    /// (active, in window) where DelegatedToDistributorId belongs to the user.
    /// </summary>
    Task<List<Guid>> GetAuthorizedDistributorIdsForUserAsync(string userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// AQ-369 — returns the list of distributors on which the user is authorized to create.
    /// Admins receive every distributor of the tenant.
    /// </summary>
    Task<List<DistributorDto>> GetAuthorizedDistributorsForUserAsync(string userId, Guid tenantId, bool isAdmin, CancellationToken cancellationToken = default);
}
