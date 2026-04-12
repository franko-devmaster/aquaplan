using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.DTOs.Samplings;

namespace AquaPlan.Application.Services.Interfaces;

public interface ISamplingService
{
    Task<SamplingDto?> GetByOrderIdAsync(Guid orderId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingDto> CreateAsync(SamplingCreateDto dto, string preleveurId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingDto?> UpdateAsync(Guid orderId, SamplingCreateDto dto, string preleveurId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> CompleteAsync(Guid orderId, string preleveurId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<bool> ValidateAsync(Guid orderId, string validatorId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingDto?> ScanBarcodeAsync(Guid orderId, string barcode, string preleveurId, Guid tenantId, CancellationToken cancellationToken = default);
    Task<SamplingDto?> GetByBarcodeAsync(string barcode, Guid tenantId, CancellationToken cancellationToken = default);
}
