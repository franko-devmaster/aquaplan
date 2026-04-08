namespace AquaPlan.Application.DTOs.Delegations;

public record DistributorDelegationDto(
    Guid Id,
    Guid DelegatingDistributorId,
    string DelegatingDistributorName,
    Guid DelegatedToDistributorId,
    string DelegatedToDistributorName,
    DateTime ValidFrom,
    DateTime? ValidTo,
    bool IsActive,
    DateTime CreatedAt);

public record DistributorDelegationCreateDto(
    Guid DelegatingDistributorId,
    Guid DelegatedToDistributorId,
    DateTime ValidFrom,
    DateTime? ValidTo);
