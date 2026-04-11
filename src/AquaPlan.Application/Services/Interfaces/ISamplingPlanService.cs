using AquaPlan.Application.DTOs.SamplingPlans;

namespace AquaPlan.Application.Services.Interfaces;

public interface ISamplingPlanService
{
    Task<SamplingPlanPagedResultDto> GetPlansFilteredAsync(
        string userId, Guid tenantId, SamplingPlanFilterDto filter, bool isAdmin,
        CancellationToken cancellationToken = default);

    Task<SamplingPlanDetailDto?> GetPlanByIdAsync(
        Guid planId, Guid tenantId, CancellationToken cancellationToken = default);

    Task<SamplingPlanDetailDto> CreatePlanAsync(
        SamplingPlanCreateDto dto, string createdById, Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<SamplingPlanDetailDto?> UpdatePlanAsync(
        Guid planId, SamplingPlanUpdateDto dto, string updatedBy, Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> DeletePlanAsync(
        Guid planId, string deletedBy, Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<SamplingPlanDetailDto?> SubmitPlanAsync(
        Guid planId, string userId, Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<SamplingPlanDetailDto?> ValidatePlanAsync(
        Guid planId, string userId, Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<SamplingPlanDetailDto?> RejectPlanAsync(
        Guid planId, string reason, string userId, Guid tenantId,
        CancellationToken cancellationToken = default);

    Task<bool> UserHasDistributorAccessAsync(
        string userId, Guid distributorId,
        CancellationToken cancellationToken = default);

    Task<GenerateOrdersResultDto> GenerateOrdersFromPlanAsync(
        Guid planId, string userId, Guid tenantId,
        CancellationToken cancellationToken = default);
}
