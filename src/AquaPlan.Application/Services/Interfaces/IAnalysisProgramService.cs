using AquaPlan.Application.DTOs.AnalysisPrograms;

namespace AquaPlan.Application.Services.Interfaces;

public interface IAnalysisProgramService
{
    Task<IList<AnalysisProgramListDto>> GetAllAsync(Guid tenantId, AnalysisProgramFilteringInputDto? filter, CancellationToken cancellationToken = default);
    Task<AnalysisProgramDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<AnalysisProgramDto> CreateAsync(AnalysisProgramAddDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<AnalysisProgramDto?> UpdateAsync(Guid id, AnalysisProgramUpdateDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<AnalysisProgramDto?> ToggleStatusAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<AnalysisProgramDto?> AddProfilesAsync(Guid id, IList<Guid> profileIds, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> RemoveProfileAsync(Guid id, Guid profileId, Guid tenantId, CancellationToken cancellationToken = default);
}
