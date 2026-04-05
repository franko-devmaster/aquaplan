namespace AquaPlan.Application.DTOs.Orders;

public record OrderPagedResultDto(
    IList<OrderListDto> Items,
    int TotalCount,
    int Page,
    int PageSize);
