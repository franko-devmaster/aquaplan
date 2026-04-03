using AquaPlan.Domain.Enums;

namespace AquaPlan.Application.DTOs.Orders;

public record OrderTransitionRequestDto(OrderStatus NewStatus);
