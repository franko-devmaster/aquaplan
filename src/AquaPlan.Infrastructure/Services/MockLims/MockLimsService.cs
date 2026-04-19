using System.Text.Json;
using AquaPlan.Application.DTOs.MockLims;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Entities;
using AquaPlan.Domain.Enums;
using AquaPlan.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace AquaPlan.Infrastructure.Services.MockLims;

internal class MockLimsService(
    AquaPlanDbContext dbContext,
    IMockLimsResultGenerator resultGenerator)
    : IMockLimsService
{
    public async Task<MockLimsOrderCreatedDto> ReceiveOrderAsync(
        MockLimsOrderCreateDto dto,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (string.IsNullOrWhiteSpace(dto.OrderReference))
        {
            throw new ArgumentException("OrderReference is required", nameof(dto));
        }

        if (dto.Parameters is null || dto.Parameters.Count == 0)
        {
            throw new ArgumentException("At least one parameter code is required", nameof(dto));
        }

        var existing = await dbContext.MockLimsOrders
            .FirstOrDefaultAsync(o => o.TenantId == tenantId && o.OrderReference == dto.OrderReference, cancellationToken);
        if (existing is not null)
        {
            return new MockLimsOrderCreatedDto(existing.LimsOrderId, existing.ReceivedAt);
        }

        var now = DateTime.UtcNow;
        var entity = new MockLimsOrder
        {
            Id = Guid.NewGuid(),
            LimsOrderId = Guid.NewGuid(),
            OrderId = dto.SourceOrderId,
            OrderReference = dto.OrderReference,
            SamplingDate = dto.SamplingDate,
            ParametersJson = JsonSerializer.Serialize(dto.Parameters),
            Status = MockLimsOrderStatus.Received,
            ReceivedAt = now,
            TenantId = tenantId,
        };

        dbContext.MockLimsOrders.Add(entity);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new MockLimsOrderCreatedDto(entity.LimsOrderId, entity.ReceivedAt);
    }

    public async Task<MockLimsOrderDto?> GetAsync(
        Guid limsOrderId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.MockLimsOrders
            .FirstOrDefaultAsync(o => o.LimsOrderId == limsOrderId && o.TenantId == tenantId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        return MapToDto(entity);
    }

    public async Task<MockLimsResultListDto?> GetResultsAsync(
        Guid limsOrderId,
        Guid tenantId,
        CancellationToken cancellationToken = default)
    {
        var entity = await dbContext.MockLimsOrders
            .FirstOrDefaultAsync(o => o.LimsOrderId == limsOrderId && o.TenantId == tenantId, cancellationToken);
        if (entity is null)
        {
            return null;
        }

        IReadOnlyList<MockLimsResultDto> results;
        if (!string.IsNullOrWhiteSpace(entity.ResultsJson))
        {
            results = JsonSerializer.Deserialize<List<MockLimsResultDto>>(entity.ResultsJson) ?? new List<MockLimsResultDto>();
        }
        else
        {
            var parameters = JsonSerializer.Deserialize<List<string>>(entity.ParametersJson) ?? new List<string>();
            // Deterministic seed derived from Id to get stable results between calls.
            var seed = entity.Id.GetHashCode();
            results = resultGenerator.Generate(parameters, seed);

            entity.ResultsJson = JsonSerializer.Serialize(results);
            entity.Status = MockLimsOrderStatus.ResultsReady;
            entity.ResultsReadyAt = DateTime.UtcNow;
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new MockLimsResultListDto(entity.LimsOrderId, results);
    }

    private static MockLimsOrderDto MapToDto(MockLimsOrder entity)
    {
        var parameters = JsonSerializer.Deserialize<List<string>>(entity.ParametersJson) ?? new List<string>();
        return new MockLimsOrderDto(
            entity.Id,
            entity.LimsOrderId,
            entity.OrderId,
            entity.OrderReference,
            entity.SamplingDate,
            parameters,
            entity.Status,
            entity.ReceivedAt,
            entity.ResultsReadyAt);
    }
}
