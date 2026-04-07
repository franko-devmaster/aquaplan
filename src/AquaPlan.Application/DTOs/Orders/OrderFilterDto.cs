using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.Orders;

public record OrderFilterDto(
    List<OrderStatus>? Statuses,
    bool? IsUnassigned,
    string? Search,
    int Page = 1,
    int PageSize = 20,
    string? SortBy = null,
    bool SortDescending = true,
    Guid? DistributorId = null,
    string? PreleveurId = null,
    DateTime? DateFrom = null,
    DateTime? DateTo = null);
