using System.ComponentModel.DataAnnotations;

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

// Polish F-220 — the two distributor IDs and the start date are mandatory. Cross-field rules
// (distinct distributors, ValidFrom <= ValidTo) are enforced in DelegationService (F-219).
public record DistributorDelegationCreateDto(
    [property: Required] Guid DelegatingDistributorId,
    [property: Required] Guid DelegatedToDistributorId,
    [property: Required] DateTime ValidFrom,
    DateTime? ValidTo);
