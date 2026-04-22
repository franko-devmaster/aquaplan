using AquaPlan.Application.DTOs.Distributors;

namespace AquaPlan.Application.Services.Interfaces;

public interface IDistributorService
{
    /// <summary>
    /// AQ-419 — non-admin users only see distributors they are authorized on
    /// (own + active delegations). Admins see every distributor of the tenant.
    /// </summary>
    Task<IList<DistributorListDto>> GetAllAsync(DistributorFilteringInputDto filter, Guid tenantId, string userId, bool isAdmin, CancellationToken cancellationToken = default);
    Task<DistributorDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<DistributorDto> CreateAsync(DistributorAddDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<DistributorDto?> UpdateAsync(Guid id, DistributorUpdateDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<DistributorDto?> ToggleStatusAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
}
