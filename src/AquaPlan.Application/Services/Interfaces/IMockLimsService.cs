using AquaPlan.Application.DTOs.MockLims;

namespace AquaPlan.Application.Services.Interfaces;

public interface IMockLimsService
{
    Task<MockLimsOrderCreatedDto> ReceiveOrderAsync(MockLimsOrderCreateDto dto, Guid tenantId, CancellationToken cancellationToken = default);
    Task<MockLimsOrderDto?> GetAsync(Guid limsOrderId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<MockLimsResultListDto?> GetResultsAsync(Guid limsOrderId, Guid tenantId, CancellationToken cancellationToken = default);
}

public interface IMockLimsResultGenerator
{
    IReadOnlyList<MockLimsResultDto> Generate(IReadOnlyList<string> parameterCodes, int? seed = null);
}
