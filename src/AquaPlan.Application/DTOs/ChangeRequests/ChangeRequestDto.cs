using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.ChangeRequests;

public record ChangeRequestDto(
    Guid Id,
    ChangeRequestType RequestType,
    ChangeRequestStatus Status,
    Guid? SamplingLocationId,
    string? SamplingLocationName,
    Guid DistributorId,
    string? DistributorName,
    string? ProposedName,
    string? ProposedLocationCode,
    string? ProposedDescription,
    string RequestedById,
    string? RequestedByName,
    DateTime RequestedAt,
    string? ReviewedById,
    string? ReviewedByName,
    DateTime? ReviewedAt,
    string? ReviewComment);
