using AquaPlan.Application.DTOs.Containers;

namespace AquaPlan.Application.Services.Interfaces;

public interface IContainerService
{
    Task<IList<ContainerListDto>> GetAllAsync(Guid tenantId, ContainerFilteringInputDto? filter, CancellationToken cancellationToken = default);
    Task<ContainerDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<ContainerDto> CreateAsync(ContainerAddDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<ContainerDto?> UpdateAsync(Guid id, ContainerUpdateDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<ContainerDto?> ToggleStatusAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
}
