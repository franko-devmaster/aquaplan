using AquaPlan.Application.DTOs.AnalysisProfiles;

namespace AquaPlan.Application.Services.Interfaces;

public interface IAnalysisProfileService
{
    Task<IList<AnalysisProfileListDto>> GetAllAsync(Guid tenantId, AnalysisProfileFilteringInputDto? filter, CancellationToken cancellationToken = default);
    Task<AnalysisProfileDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<AnalysisProfileDto> CreateAsync(AnalysisProfileAddDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<AnalysisProfileDto?> UpdateAsync(Guid id, AnalysisProfileUpdateDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<AnalysisProfileDto?> ToggleStatusAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
}
