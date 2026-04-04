using AquaPlan.Application.DTOs.ChangeRequests;

namespace AquaPlan.Application.Services.Interfaces;

public interface ISamplingLocationChangeRequestService
{
    Task<ChangeRequestDto> SubmitCreateRequestAsync(ChangeRequestCreateDto dto, string userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<ChangeRequestDto> SubmitUpdateRequestAsync(Guid samplingLocationId, ChangeRequestUpdateDto dto, string userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<ChangeRequestDto> SubmitDeactivateRequestAsync(Guid samplingLocationId, string userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IList<ChangeRequestDto>> GetMyRequestsAsync(string userId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<IList<ChangeRequestDto>> GetPendingRequestsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<ChangeRequestDto?> GetByIdAsync(Guid id, Guid tenantId, CancellationToken cancellationToken = default);
    Task<ChangeRequestDto?> ApproveAsync(Guid id, string? comment, string reviewerId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<ChangeRequestDto?> RejectAsync(Guid id, string comment, string reviewerId, Guid tenantId, CancellationToken cancellationToken = default);
}
