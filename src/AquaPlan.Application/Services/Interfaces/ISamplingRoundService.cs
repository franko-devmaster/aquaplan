using AquaPlan.Application.DTOs.SamplingRounds;

namespace AquaPlan.Application.Services.Interfaces;

public interface ISamplingRoundService
{
    Task<SamplingRoundDetailDto> CreateAsync(SamplingRoundCreateDto dto, string createdById, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingRoundDetailDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingRoundPagedResultDto> GetFilteredAsync(string userId, Guid tenantId, SamplingRoundFilterDto filter, bool isAdmin, CancellationToken cancellationToken = default);
    Task<SamplingRoundDetailDto?> UpdateAsync(Guid id, SamplingRoundUpdateDto dto, string updatedBy, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> DeleteAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingRoundDetailDto?> AssignPreleveurAsync(Guid id, SamplingRoundAssignDto dto, string updatedBy, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingRoundDetailDto?> CancelAsync(Guid id, string updatedBy, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingRoundDetailDto?> AddOrderAsync(Guid roundId, Guid orderId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> RemoveOrderAsync(Guid roundId, Guid orderId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingRoundDetailDto?> ReorderAsync(Guid roundId, SamplingRoundReorderDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> ReplaceLocationAsync(Guid orderId, LocationReplacementDto dto, string userId, Guid tenantId, bool isAdmin = false, CancellationToken cancellationToken = default);
    Task<bool> StartOrderAsync(Guid orderId, string userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> UpdateSamplerCommentAsync(Guid orderId, SamplerCommentDto dto, string userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingRoundDetailDto?> RevertToDraftAsync(Guid id, string userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingRoundDetailDto?> TransmitAllAsync(Guid id, string userId, Guid tenantId, CancellationToken cancellationToken = default);
}
