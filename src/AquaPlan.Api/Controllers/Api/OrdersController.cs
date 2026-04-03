using System.Security.Claims;
using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace AquaPlan.Api.Controllers.Api;

[Route("api/[controller]")]
[ApiController]
[Authorize]
public class OrdersController(
    IOrderService orderService,
    IOrderStatusService orderStatusService,
    IPermissionService permissionService,
    ILogger<OrdersController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IList<OrderListDto>>> GetOrders(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        // Admins see all orders; others see only their own
        var hasViewAll = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        var orders = hasViewAll
            ? await orderService.GetAllOrdersAsync(tenantId, cancellationToken)
            : await orderService.GetOrdersForUserAsync(userId, tenantId, cancellationToken);

        return Ok(orders);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<OrderDetailDto>> GetOrder(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        var order = await orderService.GetOrderByIdAsync(id, tenantId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }

        // Per-resource authorization: admin bypasses, others must have access
        var hasViewAll = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        if (!hasViewAll)
        {
            var canAccess = await orderService.UserCanAccessOrderAsync(userId, id, tenantId, cancellationToken);
            if (!canAccess)
            {
                return Forbid();
            }
        }

        return Ok(order);
    }

    [HttpPost]
    public async Task<ActionResult<OrderDetailDto>> CreateOrder([FromBody] OrderCreateDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        // Validate user has access to the distributor (admin bypasses)
        var hasViewAll = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        if (!hasViewAll)
        {
            var hasAccess = await orderService.UserHasDistributorAccessAsync(userId, dto.DistributorId, cancellationToken);
            if (!hasAccess)
            {
                return Forbid();
            }
        }

        var order = await orderService.CreateOrderAsync(dto, userId, tenantId, cancellationToken);
        return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
    }

    [HttpPost("{id:guid}/assign")]
    public async Task<ActionResult<OrderDetailDto>> AssignPreleveur(Guid id, [FromBody] OrderAssignDto dto, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var updatedBy = GetUserId();
        var order = await orderService.AssignPreleveurAsync(id, dto, updatedBy, tenantId, cancellationToken);
        if (order is null)
        {
            return NotFound();
        }
        return Ok(order);
    }

    [HttpPost("{id:guid}/transition")]
    public async Task<ActionResult<OrderStatusTransitionDto>> TransitionOrder(Guid id, [FromBody] OrderTransitionRequestDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        try
        {
            var transition = await orderStatusService.TransitionOrderAsync(id, dto.NewStatus, userId, tenantId, cancellationToken);
            return Ok(transition);
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    private string GetUserId()
    {
        return User.FindFirst(ClaimTypes.NameIdentifier)?.Value ?? throw new UnauthorizedAccessException();
    }

    private Guid GetTenantId()
    {
        var tenantClaim = User.FindFirst("tenant_id")?.Value ?? throw new UnauthorizedAccessException();
        return Guid.Parse(tenantClaim);
    }
}
