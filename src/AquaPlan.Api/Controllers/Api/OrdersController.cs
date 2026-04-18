using System.Security.Claims;
using AquaPlan.Application.DTOs.Orders;
using AquaPlan.Application.Services.Interfaces;
using AquaPlan.Domain.Enums;
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
    IOrderAuditService orderAuditService,
    IDelegationService delegationService,
    ILogger<OrdersController> logger) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<OrderPagedResultDto>> GetOrders(
        [FromQuery] List<OrderStatus>? statuses,
        [FromQuery] bool? isUnassigned,
        [FromQuery] bool? hasNoRound,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string? sortBy = null,
        [FromQuery] bool sortDescending = true,
        [FromQuery] Guid? distributorId = null,
        [FromQuery] string? preleveurId = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var isAdmin = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);

        var filter = new OrderFilterDto(statuses, isUnassigned, hasNoRound, search, page, pageSize, sortBy, sortDescending, distributorId, preleveurId, dateFrom, dateTo);
        var result = await orderService.GetOrdersFilteredAsync(userId, tenantId, filter, isAdmin, cancellationToken);

        return Ok(result);
    }

    [HttpGet("export")]
    public async Task<ActionResult> ExportOrders(
        [FromQuery] List<OrderStatus>? statuses,
        [FromQuery] string? search,
        [FromQuery] Guid? distributorId = null,
        [FromQuery] string? preleveurId = null,
        [FromQuery] DateTime? dateFrom = null,
        [FromQuery] DateTime? dateTo = null,
        CancellationToken cancellationToken = default)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();
        var isAdmin = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);

        if (!isAdmin)
        {
            return Forbid();
        }

        var filter = new OrderFilterDto(statuses, null, null, search, DistributorId: distributorId, PreleveurId: preleveurId, DateFrom: dateFrom, DateTo: dateTo);
        var csvBytes = await orderService.ExportOrdersCsvAsync(tenantId, filter, cancellationToken);
        return File(csvBytes, "text/csv", $"orders-export-{DateTime.UtcNow:yyyyMMdd}.csv");
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
    [Authorize(Roles = $"{RoleName.Administrator},{RoleName.Requerant},{RoleName.RequerantPreleveur}")]
    public async Task<ActionResult<OrderDetailDto>> CreateOrder([FromBody] OrderCreateDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        var hasViewAll = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        if (!hasViewAll)
        {
            // AQ-369 — user can only create on own distributor + distributors that delegated to him.
            var authorizedIds = await delegationService.GetAuthorizedDistributorIdsForUserAsync(userId, cancellationToken);
            if (!authorizedIds.Contains(dto.DistributorId))
            {
                return Forbid();
            }
        }

        try
        {
            var order = await orderService.CreateOrderAsync(dto, userId, tenantId, cancellationToken);
            return CreatedAtAction(nameof(GetOrder), new { id = order.Id }, order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<OrderDetailDto>> UpdateOrder(Guid id, [FromBody] OrderUpdateDto dto, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        // Verify access
        var hasViewAll = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        if (!hasViewAll)
        {
            var canAccess = await orderService.UserCanAccessOrderAsync(userId, id, tenantId, cancellationToken);
            if (!canAccess)
            {
                return Forbid();
            }
        }

        try
        {
            var order = await orderService.UpdateOrderAsync(id, dto, userId, tenantId, hasViewAll, cancellationToken);
            if (order is null)
            {
                return NotFound();
            }
            return Ok(order);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult> DeleteOrder(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        // Verify access
        var hasViewAll = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        if (!hasViewAll)
        {
            var canAccess = await orderService.UserCanAccessOrderAsync(userId, id, tenantId, cancellationToken);
            if (!canAccess)
            {
                return Forbid();
            }
        }

        try
        {
            var deleted = await orderService.DeleteOrderAsync(id, userId, tenantId, hasViewAll, cancellationToken);
            if (!deleted)
            {
                return NotFound();
            }
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
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

    [HttpGet("{id:guid}/required-containers")]
    public async Task<ActionResult<IList<RequiredContainerDto>>> GetRequiredContainers(Guid id, CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        var containers = await orderService.GetRequiredContainersAsync(id, tenantId, cancellationToken);
        if (containers is null)
        {
            return NotFound();
        }

        var hasViewAll = await permissionService.UserHasPermissionAsync(userId, "ViewAllOrders", cancellationToken);
        if (!hasViewAll)
        {
            var canAccess = await orderService.UserCanAccessOrderAsync(userId, id, tenantId, cancellationToken);
            if (!canAccess)
            {
                return Forbid();
            }
        }

        return Ok(containers);
    }

    [HttpPost("bulk-validate")]
    [Authorize(Roles = $"{RoleName.Administrator},{RoleName.Requerant},{RoleName.RequerantPreleveur}")]
    public async Task<ActionResult<BulkTransitionResultDto>> BulkValidate(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        var result = await orderService.BulkValidateAsync(userId, tenantId, cancellationToken);
        return Ok(result);
    }

    [HttpPost("bulk-transmit")]
    [Authorize(Roles = $"{RoleName.Administrator},{RoleName.Requerant},{RoleName.RequerantPreleveur}")]
    public async Task<ActionResult<BulkTransitionResultDto>> BulkTransmit(CancellationToken cancellationToken)
    {
        var userId = GetUserId();
        var tenantId = GetTenantId();

        var result = await orderService.BulkTransmitAsync(userId, tenantId, cancellationToken);
        return Ok(result);
    }

    [HttpGet("{id:guid}/audit-log")]
    public async Task<ActionResult<List<OrderAuditLogDto>>> GetAuditLog(Guid id, CancellationToken cancellationToken)
    {
        var tenantId = GetTenantId();
        var logs = await orderAuditService.GetByOrderIdAsync(id, tenantId, cancellationToken);
        return Ok(logs);
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
