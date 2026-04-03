using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.Orders;

public record OrderStatusDto(
    OrderStatus Status,
    string Name,
    string Description,
    string Color,
    bool IsTerminal);
