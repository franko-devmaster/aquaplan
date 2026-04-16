using System.ComponentModel.DataAnnotations;

namespace AquaPlan.Application.DTOs.ChangeRequests;

public record ChangeRequestCreateDto(
    [Required][StringLength(200)] string Name,
    [Required][StringLength(50)] string LocationCode,
    [StringLength(1000)] string? Description,
    [Required] Guid DistributorId);
