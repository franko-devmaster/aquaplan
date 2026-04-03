using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class OrderStatusController(
    IOrderStatusService orderStatusService) : ControllerBase
{
    [HttpGet]
    public ActionResult<IList<OrderStatusDto>> GetAllStatuses()
    {
        var statuses = orderStatusService.GetAllStatuses();
        return Ok(statuses);
    }

    [HttpGet("{status}/transitions")]
    public ActionResult<IList<OrderStatusDto>> GetAllowedTransitions(OrderStatus status)
    {
        var transitions = orderStatusService.GetAllowedTransitions(status);
        return Ok(transitions);
    }
}
