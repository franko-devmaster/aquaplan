using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.Orders;

public record OrderStatusTransitionDto(
    OrderStatus FromStatus,
    OrderStatus ToStatus,
    DateTime TransitionDate);
